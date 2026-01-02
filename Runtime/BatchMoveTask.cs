using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DenizYanar.ForgeAI.Tasks
{
    public class BatchMoveTask : AITask
    {
        public override string DisplayName => "Batch Move / Organize Files";
        public override bool CanUndo => true;

        [System.Serializable]
        private class FileMoveOperation
        {
            public string sourcePath;
            public string targetPath;
        }

        [System.Serializable]
        private class AIResponseWrapper
        {
            public List<FileMoveOperation> operations;
        }

        private struct CompletedMove
        {
            public string OriginalSource;
            public string CurrentLocation;
        }

        private List<FileMoveOperation> _proposedOperations = new();
        private Stack<CompletedMove> _executionHistory = new();
        // Track folders created specifically by this task
        private HashSet<string> _createdFolders = new(); 
        private string _rawJsonForDebug;

        public override string GenerateFullPrompt(string userInstruction)
        {
            var selectedPaths = Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .ToList();

            if (selectedPaths.Count == 0)
            {
                Debug.LogWarning("BatchMoveTask: No files selected.");
                return userInstruction;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("You are a Unity Asset Database expert.");
            sb.AppendLine("Your task is to reorganize the following files based on the user's instruction.");
            sb.AppendLine("You must output ONLY valid JSON.");
            sb.AppendLine("\n--- SELECTED FILES ---");
            foreach (var p in selectedPaths) sb.AppendLine(p);
            sb.AppendLine("----------------------\n");
            sb.AppendLine($"USER INSTRUCTION: \"{userInstruction}\"\n");
            sb.AppendLine("--- RESPONSE FORMAT ---");
            sb.AppendLine("Return a JSON object with a single key 'operations' containing an array.");
            sb.AppendLine("Example: { \"operations\": [ { \"sourcePath\": \"Assets/A.mat\", \"targetPath\": \"Assets/Materials/A.mat\" } ] }");
            sb.AppendLine("Ensure target paths include the filename and extension.");

            return sb.ToString();
        }

        public override void ProcessResponse(string rawResponse)
        {
            _rawJsonForDebug = rawResponse;

            string cleanJson = rawResponse.Replace("```json", "").Replace("```", "").Trim();

            try
            {
                var wrapper = JsonUtility.FromJson<AIResponseWrapper>(cleanJson);
                if (wrapper != null && wrapper.operations != null)
                {
                    _proposedOperations = wrapper.operations;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"JSON Parse Error: {e.Message}");
            }
        }

        public override void DrawUI()
        {
            if (_proposedOperations == null || _proposedOperations.Count == 0)
            {
                EditorGUILayout.HelpBox("No valid move operations found in AI response.", MessageType.Warning);
                if (!string.IsNullOrEmpty(_rawJsonForDebug))
                    EditorGUILayout.LabelField("Raw:", _rawJsonForDebug);
                return;
            }

            if (!IsExecuted && !IsUndone)
            {
                EditorGUILayout.LabelField($"Proposed Moves ({_proposedOperations.Count}):", EditorStyles.boldLabel);

                foreach (var op in _proposedOperations)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    GUILayout.Label(System.IO.Path.GetFileName(op.sourcePath), GUILayout.Width(150));
                    GUILayout.Label("->");
                    GUILayout.Label(op.targetPath);
                    EditorGUILayout.EndHorizontal();
                }

                GUILayout.Space(5);
                if (GUILayout.Button("Confirm & Execute Move")) Execute();
            }
            else if (IsExecuted && !IsUndone)
            {
                EditorGUILayout.HelpBox(ExecutionResult, MessageType.Info);
                GUILayout.Space(5);
                
                var defaultColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
                if (GUILayout.Button("Undo Changes")) Undo();
                GUI.backgroundColor = defaultColor;
            }
            else if (IsUndone)
            {
                EditorGUILayout.HelpBox($"Reverted: {ExecutionResult}", MessageType.Warning);
                if (GUILayout.Button("Redo (Execute Again)")) Execute();
            }
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
                    _executionHistory.Push(new CompletedMove
                    {
                        OriginalSource = op.sourcePath,
                        CurrentLocation = op.targetPath
                    });
                }
                else
                {
                    errors.Add(err);
                }
            }

            if (errors.Count > 0)
                ExecutionResult = $"Moved {successCount}/{_proposedOperations.Count} files. Errors: {string.Join(", ", errors)}";
            else
                ExecutionResult = $"Success! Moved all {successCount} files.";

            IsExecuted = true;
            IsUndone = false;
            AssetDatabase.Refresh();
        }

        public override void Undo()
        {
            if (!IsExecuted || IsUndone || _executionHistory.Count == 0) return;

            int undoCount = 0;

            // 1. Revert Files
            while (_executionHistory.Count > 0)
            {
                var move = _executionHistory.Pop();

                // Ensure original folder still exists (in case user deleted it manually)
                string originalDir = System.IO.Path.GetDirectoryName(move.OriginalSource);
                if (!string.IsNullOrEmpty(originalDir) && !System.IO.Directory.Exists(originalDir))
                {
                     System.IO.Directory.CreateDirectory(originalDir);
                }

                string err = AssetDatabase.MoveAsset(move.CurrentLocation, move.OriginalSource);

                if (string.IsNullOrEmpty(err)) undoCount++;
                else Debug.LogError($"Undo Failed for {move.CurrentLocation}: {err}");
            }

            // 2. Clean up Created Folders (Deepest first)
            // We sort by length descending to ensure subfolders (longer paths) are deleted before their parents.
            var foldersToDelete = _createdFolders.OrderByDescending(path => path.Length).ToList();

            foreach (var folder in foldersToDelete)
            {
                // Only delete if we created it AND it is now empty
                if (System.IO.Directory.Exists(folder))
                {
                    var files = System.IO.Directory.GetFiles(folder);
                    var dirs = System.IO.Directory.GetDirectories(folder);

                    // If empty (ignoring .meta files which Unity handles)
                    if (files.Length == 0 && dirs.Length == 0)
                    {
                        AssetDatabase.DeleteAsset(folder);
                    }
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
                _createdFolders.Add(folder); // Track creation
                AssetDatabase.Refresh();
            }
        }
    }
}