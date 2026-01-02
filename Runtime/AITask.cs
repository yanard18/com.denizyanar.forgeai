using UnityEngine;

namespace DenizYanar.ForgeAI.Tasks
{
    public abstract class AITask
    {
        // ... Existing Properties ...
        public abstract string DisplayName { get; }
        public string RawData { get; set; }
        
        // State Tracking
        public bool IsExecuted { get; protected set; }
        public bool IsUndone { get; protected set; }
        public string ExecutionResult { get; protected set; }

        // COMMAND PATTERN: Capabilities
        public virtual bool CanUndo => false; // Default to false (e.g. Chat)

        // ... Existing Methods (GenerateFullPrompt, ProcessResponse) ...
        public abstract string GenerateFullPrompt(string userInstruction);
        public abstract void ProcessResponse(string rawResponse);

        // COMMAND PATTERN: Actions
        public abstract void Execute();
        
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