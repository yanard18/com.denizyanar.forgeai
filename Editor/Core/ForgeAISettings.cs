using UnityEditor;
using UnityEngine;

namespace ForgeAI
{
    public enum AIProvider
    {
        OpenAI,
        Gemini
    }

    [FilePath("ProjectSettings/ForgeAISettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class ForgeAISettings : ScriptableSingleton<ForgeAISettings>
    {
        public AIProvider provider = AIProvider.OpenAI;
        public string ModelName = "gpt-4o";
        public bool EnableLogging = true;

        private const string OpenAIKeyPref = "ForgeAI_OpenAIKey";
        private const string GeminiKeyPref = "ForgeAI_GeminiKey";

        public string GetApiKey()
        {
            return provider == AIProvider.OpenAI 
                ? EditorPrefs.GetString(OpenAIKeyPref, "") 
                : EditorPrefs.GetString(GeminiKeyPref, "");
        }

        public void SetApiKey(string key)
        {
            if (provider == AIProvider.OpenAI) EditorPrefs.SetString(OpenAIKeyPref, key);
            else EditorPrefs.SetString(GeminiKeyPref, key);
        }

        public void Save()
        {
            Save(true);
        }

        // -----------------------------------------------------------------------
        // SETTINGS PROVIDER (UI)
        // -----------------------------------------------------------------------

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

                    bool newLogging = EditorGUILayout.Toggle("Enable Logging", settings.EnableLogging);
                    if (newLogging != settings.EnableLogging)
                    {
                        settings.EnableLogging = newLogging;
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

                keywords = new System.Collections.Generic.HashSet<string>(new[] { "Forge", "AI", "OpenAI", "Gemini", "GPT" })
            };

            return provider;
        }
    }
}
