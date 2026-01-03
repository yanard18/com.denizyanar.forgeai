using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ForgeAI
{
    public class ChatMessage
    {
        public string role;
        public string content;
    }

    public static class LLMClient
    {
        private static readonly Dictionary<AIProvider, ILLMProvider> Providers = new Dictionary<AIProvider, ILLMProvider>
        {
            { AIProvider.OpenAI, new OpenAIProvider() }
            // Gemini provider will be added here once implemented
        };

        public static async Task<string> SendRequest(List<ChatMessage> conversationHistory)
        {
            var settings = ForgeAISettings.instance;
            var apiKey = settings.GetApiKey();

            if (string.IsNullOrEmpty(apiKey))
            {
                return "Error: API Key is missing. Please configure it in the settings.";
            }

            if (Providers.TryGetValue(settings.provider, out var provider))
            {
                return await provider.SendRequest(apiKey, settings.ModelName, conversationHistory);
            }
            
            return $"Error: Implementation for {settings.provider} is pending.";
        }
    }
}