using UnityEditor;
using UnityEngine;

namespace ForgeAI
{
    public static class ForgeAIPreferences
    {
        [SettingsProvider]
        public static SettingsProvider CreateForgeAISettingsProvider()
        {
            var provider = new SettingsProvider("Preferences/ForgeAI", SettingsScope.User)
            {
                label = "ForgeAI",
                guiHandler = (searchContext) =>
                {
                    var settings = ForgeAISettings.instance;
                    bool changed = false;

                    GUILayout.Space(10);
                    GUILayout.Label("General Settings", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    
                    var newProvider = (AIProvider)EditorGUILayout.EnumPopup("AI Provider", settings.provider);
                    if (newProvider != settings.provider)
                    {
                        settings.provider = newProvider;
                        changed = true;
                    }

                    string currentKey = settings.GetApiKey();
                    string newKey = EditorGUILayout.PasswordField("API Key", currentKey);
                    if (newKey != currentKey)
                    {
                        settings.SetApiKey(newKey);
                        changed = true; // Key is saved immediately in SetApiKey, but we mark changed for consistency
                    }

                    string newModel = EditorGUILayout.TextField("Model Name", settings.ModelName);
                    if (newModel != settings.ModelName)
                    {
                        settings.ModelName = newModel;
                        changed = true;
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        if (changed) settings.Save();
                    }

                    GUILayout.Space(20);
                    GUILayout.Label("Info", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("ForgeAI allows you to control the Unity Editor using natural language. \n\nEnsure you have a valid API key for the selected provider.", MessageType.Info);
                },

                // Create the SettingsProvider and initialize its drawing (optional)
                keywords = new System.Collections.Generic.HashSet<string>(new[] { "Forge", "AI", "OpenAI", "Gemini", "GPT" })
            };

            return provider;
        }
    }
}
