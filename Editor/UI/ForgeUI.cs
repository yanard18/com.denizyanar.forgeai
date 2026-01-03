using UnityEditor;
using UnityEngine;

namespace DenizYanar.ForgeAI.Editor
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
                iconOverride = AssetDatabase.GetCachedIcon(sourcePath);
                if (iconOverride == null) iconOverride = EditorGUIUtility.IconContent("d_GameObject Icon").image;
            }
            GUILayout.Label(iconOverride, GUILayout.Width(16), GUILayout.Height(16));

            // 3. Source Name (Truncate if too long could be added here later)
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

        // You can add more shared UI elements here later (e.g., DrawSuccessBox, DrawErrorBox)
    }
}