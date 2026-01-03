using System.Text;
using UnityEngine;

namespace DenizYanar.ForgeAI.Tasks
{
    public abstract class AITask
    {
        public abstract string DisplayName { get; }
        public virtual string ToolDescription => "Generic Task";

        // ----------------------------------------------------------------
        // 1. DATA PIPELINE (Common for all tasks)
        // ----------------------------------------------------------------
        
        // INPUT: Context passed from previous steps (The Orchestrator sets this)
        public string ContextFromPreviousSteps { get; set; } = "";

        // OUTPUT: The raw data produced by this task (Logs, Code, Text)
        protected string _executionData = "";

        // ----------------------------------------------------------------
        // 2. STATE TRACKING
        // ----------------------------------------------------------------
        public bool IsExecuted { get; protected set; }
        public string StatusMessage { get; protected set; } // Renamed from ExecutionResult for clarity (e.g. "Done", "Error")
        public virtual bool CanUndo => false;

        // ----------------------------------------------------------------
        // 3. METHODS
        // ----------------------------------------------------------------
        public abstract string GenerateFullPrompt(string userInstruction);
        public abstract void ProcessResponse(string rawResponse);
        public abstract void Execute();

        /// <summary>
        /// Standardized method to retrieve the data produced by this task.
        /// Derived classes should write to _executionData, not override this.
        /// </summary>
        public virtual string GetExecutionResult()
        {
            if (string.IsNullOrEmpty(_executionData))
            {
                return $"[{DisplayName}] executed, but produced no data.";
            }
            return _executionData;
        }
        
        /// <summary>
        /// Helper to append context to the prompt in a standardized format.
        /// Returns true if context was added.
        /// </summary>
        protected bool AppendContextIfAvailable(StringBuilder sb)
        {
            if (string.IsNullOrEmpty(ContextFromPreviousSteps)) return false;

            sb.AppendLine("\n============== CONTEXT / INPUT DATA ==============");
            sb.AppendLine("The following data was passed from a previous step. Use it if relevant:");
            sb.AppendLine(ContextFromPreviousSteps);
            sb.AppendLine("==================================================\n");
            return true;
        }

        public virtual void Undo() 
        {
            Debug.LogWarning($"Undo not implemented for {DisplayName}");
        }

        public virtual void DrawUI() { }
    }
}