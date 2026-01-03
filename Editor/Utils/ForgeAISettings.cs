using UnityEngine;
using UnityEditor;

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
    }
}
