using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DenizYanar.ForgeAI.Tasks
{
    public class BatchMoveTask : AITask
    {
        public override string DisplayName => "Batch Move / Organize Files";

        // Internal data structure for JSON parsing
        [System.Serializable]
        private class FileMoveOperation
        {
            public string sourcePath;
            public string targetPath; // Relative to Assets/
        }

        [System.Serializable]
        private class AIResponseWrapper
        {
            public List<FileMoveOperation> operations;
        }

        private List<FileMoveOperation> _proposedOperations = new();

        // The "History" for Undo (Only stores moves that actually succeeded)
        private struct CompletedMove
        {
            public string OriginalSource;
            public string CurrentLocation; // This was the 'target' during Execute
        }

        private Stack<CompletedMove> _executionHistory = new();

        private string _rawJsonForDebug;

        // ---------------------------------------------------------
        // 1. GENERATE PROMPT
        // ---------------------------------------------------------
        public override string GenerateFullPrompt(string userInstruction)
        {
            // A. Gather Context (The Window shouldn't do this)
            var selectedPaths = new List<string>();
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path)) selectedPaths.Add(path);
            }

            if (selectedPaths.Count == 0)
            {
                Debug.LogWarning("BatchMoveTask: No files selected.");
                return userInstruction; // Fallback
            }

            // B. Build the System Prompt
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("You are a Unity Asset Database expert.");
            sb.AppendLine("Your task is to reorganize the following files based on the user's instruction.");
            sb.AppendLine("You must output ONLY valid JSON.");
            sb.AppendLine("");
            sb.AppendLine("--- SELECTED FILES ---");
            foreach (var p in selectedPaths) sb.AppendLine(p);
            sb.AppendLine("----------------------");
            sb.AppendLine("");
            sb.AppendLine($"USER INSTRUCTION: \"{userInstruction}\"");
            sb.AppendLine("");
            sb.AppendLine("--- RESPONSE FORMAT ---");
            sb.AppendLine("Return a JSON object with a single key 'operations' containing an array.");
            sb.AppendLine("Example:");
            sb.AppendLine(
                "{ \"operations\": [ { \"sourcePath\": \"Assets/A.mat\", \"targetPath\": \"Assets/Materials/A.mat\" } ] }");
            sb.AppendLine("Ensure target paths include the filename and extension.");

            return sb.ToString();
        }

        // ---------------------------------------------------------
        // 2. PROCESS RESPONSE
        // ---------------------------------------------------------
        public override void ProcessResponse(string rawResponse)
        {
            _rawJsonForDebug = rawResponse;

            // Sanitize Markdown if the AI sends it (e.g. ```json ... ```)
            string cleanJson = rawResponse;
            if (cleanJson.Contains("```json"))
            {
                cleanJson = cleanJson.Split("```json")[1].Split("```")[0].Trim();
            }
            else if (cleanJson.Contains("```"))
            {
                cleanJson = cleanJson.Split("```")[1].Split("```")[0].Trim();
            }

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
            // Case 1: Error or Empty
            if (_proposedOperations == null || _proposedOperations.Count == 0)
            {
                EditorGUILayout.HelpBox("No valid move operations found in AI response.", MessageType.Warning);
                if (!string.IsNullOrEmpty(_rawJsonForDebug))
                    EditorGUILayout.LabelField("Raw:", _rawJsonForDebug);
                return;
            }

            // Case 2: Not Executed yet (Show Plan)
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
                if (GUILayout.Button("Confirm & Execute Move"))
                {
                    Execute();
                }
            }
            // Case 3: Executed (Show Success + Undo Button)
            else if (IsExecuted && !IsUndone)
            {
                // THIS IS THE PART YOU WERE MISSING
                EditorGUILayout.HelpBox(ExecutionResult, MessageType.Info);

                GUILayout.Space(5);
                GUI.backgroundColor = new Color(1f, 0.7f, 0.7f); // Reddish tint
                if (GUILayout.Button("Undo Changes"))
                {
                    Undo();
                }

                GUI.backgroundColor = Color.white;
            }
            // Case 4: Undone (Show Reverted status)
            else if (IsUndone)
            {
                EditorGUILayout.HelpBox($"Reverted: {ExecutionResult}", MessageType.Warning);
                if (GUILayout.Button("Redo (Execute Again)"))
                {
                    Execute();
                }
            }
        }

        public override void Undo()
        {
            if (!IsExecuted || IsUndone || _executionHistory.Count == 0) return;

            int undoCount = 0;

            // We pop from the stack to reverse the order (LIFO)
            while (_executionHistory.Count > 0)
            {
                var move = _executionHistory.Pop();

                // Swap Target and Source to move it back
                // Ensure directory exists just in case
                string originalFolder = System.IO.Path.GetDirectoryName(move.OriginalSource);
                if (!System.IO.Directory.Exists(originalFolder)) System.IO.Directory.CreateDirectory(originalFolder);

                string err = AssetDatabase.MoveAsset(move.CurrentLocation, move.OriginalSource);

                if (string.IsNullOrEmpty(err))
                {
                    undoCount++;
                }
                else
                {
                    Debug.LogError($"Undo Failed for {move.CurrentLocation}: {err}");
                }
            }

            IsUndone = true;
            IsExecuted = false;
            ExecutionResult = $"Undid {undoCount} moves.";
            AssetDatabase.Refresh();
        }


        // ---------------------------------------------------------
        // 4. EXECUTE LOGIC
        // ---------------------------------------------------------
        public override void Execute()
        {
            // Safety check
            if (IsExecuted) return;

            int successCount = 0;
            List<string> errors = new List<string>();

            // CRITICAL: Clear history before starting a new run
            _executionHistory.Clear();

            foreach (var op in _proposedOperations)
            {
                // 1. Ensure folder exists
                string folder = System.IO.Path.GetDirectoryName(op.targetPath);
                if (!System.IO.Directory.Exists(folder))
                {
                    System.IO.Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }

                // 2. Move Asset
                string err = AssetDatabase.MoveAsset(op.sourcePath, op.targetPath);

                if (string.IsNullOrEmpty(err))
                {
                    successCount++;

                    // --- THIS WAS MISSING ---
                    // We must record the move so Undo knows what to reverse
                    _executionHistory.Push(new CompletedMove
                    {
                        OriginalSource = op.sourcePath,
                        CurrentLocation = op.targetPath
                    });
                    // ------------------------
                }
                else
                {
                    errors.Add(err);
                }
            }

            if (errors.Count > 0)
            {
                ExecutionResult =
                    $"Moved {successCount}/{_proposedOperations.Count} files. Errors: {string.Join(", ", errors)}";
            }
            else
            {
                ExecutionResult = $"Success! Moved all {successCount} files.";
            }

            IsExecuted = true;
            IsUndone = false; // Reset undo flag
            AssetDatabase.Refresh();
        }
    }
}