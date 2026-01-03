using UnityEditor;
using UnityEngine;

namespace DenizYanar.ForgeAI.Editor
{
    public static class AIProjectPreferences
    {
        private const string PREFS_KEY_API = "YourName_AI_OpenAI_Key";

        public static string APIKey
        {
            get => EditorPrefs.GetString(PREFS_KEY_API, "");
            private set => EditorPrefs.SetString(PREFS_KEY_API, value);
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Preferences/AI Assistant", SettingsScope.User)
            {
                label = "AI Assistant",
                guiHandler = (searchContext) =>
                {
                    // 1. Fetch current value
                    string currentKey = APIKey;
                    
                    GUILayout.Space(10);
                    GUILayout.Label("Configuration", EditorStyles.boldLabel);
                    GUILayout.Space(5);

                    // 2. Draw the text field
                    // Using PasswordField so it shows dots (••••) instead of text
                    string newKey = EditorGUILayout.PasswordField("OpenAI API Key", currentKey);

                    // 3. Save if changed
                    if (newKey != currentKey)
                    {
                        APIKey = newKey;
                    }

                    GUILayout.Space(10);
                    EditorGUILayout.HelpBox("This key is stored locally on this machine via EditorPrefs. It will not be included in builds or version control.", MessageType.Info);
                    
                    if (GUILayout.Button("Test Key Visibility (Debug Log)"))
                    {
                        Debug.Log($"Current Key: {APIKey}");
                    }
                },

                // Keywords allow you to search for this menu in the search bar
                keywords = new System.Collections.Generic.HashSet<string>(new[] { "AI", "OpenAI", "GPT", "Key", "Token" })
            };

            return provider;
        }
    }
}