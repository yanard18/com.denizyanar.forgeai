using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using DenizYanar.ForgeAI.Editor; // Assuming AIClient is here

namespace DenizYanar.ForgeAI.Tasks
{
    [Serializable]
    public class PlannedStep
    {
        public string toolName;
        public string instruction;
        public string reasoning;
    }

    [Serializable]
    public class AgentPlan
    {
        public List<PlannedStep> steps;
    }

    public class AgentOrchestratorTask : AITask
    {
        public override string DisplayName => "Auto-Agent";
        public override string ToolDescription => "Decides which tools to use to solve complex problems.";

        private List<PlannedStep> _plan = new List<PlannedStep>();
        private List<AITask> _subTasks = new List<AITask>();
        
        // State tracking
        private int _currentStepIndex = 0;
        private bool _isExecutingChain = false;
        private string _accumulatedContext = "";

        public override string GenerateFullPrompt(string userInstruction)
        {
            // 1. Gather all available tools via Reflection (excluding this one to prevent recursion)
            var taskTypes = TypeCache.GetTypesDerivedFrom<AITask>();
            StringBuilder toolsBuilder = new StringBuilder();
            
            foreach (var t in taskTypes)
            {
                if (t.IsAbstract || t == typeof(AgentOrchestratorTask)) continue;
                var instance = (AITask)Activator.CreateInstance(t);
                toolsBuilder.AppendLine($"- Tool Name: \"{instance.GetType().Name}\"");
                toolsBuilder.AppendLine($"  Description: {instance.ToolDescription}");
            }

            // 2. Build the Planner Prompt
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("You are an AI Architect. Break the user's request into a sequential plan using the tools below.");
            sb.AppendLine("AVAILABLE TOOLS:");
            sb.Append(toolsBuilder.ToString());
            sb.AppendLine("---");
            sb.AppendLine($"USER REQUEST: \"{userInstruction}\"");
            sb.AppendLine("---");
            sb.AppendLine("REQUIREMENTS:");
            sb.AppendLine("1. Return ONLY valid JSON.");
            sb.AppendLine("2. Pass output of previous steps to the next implicitly.");
            sb.AppendLine("3. If the user wants to filter/undo commits, ALWAYS use GitOperationTask to get logs first.");
            sb.AppendLine("Example JSON Format:");
            sb.AppendLine("{ \"steps\": [ { \"toolName\": \"GitOperationTask\", \"instruction\": \"Get last 50 commits\", \"reasoning\": \"Need logs first\" }, { \"toolName\": \"ChatTask\", \"instruction\": \"Analyze logs and identify Deniz's commits\", \"reasoning\": \"Filter data\" } ] }");

            return sb.ToString();
        }

        
        public override void ProcessResponse(string response)
        {
            try
            {
                // 1. Clean the response before parsing
                string cleanedJson = ExtractJson(response);
                
                // Debug log to see exactly what we are trying to parse (crucial for debugging)
                // Debug.Log($"[ForgeAI] Raw: {response.Length} chars | Cleaned: {cleanedJson}");

                var planData = JsonUtility.FromJson<AgentPlan>(cleanedJson);
                
                if (planData != null && planData.steps != null)
                {
                    _plan = planData.steps;
                    CreateSubTasksFromPlan();
                }
                else
                {
                    Debug.LogError($"[ForgeAI] Parsed JSON was empty or invalid.\nPayload: {cleanedJson}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ForgeAI] JSON Parse Failed: {e.Message}\nOriginal Response: {response}");
            }
        }

        /// <summary>
        /// Helper to strip Markdown (```json) and non-JSON text from LLM responses.
        /// </summary>
        private string ExtractJson(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            // 1. Remove markdown code block delimiters
            // Note: We replace "```json" first to catch the specific tag, then generic "```"
            string clean = raw.Replace("```json", "").Replace("```", "").Trim();

            // 2. Find the start and end of the actual JSON object
            int firstBrace = clean.IndexOf('{');
            int lastBrace = clean.LastIndexOf('}');

            // If we found valid braces, extract just that substring
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                clean = clean.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            return clean;
        }
        

        private void CreateSubTasksFromPlan()
        {
            _subTasks.Clear();
            foreach (var step in _plan)
            {
                // Find the type by name
                var type = TypeCache.GetTypesDerivedFrom<AITask>()
                    .FirstOrDefault(t => t.Name == step.toolName);

                if (type != null)
                {
                    var task = (AITask)Activator.CreateInstance(type);
                    _subTasks.Add(task);
                }
                else
                {
                    Debug.LogError($"Agent planned to use unknown tool: {step.toolName}");
                }
            }
        }

        public override void DrawUI()
        {
            if (_plan == null || _plan.Count == 0)
            {
                GUILayout.Label("Waiting for plan...");
                return;
            }

            GUILayout.Label("📋 Execution Plan", EditorStyles.boldLabel);

            for (int i = 0; i < _subTasks.Count; i++)
            {
                var step = _plan[i];
                var task = _subTasks[i];
                bool isCurrent = i == _currentStepIndex;
                bool isDone = i < _currentStepIndex;

                GUI.backgroundColor = isCurrent ? new Color(0.7f, 1f, 0.7f) : (isDone ? Color.gray : Color.white);
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.backgroundColor = Color.white;

                GUILayout.Label($"Step {i + 1}: {step.reasoning} ({step.toolName})", EditorStyles.boldLabel);
                GUILayout.Label($"Instructions: {step.instruction}", EditorStyles.miniLabel);

                if (isCurrent || isDone)
                {
                    // Draw the UI of the sub-task (e.g. the Git Table)
                    task.DrawUI();
                }

                GUILayout.EndVertical();
            }

            GUILayout.Space(10);

            if (!_isExecutingChain && _currentStepIndex < _subTasks.Count)
            {
                if (GUILayout.Button("▶ Run Plan Step-by-Step"))
                {
                    ExecuteNextStep();
                }
            }
        }

        private async void ExecuteNextStep()
        {
            if (_currentStepIndex >= _subTasks.Count) return;

            _isExecutingChain = true;
            var currentTask = _subTasks[_currentStepIndex];
            var currentStepInfo = _plan[_currentStepIndex];

            if (!string.IsNullOrEmpty(_accumulatedContext))
            {
                currentTask.ContextFromPreviousSteps = _accumulatedContext;
            }
           

            // 2. PREPARE THE TASK (API CALL)
            // Most tasks need to hit the API to convert instruction -> operations
            // We simulate what AIWindow does here
            string prompt = currentTask.GenerateFullPrompt(currentStepInfo.instruction);
            
            Debug.Log($"[Agent] Step {_currentStepIndex} Prompt Preview:\n{prompt.Substring(0, Mathf.Min(prompt.Length, 500))}...");
            
            // Assuming we have access to the API key from Preferences
            string apiKey = AIProjectPreferences.APIKey; 
            string response = await AIClient.SendRequestAsync(prompt, apiKey);
            
            currentTask.ProcessResponse(response);

            // 3. AUTO-EXECUTE (Optional)
            // If the task is a simple Git Log, we might want to auto-run it.
            // If it's a destructive Git Revert, we might want to wait for user confirmation.
            // For now, let's auto-execute to get the output for the next step.
            currentTask.Execute();

            // 4. CAPTURE RESULT
            string result = currentTask.GetExecutionResult();
            _accumulatedContext += $"\n--- Output of Step {_currentStepIndex+1} ({currentStepInfo.toolName}) ---\n{result}\n";

            _currentStepIndex++;
            _isExecutingChain = false;
            
            // Trigger repaint of window if possible
            // In a real scenario, you'd use an event or callback to repaint the EditorWindow
        }

        public override void Execute()
        {
            // The "Execute" button in the main window triggers the chain
            ExecuteNextStep();
        }
    }
}