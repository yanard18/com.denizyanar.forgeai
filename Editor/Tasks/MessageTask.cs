using UnityEditor;
using UnityEngine;
using System.Text; // Required for StringBuilder

namespace DenizYanar.ForgeAI.Tasks
{
    public class MessageTask : AITask
    {
        public override string DisplayName => "Chat / Analysis";
        public override string ToolDescription => "Analyzes text, filters logs, explains code.";

        private Vector2 _scrollPosition;

        public override string GenerateFullPrompt(string userInstruction)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("You are a helpful Unity Expert.");

            // ONE LINE to inject logs, code, or file lists
            AppendContextIfAvailable(sb);

            sb.AppendLine($"User Instruction: \"{userInstruction}\"");
            return sb.ToString();
        }

        public override void ProcessResponse(string rawResponse) 
        {
            // Save to the standard pipeline storage
            _executionData = rawResponse.Trim();
            StatusMessage = "Response Received";
            IsExecuted = true;
        }

        public override void DrawUI()
        {
            // BUG FIX: Read from the base class property _executionData
            if (string.IsNullOrEmpty(_executionData)) return;

            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Analysis Result:", EditorStyles.boldLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));
        
            var style = new GUIStyle(EditorStyles.textArea) { richText = true, wordWrap = true };
        
            // BUG FIX: Display the actual data
            EditorGUILayout.TextArea(_executionData, style, GUILayout.ExpandHeight(true));
        
            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        public override void Execute()
        {
            if (!string.IsNullOrEmpty(_executionData))
            {
                GUIUtility.systemCopyBuffer = _executionData;
                Debug.Log("Copied to clipboard.");
            }
        }
    }
}