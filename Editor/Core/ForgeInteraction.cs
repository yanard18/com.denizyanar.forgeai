using System.Collections.Generic;

namespace ForgeAI
{
    [System.Serializable]
    public class ForgeInteraction
    {
        public string UserPrompt;
        public string Status; // "Thinking", "Waiting for Approval", "Completed", "Error", "Aborted"
        public string AIResponse; // The text reply
        
        // Multi-Action Support
        public List<ReActEngine.ToolAction> ProposedActions = new List<ReActEngine.ToolAction>();
        public List<string> ActionResults = new List<string>(); // Corresponds to ProposedActions
        
        public bool IsExpanded = true;
        public string ErrorMessage;

        public ForgeInteraction(string prompt)
        {
            UserPrompt = prompt;
            Status = "Thinking";
        }

        // Helper for single action backward compat or easy access
        public ReActEngine.ToolAction FirstAction => ProposedActions.Count > 0 ? ProposedActions[0] : null;
        public string CombinedResult => string.Join("\n", ActionResults);
    }
}