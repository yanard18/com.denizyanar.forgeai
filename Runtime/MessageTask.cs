using UnityEditor;
using UnityEngine;

namespace DenizYanar.ForgeAI.Tasks
{
    public class MessageTask : AITask
    {
        public override string DisplayName => "Chat / Q&A";

        // We store the raw response here to display it
        private string _responseText;

        // ---------------------------------------------------------
        // 1. GENERATE PROMPT
        // ---------------------------------------------------------
        public override string GenerateFullPrompt(string userInstruction)
        {
            // For a basic chat, we might just pass the user instruction directly.
            // Or we can add a lightweight system context if we want it to act 
            // specifically as a Unity Expert.
            
            return $"You are a helpful Unity expert. Answer the following question clearly:\n\n{userInstruction}";
        }

        // ---------------------------------------------------------
        // 2. PROCESS RESPONSE
        // ---------------------------------------------------------
        public override void ProcessResponse(string rawResponse)
        {
            // No complex JSON parsing needed here. 
            // Just clean up the string if necessary.
            _responseText = rawResponse.Trim();
            
            // Mark as executed immediately since there's no secondary "Confirm" step for chat
            IsExecuted = true; 
            ExecutionResult = "Response Received.";
        }

        // ---------------------------------------------------------
        // 3. DRAW UI
        // ---------------------------------------------------------
        public override void DrawUI()
        {
            if (string.IsNullOrEmpty(_responseText))
            {
                return;
            }

            // Use a SelectableLabel so users can copy-paste code snippets from the answer
            // We calculate height dynamically to avoid scroll issues inside the history item
            float height = EditorStyles.textArea.CalcHeight(new GUIContent(_responseText), EditorGUIUtility.currentViewWidth - 50);
            
            EditorGUILayout.SelectableLabel(_responseText, EditorStyles.textArea, GUILayout.Height(height));
        }

        // ---------------------------------------------------------
        // 4. EXECUTE LOGIC
        // ---------------------------------------------------------
        public override void Execute()
        {
            // MessageTask typically doesn't have a secondary "Execute" action 
            // (like moving files), so this can be left empty or used to copy to clipboard.
            
            GUIUtility.systemCopyBuffer = _responseText;
            Debug.Log("Response copied to clipboard.");
        }
    }
}