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
        [SerializeField] private string openAIKey;
        [SerializeField] private string geminiKey;
        
        public string ModelName = "gpt-4o"; // Default to a strong model

        public string GetApiKey()
        {
            return provider == AIProvider.OpenAI ? openAIKey : geminiKey;
        }

        public void SetApiKey(string key)
        {
            if (provider == AIProvider.OpenAI) openAIKey = key;
            else geminiKey = key;
        }

        public void Save()
        {
            Save(true);
        }
    }
}
