using System.Collections.Generic;
using System.Linq; // For OrderByDescending
using System.Text;
using DenizYanar.ForgeAI.Editor;
using UnityEditor;
using UnityEngine;

namespace DenizYanar.ForgeAI.Tasks
{
    [System.Serializable]
    public class FileMoveOperation
    {
        public string sourcePath;
        public string targetPath;
    }

    public class BatchMoveTask : AIOperationTask<FileMoveOperation>
    {
        public override string DisplayName => "Batch Move";
        
        // 1. Describe the tool for the Agent
        public override string ToolDescription => "Organizes project assets by moving them to new folders based on context or file type.";
        
        public override bool CanUndo => true;

        // 2. Local state tracking
        private bool _isUndone = false;

        private struct CompletedMove
        {
            public string OriginalSource;
            public string CurrentLocation;
        }

        private Stack<CompletedMove> _executionHistory = new();
        private HashSet<string> _createdFolders = new();

        public override string GenerateFullPrompt(string userInstruction)
        {
            var selectedPaths = GetSelectedAssetPaths();
            // If no files selected, we might rely on the instruction, but usually this needs selection.
            if (selectedPaths.Count == 0) return userInstruction;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("You are a Unity Asset Database expert. Output ONLY valid JSON.");
            sb.AppendLine("Reorganize these files based on the instruction:");
            foreach (var p in selectedPaths) sb.AppendLine(p);
            
            sb.AppendLine($"\nUSER INSTRUCTION: \"{userInstruction}\"\n");
            
            // Note: No need to explicitly check ContextFromPreviousSteps here usually, 
            // as this task is heavily reliant on current Selection, 
            // but you could add it if you wanted the Agent to pass file lists dynamically.
            
            sb.AppendLine("JSON Format: { \"operations\": [ { \"sourcePath\": \"Assets/A.mat\", \"targetPath\": \"Assets/Folder/A.mat\" } ] }");
            sb.AppendLine("Ensure target paths include filename/extension.");
            return sb.ToString();
        }

        protected override void DrawOperationRow(FileMoveOperation op, int index)
        {
            ForgeUI.DrawProposedPlanOperationRow(op.sourcePath, op.targetPath);
        }

        public override void Execute()
        {
            if (IsExecuted) return;
            
            int successCount = 0;
            List<string> errors = new List<string>();
            _executionHistory.Clear();
            _createdFolders.Clear();
            
            // 3. Build Data Log
            StringBuilder executionLog = new StringBuilder();

            foreach (var op in _proposedOperations)
            {
                EnsureDirectoryExists(op.targetPath);
                string err = AssetDatabase.MoveAsset(op.sourcePath, op.targetPath);

                if (string.IsNullOrEmpty(err))
                {
                    successCount++;
                    executionLog.AppendLine($"[MOVED] {op.sourcePath} -> {op.targetPath}");
                    
                    _executionHistory.Push(new CompletedMove 
                    { 
                        OriginalSource = op.sourcePath, 
                        CurrentLocation = op.targetPath 
                    });
                }
                else 
                {
                    errors.Add(err);
                    executionLog.AppendLine($"[ERROR] Failed to move {op.sourcePath}: {err}");
                }
            }

            // 4. Update Base Class Properties
            _executionData = executionLog.ToString();
            
            StatusMessage = errors.Count > 0 
                ? $"Moved {successCount}/{_proposedOperations.Count} files. Errors: {errors.Count}"
                : $"Success! Moved all {successCount} files.";

            IsExecuted = true;
            _isUndone = false;
            AssetDatabase.Refresh();
        }

        public override void Undo()
        {
            if (!IsExecuted || _isUndone) return;
            
            int undoCount = 0;
            StringBuilder undoLog = new StringBuilder();

            while (_executionHistory.Count > 0)
            {
                var move = _executionHistory.Pop();
                string originalDir = System.IO.Path.GetDirectoryName(move.OriginalSource);
                
                // Ensure original directory exists before moving back
                if (!System.IO.Directory.Exists(originalDir)) 
                {
                    System.IO.Directory.CreateDirectory(originalDir);
                }

                if (string.IsNullOrEmpty(AssetDatabase.MoveAsset(move.CurrentLocation, move.OriginalSource)))
                {
                    undoCount++;
                    undoLog.AppendLine($"[UNDO] Reverted {move.CurrentLocation} -> {move.OriginalSource}");
                }
            }

            // Cleanup empty folders we created
            foreach (var folder in _createdFolders.OrderByDescending(p => p.Length))
            {
                if (System.IO.Directory.Exists(folder) && System.IO.Directory.GetFileSystemEntries(folder).Length == 0)
                {
                    AssetDatabase.DeleteAsset(folder);
                    undoLog.AppendLine($"[CLEANUP] Removed empty folder: {folder}");
                }
            }

            _isUndone = true;
            IsExecuted = false;
            
            // 5. Update Log
            _executionData += "\n\n--- UNDO OPERATIONS ---\n" + undoLog.ToString();
            StatusMessage = $"Undid {undoCount} moves.";
            
            AssetDatabase.Refresh();
        }

        private void EnsureDirectoryExists(string assetPath)
        {
            string folder = System.IO.Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(folder) && !System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
                _createdFolders.Add(folder);
                AssetDatabase.Refresh();
            }
        }
    }
}