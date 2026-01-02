using UnityEditor;
using UnityEngine;
using System.Text; // Required for StringBuilder

namespace DenizYanar.ForgeAI.Tasks
{
    public class MessageTask : AITask
    {
        public override string DisplayName => "Chat / Analysis";
        
        // KEY CHANGE 1: Describe this tool so the Orchestrator knows when to use it.
        public override string ToolDescription => "Analyzes text, filters logs, explains code, or answers general questions.";

        private string _responseText;
        private Vector2 _scrollPosition;

        public override string GenerateFullPrompt(string userInstruction)
        {
            StringBuilder sb = new StringBuilder();

            // Set the Persona
            sb.AppendLine("You are a helpful Unity and Git Expert.");

            if (!string.IsNullOrEmpty(ContextFromPreviousSteps))
            {
                sb.AppendLine("\n=== DATA TO ANALYZE ===");
                sb.AppendLine(ContextFromPreviousSteps);
                sb.AppendLine("=======================\n");
                
                sb.AppendLine("Based on the data above, please address the following request:");
            }

            sb.AppendLine($"User Instruction: \"{userInstruction}\"");

            return sb.ToString();
        }

        public override void ProcessResponse(string rawResponse)
        {
            _responseText = rawResponse.Trim();
            IsExecuted = true; 
            ExecutionResult = _responseText; // Store result so it can be passed to future steps if needed
        }

        public override void DrawUI()
        {
            if (string.IsNullOrEmpty(_responseText)) return;

            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Analysis Result:", EditorStyles.boldLabel);

            // Scroll view for long analysis results
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));
            
            // Rich text support allows bolding/coloring if the AI uses Markdown
            var style = new GUIStyle(EditorStyles.textArea) { richText = true, wordWrap = true };
            EditorGUILayout.TextArea(_responseText, style, GUILayout.ExpandHeight(true));
            
            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        public override void Execute()
        {
            // Optional: Copy result to clipboard
            if (!string.IsNullOrEmpty(_responseText))
            {
                GUIUtility.systemCopyBuffer = _responseText;
                Debug.Log("Analysis copied to clipboard.");
            }
        }
    }
}