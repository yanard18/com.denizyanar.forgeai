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
        public override bool CanUndo => true;

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
            if (selectedPaths.Count == 0) return userInstruction;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("You are a Unity Asset Database expert. Output ONLY valid JSON.");
            sb.AppendLine("Reorganize these files based on the instruction:");
            foreach (var p in selectedPaths) sb.AppendLine(p);
            sb.AppendLine($"\nUSER INSTRUCTION: \"{userInstruction}\"\n");
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

            foreach (var op in _proposedOperations)
            {
                EnsureDirectoryExists(op.targetPath);
                string err = AssetDatabase.MoveAsset(op.sourcePath, op.targetPath);

                if (string.IsNullOrEmpty(err))
                {
                    successCount++;
                    _executionHistory.Push(new CompletedMove { OriginalSource = op.sourcePath, CurrentLocation = op.targetPath });
                }
                else errors.Add(err);
            }

            ExecutionResult = errors.Count > 0 
                ? $"Moved {successCount}/{_proposedOperations.Count} files. Errors: {string.Join(", ", errors)}"
                : $"Success! Moved all {successCount} files.";

            IsExecuted = true;
            IsUndone = false;
            AssetDatabase.Refresh();
        }

        public override void Undo()
        {
            if (!IsExecuted || IsUndone) return;
            int undoCount = 0;

            while (_executionHistory.Count > 0)
            {
                var move = _executionHistory.Pop();
                string originalDir = System.IO.Path.GetDirectoryName(move.OriginalSource);
                if (!System.IO.Directory.Exists(originalDir)) System.IO.Directory.CreateDirectory(originalDir);

                if (string.IsNullOrEmpty(AssetDatabase.MoveAsset(move.CurrentLocation, move.OriginalSource))) undoCount++;
            }

            // Cleanup empty folders we created
            foreach (var folder in _createdFolders.OrderByDescending(p => p.Length))
            {
                if (System.IO.Directory.Exists(folder) && System.IO.Directory.GetFileSystemEntries(folder).Length == 0)
                {
                    AssetDatabase.DeleteAsset(folder);
                }
            }

            IsUndone = true;
            IsExecuted = false;
            ExecutionResult = $"Undid {undoCount} moves.";
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