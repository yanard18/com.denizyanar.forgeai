using System.Collections.Generic;
using System.Linq; // For Where()
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
        public override bool CanUndo => true;

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
            if (selectedPaths.Count == 0) return userInstruction;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("You are a Naming Convention Expert. Output ONLY valid JSON.");
            sb.AppendLine("Rename these files based on the instruction:");
            foreach (var p in selectedPaths) sb.AppendLine(p);
            sb.AppendLine($"\nUSER INSTRUCTION: \"{userInstruction}\"\n");
            sb.AppendLine("JSON Format: { \"operations\": [ { \"originalPath\": \"Assets/tree.png\", \"newName\": \"T_Tree.png\" } ] }");
            sb.AppendLine("Rules: 1. Only include files that change. 2. Preserve extensions unless asked otherwise.");
            return sb.ToString();
        }

        // Override the hook to filter unchanged files
        protected override void PostProcessOperations()
        {
            _proposedOperations = _proposedOperations
                .Where(op => System.IO.Path.GetFileName(op.originalPath) != op.newName)
                .ToList();
        }

        protected override void DrawEmptyOrErrorState()
        {
             EditorGUILayout.HelpBox("No files require renaming based on this instruction.", MessageType.Info);
        }

        protected override void DrawOperationRow(FileRenameOperation op, int index)
        {
            // Use the shared library
            ForgeUI.DrawProposedPlanOperationRow(op.originalPath, op.newName);
        }

        public override void Execute()
        {
            if (IsExecuted) return;
            int successCount = 0;
            List<string> errors = new List<string>();
            _executionHistory.Clear();

            foreach (var op in _proposedOperations)
            {
                string oldName = System.IO.Path.GetFileName(op.originalPath);
                if (oldName == op.newName) continue;

                string err = AssetDatabase.RenameAsset(op.originalPath, op.newName);
                if (string.IsNullOrEmpty(err))
                {
                    successCount++;
                    _executionHistory.Push(new CompletedRename 
                    { 
                        DirectoryPath = System.IO.Path.GetDirectoryName(op.originalPath), 
                        OldName = oldName, 
                        NewName = op.newName 
                    });
                }
                else errors.Add(err);
            }

            ExecutionResult = errors.Count > 0 
                ? $"Renamed {successCount}/{_proposedOperations.Count} files. Errors: {string.Join(", ", errors)}" 
                : $"Success! Renamed all {successCount} files.";

            IsExecuted = true;
            IsUndone = false;
            AssetDatabase.SaveAssets();
        }

        public override void Undo()
        {
            if (!IsExecuted || IsUndone) return;
            int undoCount = 0;

            while (_executionHistory.Count > 0)
            {
                var rename = _executionHistory.Pop();
                string currentPath = System.IO.Path.Combine(rename.DirectoryPath, rename.NewName);
                if (string.IsNullOrEmpty(AssetDatabase.RenameAsset(currentPath, rename.OldName))) undoCount++;
            }

            IsUndone = true;
            IsExecuted = false;
            ExecutionResult = $"Undid {undoCount} renames.";
            AssetDatabase.SaveAssets();
        }
    }
}