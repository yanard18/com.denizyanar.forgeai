using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DenizYanar.ForgeAI.Tasks
{
    /// <summary>
    /// A generic base class for AI tasks that result in a list of operations (Move, Rename, Git Command, etc).
    /// </summary>
    /// <typeparam name="TOperation">The serializable class representing a single operation.</typeparam>
    public abstract class AIOperationTask<TOperation> : AITask 
        where TOperation : class
    {
        // --- Shared Data Structures ---
        
        // Unity JsonUtility requires a wrapper to deserialize lists
        [System.Serializable]
        protected class AIResponseWrapper
        {
            public List<TOperation> operations;
        }

        protected List<TOperation> _proposedOperations = new();
        protected string _rawJsonForDebug;

        // --- Shared UI State ---

        public override void ProcessResponse(string rawResponse)
        {
            _rawJsonForDebug = rawResponse;
            string cleanJson = rawResponse.Replace("```json", "").Replace("```", "").Trim();

            try
            {
                // Deserialize into the generic wrapper
                var wrapper = JsonUtility.FromJson<AIResponseWrapper>(cleanJson);
                if (wrapper != null && wrapper.operations != null)
                {
                    _proposedOperations = wrapper.operations;
                    PostProcessOperations(); // Hook for filtering (e.g., Rename checks)
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[{DisplayName}] JSON Parse Error: {e.Message}");
            }
        }

        /// <summary>
        /// Optional hook to filter or modify operations after parsing (e.g., removing unchanged filenames).
        /// </summary>
        protected virtual void PostProcessOperations() { }

        public override void DrawUI()
        {
            // State 1: No Data / Error
            if (_proposedOperations == null || _proposedOperations.Count == 0)
            {
                DrawEmptyOrErrorState();
                return;
            }

            // State 2: Proposed Plan (Waiting for Confirmation)
            if (!IsExecuted && !IsUndone)
            {
                GUILayout.Label($"Proposed Plan ({_proposedOperations.Count} items)", EditorStyles.boldLabel);
                
                // Draw the list of operations
                DrawOperationList();

                GUILayout.Space(5);
                DrawConfirmButton();
            }
            // State 3: Executed (Success/Fail)
            else if (IsExecuted && !IsUndone)
            {
                DrawExecutionResultUI();
            }
            // State 4: Undone
            else if (IsUndone)
            {
                DrawUndoneUI();
            }
        }

        protected virtual void DrawEmptyOrErrorState()
        {
            EditorGUILayout.HelpBox("No valid operations found.", MessageType.Warning);
        }

        protected virtual void DrawOperationList()
        {
            // Scroll view handling could go here if lists get long
            for (int i = 0; i < _proposedOperations.Count; i++)
            {
                DrawOperationRow(_proposedOperations[i], i);
            }
        }

        // Abstract: How does a specific task draw one row? (e.g., "A -> B" vs "Git Command")
        protected abstract void DrawOperationRow(TOperation op, int index);

        protected virtual void DrawConfirmButton()
        {
            var defaultColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            if (GUILayout.Button(new GUIContent(" Confirm & Execute", EditorGUIUtility.IconContent("d_PlayButton").image), GUILayout.Height(28)))
            {
                Execute();
            }
            GUI.backgroundColor = defaultColor;
        }

        protected virtual void DrawExecutionResultUI()
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(EditorGUIUtility.IconContent("d_winbtn_mac_max").image, GUILayout.Width(16), GUILayout.Height(16));

            GUILayout.BeginVertical();
            GUILayout.Label(ExecutionResult, new GUIStyle(EditorStyles.label) { wordWrap = true });
            GUILayout.EndVertical();

            if (CanUndo)
            {
                GUILayout.Space(5);
                var defaultColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button(new GUIContent(" Undo", EditorGUIUtility.IconContent("d_RotateTool").image), EditorStyles.miniButton, GUILayout.Width(65)))
                {
                    Undo();
                }
                GUI.backgroundColor = defaultColor;
            }
            GUILayout.EndHorizontal();
        }

        protected virtual void DrawUndoneUI()
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(EditorGUIUtility.IconContent("d_console.warnicon.sml").image, GUILayout.Width(16), GUILayout.Height(16));
            GUILayout.Label("Operations Reverted.", new GUIStyle(EditorStyles.label) { wordWrap = true });
            
            GUILayout.Space(5);
            if (GUILayout.Button(new GUIContent(" Redo", EditorGUIUtility.IconContent("d_Refresh").image), EditorStyles.miniButton, GUILayout.Width(65)))
            {
                Execute();
            }
            GUILayout.EndHorizontal();
        }

        // --- Helper Methods ---
        
        protected List<string> GetSelectedAssetPaths()
        {
            var paths = new List<string>();
            foreach(var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
            }
            return paths;
        }
    }
}