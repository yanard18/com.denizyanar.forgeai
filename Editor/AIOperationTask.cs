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
        
        // MOVED HERE: Common state tracking for all operation tasks
        protected bool _isUndone = false; 

        // --- JSON Parsing Logic ---

        public override void ProcessResponse(string rawResponse)
        {
            string cleanJson = ExtractJson(rawResponse);

            try
            {
                var wrapper = JsonUtility.FromJson<AIResponseWrapper>(cleanJson);
                if (wrapper != null && wrapper.operations != null)
                {
                    _proposedOperations = wrapper.operations;
                    PostProcessOperations(); // Hook for filtering
                    
                    // Mark as "Received Response" but not yet "Executed"
                    StatusMessage = $"Plan generated. {_proposedOperations.Count} operations proposed.";
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[{DisplayName}] JSON Parse Error: {e.Message}\nRaw: {cleanJson}");
                StatusMessage = "Failed to parse AI response.";
            }
        }

        /// <summary>
        /// Helper to robustly strip Markdown and extract the JSON object.
        /// </summary>
        private string ExtractJson(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string clean = raw.Replace("```json", "").Replace("```", "").Trim();
            int firstBrace = clean.IndexOf('{');
            int lastBrace = clean.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                clean = clean.Substring(firstBrace, lastBrace - firstBrace + 1);
            }
            return clean;
        }

        /// <summary>
        /// Optional hook to filter or modify operations after parsing.
        /// </summary>
        protected virtual void PostProcessOperations() { }

        // --- Shared UI Logic ---

        public override void DrawUI()
        {
            // State 1: No Data / Error
            if (_proposedOperations == null || _proposedOperations.Count == 0)
            {
                DrawEmptyOrErrorState();
                return;
            }

            // State 2: Proposed Plan (Waiting for Confirmation)
            if (!IsExecuted && !_isUndone)
            {
                GUILayout.Label($"Proposed Plan ({_proposedOperations.Count} items)", EditorStyles.boldLabel);
                DrawOperationList();
                GUILayout.Space(5);
                DrawConfirmButton();
            }
            // State 3: Executed (Success/Fail)
            else if (IsExecuted && !_isUndone)
            {
                DrawExecutionResultUI();
            }
            // State 4: Undone
            else if (_isUndone)
            {
                DrawUndoneUI();
            }
        }

        protected virtual void DrawEmptyOrErrorState()
        {
            // Only show warning if we actually tried to parse something
            if (!string.IsNullOrEmpty(StatusMessage))
                EditorGUILayout.HelpBox(StatusMessage, MessageType.Warning);
        }

        protected virtual void DrawOperationList()
        {
            // Optional: Add ScrollView if list > 10 items
            for (int i = 0; i < _proposedOperations.Count; i++)
            {
                DrawOperationRow(_proposedOperations[i], i);
            }
        }

        protected abstract void DrawOperationRow(TOperation op, int index);

        protected virtual void DrawConfirmButton()
        {
            var defaultColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            
            if (GUILayout.Button(new GUIContent(" Confirm & Execute", EditorGUIUtility.IconContent("d_PlayButton").image), GUILayout.Height(28)))
            {
                Execute();
                // Ensure Undone flag is reset when we execute
                _isUndone = false; 
            }
            
            GUI.backgroundColor = defaultColor;
        }

        protected virtual void DrawExecutionResultUI()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Draw Status Message (Success/Error)
            GUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("d_winbtn_mac_max").image, GUILayout.Width(16), GUILayout.Height(16));
            GUILayout.Label(StatusMessage, new GUIStyle(EditorStyles.label) { wordWrap = true });
            GUILayout.EndHorizontal();

            // Draw Undo Button
            if (CanUndo)
            {
                GUILayout.Space(5);
                var defaultColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button(new GUIContent(" Undo Operations", EditorGUIUtility.IconContent("d_RotateTool").image), GUILayout.Height(24)))
                {
                    Undo();
                    _isUndone = true; // Set flag for UI
                }
                GUI.backgroundColor = defaultColor;
            }
            
            // Draw Data Log (Optional - show what was actually done)
            if (!string.IsNullOrEmpty(_executionData))
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField("Log:", EditorStyles.miniLabel);
                EditorGUILayout.TextArea(_executionData, EditorStyles.textArea);
            }
            
            GUILayout.EndVertical();
        }

        protected virtual void DrawUndoneUI()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("d_console.warnicon.sml").image, GUILayout.Width(16), GUILayout.Height(16));
            GUILayout.Label(StatusMessage, new GUIStyle(EditorStyles.label) { wordWrap = true }); // e.g., "Undid 5 moves"
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            // Allow Redo
            if (GUILayout.Button(new GUIContent(" Redo (Execute Again)", EditorGUIUtility.IconContent("d_Refresh").image), GUILayout.Height(24)))
            {
                Execute();
                _isUndone = false;
            }
            GUILayout.EndVertical();
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