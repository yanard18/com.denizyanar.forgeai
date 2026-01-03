using System.Collections.Generic;
using System.Linq; 
using System.Text;
using DenizYanar.ForgeAI.Editor;
using UnityEditor;
using UnityEngine;

namespace DenizYanar.ForgeAI.Tasks
{
    [System.Serializable]
    public class FileRenameOperation
    {
        public string originalPath;
        public string newName;
    }

    public class BatchRenameTask : AIOperationTask<FileRenameOperation>
    {
        public override string DisplayName => "Batch Rename";
        public override string ToolDescription => "Renames assets based on conventions.";
        public override bool CanUndo => true;

        // NOTE: Removed private bool _isUndone (Base class handles it now)

        private struct CompletedRename
        {
            public string DirectoryPath;
            public string OldName;
            public string NewName;
        }

        private Stack<CompletedRename> _executionHistory = new();

        public override string GenerateFullPrompt(string userInstruction)
        {
            var selectedPaths = GetSelectedAssetPaths();
            
            // Generate prompt even if context is empty, but warn LLM
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("You are a Naming Convention Expert. Output ONLY valid JSON.");
            
            // 1. Add Selection
            if (selectedPaths.Count > 0)
            {
                sb.AppendLine("TARGET FILES:");
                foreach (var p in selectedPaths) sb.AppendLine(p);
            }

            // 2. Add Context (Base Class Helper)
            AppendContextIfAvailable(sb);

            sb.AppendLine($"\nUSER INSTRUCTION: \"{userInstruction}\"\n");
            sb.AppendLine("JSON Format: { \"operations\": [ { \"originalPath\": \"Assets/tree.png\", \"newName\": \"T_Tree.png\" } ] }");
            sb.AppendLine("Rules: 1. Only include files that change. 2. Preserve extensions.");

            return sb.ToString();
        }

        // Removes operations where the name didn't change
        protected override void PostProcessOperations()
        {
            if (_proposedOperations == null) return;

            _proposedOperations = _proposedOperations
                .Where(op => System.IO.Path.GetFileName(op.originalPath) != op.newName)
                .ToList();
        }

        protected override void DrawOperationRow(FileRenameOperation op, int index)
        {
            // Simple fallback drawing if ForgeUI is missing
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(op.originalPath, EditorStyles.miniLabel, GUILayout.Width(150));
            GUILayout.Label("->", GUILayout.Width(20));
            GUILayout.Label(op.newName, EditorStyles.boldLabel);
            GUILayout.EndHorizontal();
        }

        public override void Execute()
        {
            if (IsExecuted) return;
            
            int successCount = 0;
            List<string> errors = new List<string>();
            _executionHistory.Clear();
            StringBuilder executionLog = new StringBuilder();

            foreach (var op in _proposedOperations)
            {
                string oldName = System.IO.Path.GetFileName(op.originalPath);
                
                // Double check equality to prevent Unity errors
                if (oldName == op.newName) continue;

                string err = AssetDatabase.RenameAsset(op.originalPath, op.newName);
                if (string.IsNullOrEmpty(err))
                {
                    successCount++;
                    executionLog.AppendLine($"[RENAMED] {op.originalPath} -> {op.newName}");
                    
                    _executionHistory.Push(new CompletedRename 
                    { 
                        DirectoryPath = System.IO.Path.GetDirectoryName(op.originalPath), 
                        OldName = oldName, 
                        NewName = op.newName 
                    });
                }
                else 
                {
                    errors.Add(err);
                    executionLog.AppendLine($"[ERROR] {op.originalPath}: {err}");
                }
            }

            _executionData = executionLog.ToString();
            StatusMessage = errors.Count > 0 ? $"Done with {errors.Count} errors." : $"Renamed {successCount} files.";

            IsExecuted = true;
            _isUndone = false; // Reset base flag
            AssetDatabase.SaveAssets();
        }

        public override void Undo()
        {
            if (!IsExecuted || _isUndone) return;
            
            int undoCount = 0;
            StringBuilder undoLog = new StringBuilder();

            while (_executionHistory.Count > 0)
            {
                var rename = _executionHistory.Pop();
                string currentPath = System.IO.Path.Combine(rename.DirectoryPath, rename.NewName);
                
                if (string.IsNullOrEmpty(AssetDatabase.RenameAsset(currentPath, rename.OldName)))
                {
                    undoCount++;
                    undoLog.AppendLine($"[UNDO] Reverted {rename.NewName} -> {rename.OldName}");
                }
            }

            _isUndone = true; // Set base flag
            IsExecuted = false; // Allow re-execution
            
            _executionData += "\n[UNDO COMPLETED]";
            StatusMessage = $"Undid {undoCount} renames.";
            AssetDatabase.SaveAssets();
        }
    }
}