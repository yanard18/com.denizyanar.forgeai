using UnityEngine;

namespace DenizYanar.ForgeAI.Tasks
{
    public abstract class AITask
    {
        
        public abstract string DisplayName { get; }
        public string RawData { get; set; }
        
        // Describe what this tool does for the Planner AI
        public virtual string ToolDescription => "Generic Task";
        
        // Context passed from previous steps
        public string ContextFromPreviousSteps { get; set; } = "";
        
        // State Tracking
        public bool IsExecuted { get; protected set; }
        public bool IsUndone { get; protected set; }
        public string ExecutionResult { get; protected set; }

        public virtual bool CanUndo => false; // Default to false (e.g. Chat)

        public abstract string GenerateFullPrompt(string userInstruction);
        public abstract void ProcessResponse(string rawResponse);

        public abstract void Execute();
        
        // Helper to expose execution results to the next step
        public virtual string GetExecutionResult() => "";
        
        public virtual void Undo() 
        {
            // Default no-op
            Debug.LogWarning("Undo not implemented for this task.");
        }

        // Shared UI for the "Undo" button
        public virtual void DrawUI()
        {
            // If the task logic has its own UI, it overrides this.
            // If it calls base.DrawUI(), we can draw the generic Undo button here,
            // or the specific task can draw it. 
        }
    }
}