using System.Collections.Generic;

namespace ForgeAI
{
    [System.Serializable]
    public class ForgeInteraction
    {
        public string UserPrompt;
        public string Status; // "Thinking", "Waiting for Approval", "Completed", "Error", "Aborted"
        public string AIResponse; // The text reply
        public ReActEngine.ToolAction ProposedAction;
        public string ActionResult; // Result of the tool
        public bool IsExpanded = true;
        
        // Error handling
        public string ErrorMessage;

        public ForgeInteraction(string prompt)
        {
            UserPrompt = prompt;
            Status = "Thinking";
        }
    }
}
