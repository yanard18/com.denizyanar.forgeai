using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using System.Threading.Tasks;
using DenizYanar.ForgeAI.Editor; // For Task<string>

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
        private string _accumulatedContext = "";
        
        // Flags to manage UI state
        private bool _isThinking = false;
        private bool _isStepPrepared = false; 

        public override string GenerateFullPrompt(string userInstruction)
        {
            var taskTypes = TypeCache.GetTypesDerivedFrom<AITask>();
            StringBuilder toolsBuilder = new StringBuilder();
            
            foreach (var t in taskTypes)
            {
                if (t.IsAbstract || t == typeof(AgentOrchestratorTask)) continue;
                var instance = (AITask)Activator.CreateInstance(t);
                toolsBuilder.AppendLine($"- Tool Name: \"{instance.GetType().Name}\"");
                toolsBuilder.AppendLine($"  Description: {instance.ToolDescription}");
            }

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
            sb.AppendLine("{ \"steps\": [ { \"toolName\": \"GitOperationTask\", \"instruction\": \"Get last 50 commits\", \"reasoning\": \"Need logs first\" } ] }");

            return sb.ToString();
        }

        public override void ProcessResponse(string response)
        {
            try
            {
                string cleanedJson = ExtractJson(response);
                var planData = JsonUtility.FromJson<AgentPlan>(cleanedJson);
                
                if (planData != null && planData.steps != null)
                {
                    _plan = planData.steps;
                    CreateSubTasksFromPlan();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ForgeAI] Plan Parse Failed: {e.Message}");
            }
        }

        private string ExtractJson(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string clean = raw.Replace("```json", "").Replace("```", "").Trim();
            int firstBrace = clean.IndexOf('{');
            int lastBrace = clean.LastIndexOf('}');
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
                var type = TypeCache.GetTypesDerivedFrom<AITask>().FirstOrDefault(t => t.Name == step.toolName);
                if (type != null) _subTasks.Add((AITask)Activator.CreateInstance(type));
                else Debug.LogError($"Unknown tool: {step.toolName}");
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

                // Visual Highlight for current step
                GUI.backgroundColor = isCurrent ? new Color(0.8f, 1f, 0.8f) : (isDone ? new Color(0.9f, 0.9f, 0.9f) : Color.white);
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.backgroundColor = Color.white;

                GUILayout.Label($"Step {i + 1}: {step.reasoning} ({step.toolName})", EditorStyles.boldLabel);
                GUILayout.Label($"Instructions: {step.instruction}", EditorStyles.miniLabel);

                // Only draw the sub-task UI if it's the active one or already done
                if (isCurrent || isDone)
                {
                    task.DrawUI();
                }

                GUILayout.EndVertical();
            }

            GUILayout.Space(10);
            
            // --- MAIN CONTROL BUTTONS ---
            DrawControlButtons();
        }

        private void DrawControlButtons()
        {
            if (_currentStepIndex >= _subTasks.Count)
            {
                EditorGUILayout.HelpBox("All steps completed successfully!", MessageType.Info);
                return;
            }

            var currentTask = _subTasks[_currentStepIndex];

            // STATE 1: Thinking (API Call in progress)
            if (_isThinking)
            {
                GUILayout.Label("Thinking... Please wait.", EditorStyles.boldLabel);
                return;
            }

            // STATE 2: Task Prepared (User needs to Review & Execute inside the sub-task UI)
            // We check !IsExecuted to verify the user hasn't clicked "Confirm" yet.
            if (_isStepPrepared && !currentTask.IsExecuted)
            {
                EditorGUILayout.HelpBox("⚠ Action Required: Review the proposed plan above and click 'Confirm & Execute' to proceed.", MessageType.Warning);
            }
            // STATE 3: Task Executed (Ready to move to next step)
            else if (currentTask.IsExecuted)
            {
                GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                if (GUILayout.Button("▶ Proceed to Next Step", GUILayout.Height(30)))
                {
                    AdvanceToNextStep();
                }
                GUI.backgroundColor = Color.white;
            }
            // STATE 4: Not Started (Need to generate prompt)
            else
            {
                if (GUILayout.Button($"▶ Initialize Step {_currentStepIndex + 1}", GUILayout.Height(30)))
                {
                    PrepareCurrentStep();
                }
            }
        }

        private async void PrepareCurrentStep()
        {
            _isThinking = true;
            var currentTask = _subTasks[_currentStepIndex];
            var currentStepInfo = _plan[_currentStepIndex];

            // 1. Inject Context from previous steps
            if (!string.IsNullOrEmpty(_accumulatedContext))
            {
                currentTask.ContextFromPreviousSteps = _accumulatedContext;
            }

            // 2. Generate Prompt & Call API
            string prompt = currentTask.GenerateFullPrompt(currentStepInfo.instruction);
            
            // Assuming Editor Code access:
            string apiKey = AIProjectPreferences.APIKey; 
            string response = await AIClient.SendRequestAsync(prompt, apiKey);
            
            currentTask.ProcessResponse(response);

            // 3. Update State - WE DO NOT AUTO-EXECUTE HERE
            _isThinking = false;
            _isStepPrepared = true; 
            
            // Now the DrawUI loop will show the Sub-Task's "Proposed Plan"
        }

        private void AdvanceToNextStep()
        {
            var currentTask = _subTasks[_currentStepIndex];
            var currentStepInfo = _plan[_currentStepIndex];

            // 1. Capture Result (Now that the user has definitely executed it)
            string result = currentTask.GetExecutionResult();
            _accumulatedContext += $"\n--- Output of Step {_currentStepIndex + 1} ({currentStepInfo.toolName}) ---\n{result}\n";

            // 2. Advance Index
            _currentStepIndex++;
            _isStepPrepared = false; // Reset for next task

            // 3. Optional: Auto-start the next step's preparation?
            // Uncomment the line below if you want to immediately start thinking about the next step
            // if (_currentStepIndex < _subTasks.Count) PrepareCurrentStep();
        }

        public override void Execute()
        {
            // Initial Trigger
            if (_currentStepIndex == 0 && !_isStepPrepared)
            {
                PrepareCurrentStep();
            }
        }
    }
}