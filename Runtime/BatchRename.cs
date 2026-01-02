using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DenizYanar.ForgeAI.Tasks
{
    public class BatchRenameTask : AITask
    {
        public override string DisplayName => "Batch Rename";
        public override bool CanUndo => true;

        [System.Serializable]
        private class FileRenameOperation
        {
            public string originalPath; 
            public string newName;      
        }

        [System.Serializable]
        private class AIResponseWrapper
        {
            public List<FileRenameOperation> operations;
        }

        private struct CompletedRename
        {
            public string DirectoryPath;
            public string OldName;
            public string NewName;
        }

        private List<FileRenameOperation> _proposedOperations = new();
        private Stack<CompletedRename> _executionHistory = new();
        private string _rawJsonForDebug;

        public override string GenerateFullPrompt(string userInstruction)
        {
            var selectedPaths = Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .ToList();

            if (selectedPaths.Count == 0)
            {
                Debug.LogWarning("BatchRenameTask: No files selected.");
                return userInstruction;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("You are a Unity Naming Convention Expert.");
            sb.AppendLine("Your task is to rename the following files based on the user's instruction.");
            sb.AppendLine("You must output ONLY valid JSON.");
            sb.AppendLine("\n--- SELECTED FILES ---");
            foreach (var p in selectedPaths) sb.AppendLine(p);
            sb.AppendLine("----------------------\n");
            sb.AppendLine($"USER INSTRUCTION: \"{userInstruction}\"\n");
            
            sb.AppendLine("--- RESPONSE FORMAT ---");
            sb.AppendLine("Return a JSON object with a single key 'operations' containing an array.");
            sb.AppendLine("The 'originalPath' must match the input exactly.");
            sb.AppendLine("The 'newName' must be ONLY the filename and extension (not the full path).");
            
            // --- CRITICAL UPDATES HERE ---
            sb.AppendLine("CRITICAL RULES:");
            sb.AppendLine("1. If a file's name DOES NOT need to change based on the instruction, DO NOT include it in the array.");
            sb.AppendLine("2. PRESERVE the original file extension unless explicitly told to change it.");
            // -----------------------------

            sb.AppendLine(
                "Example: { \"operations\": [ { \"originalPath\": \"Assets/Textures/tree_01.png\", \"newName\": \"T_Tree_01.png\" } ] }");

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
                    // Double-check optimization: 
                    // Remove any entries where the name didn't actually change, just in case AI ignored the prompt.
                    _proposedOperations = wrapper.operations
                        .Where(op => System.IO.Path.GetFileName(op.originalPath) != op.newName)
                        .ToList();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"JSON Parse Error: {e.Message}");
            }
        }

        public override void DrawUI()
        {
            // Case 1: Error or No Data
            if (_proposedOperations == null || _proposedOperations.Count == 0)
            {
                // Changed message to be clearer since we now filter unchanged files
                EditorGUILayout.HelpBox("No files require renaming based on this instruction.", MessageType.Info);
                return;
            }

            // Case 2: Proposed Plan (Before Execution)
            if (!IsExecuted && !IsUndone)
            {
                GUILayout.Label($"Proposed Renames ({_proposedOperations.Count} files)", EditorStyles.boldLabel);

                foreach (var op in _proposedOperations)
                {
                    GUILayout.BeginHorizontal(EditorStyles.helpBox);

                    Texture icon = AssetDatabase.GetCachedIcon(op.originalPath);
                    if (icon == null) icon = EditorGUIUtility.IconContent("d_Text Asset Icon").image;

                    GUILayout.Label(icon, GUILayout.Width(16), GUILayout.Height(16));
                    
                    string oldName = System.IO.Path.GetFileName(op.originalPath);
                    GUILayout.Label(oldName, GUILayout.Width(140));
                    
                    GUILayout.Label(EditorGUIUtility.IconContent("d_forward").image, GUILayout.Width(16), GUILayout.Height(16));
                    
                    var style = new GUIStyle(EditorStyles.miniLabel);
                    style.normal.textColor = new Color(0.7f, 1f, 0.7f);
                    GUILayout.Label(op.newName, style);
                    
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(5);

                var defaultColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
                if (GUILayout.Button(
                        new GUIContent(" Confirm Rename", EditorGUIUtility.IconContent("d_PlayButton").image),
                        GUILayout.Height(28)))
                {
                    Execute();
                }

                GUI.backgroundColor = defaultColor;
            }

            // Case 3: Execution Success
            else if (IsExecuted && !IsUndone)
            {
                GUILayout.BeginHorizontal(EditorStyles.helpBox);
                var successIcon = EditorGUIUtility.IconContent("d_winbtn_mac_max").image;
                GUILayout.Label(successIcon, GUILayout.Width(16), GUILayout.Height(16));

                GUILayout.BeginVertical();
                GUILayout.Label(ExecutionResult, new GUIStyle(EditorStyles.label) { wordWrap = true });
                GUILayout.EndVertical();

                GUILayout.Space(5);

                var defaultColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);

                if (GUILayout.Button(new GUIContent(" Undo", EditorGUIUtility.IconContent("d_RotateTool").image),
                        EditorStyles.miniButton, GUILayout.Width(65)))
                {
                    Undo();
                }
                GUI.backgroundColor = defaultColor;
                GUILayout.EndHorizontal();
            }

            // Case 4: Undone State
            else if (IsUndone)
            {
                GUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(EditorGUIUtility.IconContent("d_console.warnicon.sml").image, GUILayout.Width(16), GUILayout.Height(16));

                GUILayout.BeginVertical();
                GUILayout.Label("Renames Reverted.", new GUIStyle(EditorStyles.label) { wordWrap = true });
                GUILayout.EndVertical();
                
                GUILayout.Space(5);
                
                if (GUILayout.Button(new GUIContent(" Redo", EditorGUIUtility.IconContent("d_Refresh").image),
                        EditorStyles.miniButton, GUILayout.Width(65)))
                {
                    Execute();
                }
                GUILayout.EndHorizontal();
            }
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

                // FINAL SAFETY CHECK: Skip if names are identical (prevents Unity error)
                if (oldName == op.newName) continue;

                string err = AssetDatabase.RenameAsset(op.originalPath, op.newName);

                if (string.IsNullOrEmpty(err))
                {
                    successCount++;
                    
                    string directory = System.IO.Path.GetDirectoryName(op.originalPath);
                    
                    _executionHistory.Push(new CompletedRename
                    {
                        DirectoryPath = directory,
                        OldName = oldName, 
                        NewName = op.newName   
                    });
                }
                else
                {
                    errors.Add($"Failed to rename {oldName}: {err}");
                }
            }

            if (errors.Count > 0)
                ExecutionResult = $"Renamed {successCount}/{_proposedOperations.Count} files. Errors: {string.Join(", ", errors)}";
            else
                ExecutionResult = $"Success! Renamed all {successCount} files.";

            IsExecuted = true;
            IsUndone = false;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public override void Undo()
        {
            if (!IsExecuted || IsUndone || _executionHistory.Count == 0) return;

            int undoCount = 0;

            while (_executionHistory.Count > 0)
            {
                var rename = _executionHistory.Pop();
                string currentPath = System.IO.Path.Combine(rename.DirectoryPath, rename.NewName);
                
                string err = AssetDatabase.RenameAsset(currentPath, rename.OldName);

                if (string.IsNullOrEmpty(err)) undoCount++;
                else Debug.LogError($"Undo Failed for {currentPath}: {err}");
            }

            IsUndone = true;
            IsExecuted = false;
            ExecutionResult = $"Undid {undoCount} renames.";
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}