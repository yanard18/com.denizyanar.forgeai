using UnityEditor;
using UnityEngine;

namespace ForgeAI
{
    public static class ForgeUI
    {
        // Cache styles to avoid re-creating them every frame (Optimization)
        private static GUIStyle _miniLabelSuccess;
        private static GUIStyle _miniLabelNormal;

        public static void DrawProposedPlanOperationRow(string sourcePath, string targetLabel, Texture iconOverride = null)
        {
            // 1. Setup Container
            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            // 2. Icon (Cache lookup if not provided)
            if (iconOverride == null)
            {
                // Try to load asset to get icon, or fallback
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(sourcePath);
                if (asset != null) iconOverride = EditorGUIUtility.ObjectContent(asset, asset.GetType()).image;
                if (iconOverride == null) iconOverride = EditorGUIUtility.IconContent("d_GameObject Icon").image;
            }
            GUILayout.Label(iconOverride, GUILayout.Width(16), GUILayout.Height(16));

            // 3. Source Name
            GUILayout.Label(System.IO.Path.GetFileName(sourcePath), GUILayout.Width(140));

            // 4. Arrow
            GUILayout.Label(EditorGUIUtility.IconContent("d_forward").image, GUILayout.Width(16), GUILayout.Height(16));

            // 5. Target Label (Styled)
            if (_miniLabelSuccess == null)
            {
                _miniLabelSuccess = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.7f, 1f, 0.7f) } }; // Light Green
                _miniLabelNormal = new GUIStyle(EditorStyles.miniLabel);
            }

            // If target looks different than source (simple check), color it green
            bool isChange = System.IO.Path.GetFileName(sourcePath) != targetLabel && targetLabel != sourcePath;
            GUILayout.Label(targetLabel, isChange ? _miniLabelSuccess : _miniLabelNormal);

            GUILayout.EndHorizontal();
        }

        public static void DrawActionProposal(string toolName, string[] args, System.Action onExecute, System.Action onReject)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = new Color(0.9f, 0.95f, 1f);
            GUILayout.Label("Proposed Action", EditorStyles.boldLabel);
            GUI.backgroundColor = Color.white;
            
            GUILayout.Label($"Tool: {toolName}", EditorStyles.boldLabel);
            if (args != null && args.Length > 0)
            {
                GUILayout.Label($"Args: {string.Join(", ", args)}", EditorStyles.wordWrappedLabel);
            }

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("Approve & Execute", GUILayout.Height(25)))
            {
                onExecute?.Invoke();
            }
            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("Reject", GUILayout.Height(25)))
            {
                onReject?.Invoke();
            }
            GUI.backgroundColor = Color.white;
            
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}
