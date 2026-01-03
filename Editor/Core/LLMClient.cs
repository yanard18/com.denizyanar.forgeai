using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ForgeAI
{
    [Serializable]
    public class ChatMessage
    {
        public string role;
        public string content;
    }

    public static class LLMClient
    {
        [Serializable]
        private class ConversationLog
        {
            public List<ChatMessage> messages;
        }

        // Simple Factory for now. In a larger system, use Dependency Injection.
        private static ILLMProvider GetProvider(AIProvider providerType)
        {
            switch (providerType)
            {
                case AIProvider.OpenAI:
                    return new OpenAIProvider();
                // case AIProvider.Gemini: return new GeminiProvider();
                default:
                    return null;
            }
        }

        public static async Task<string> SendRequest(List<ChatMessage> conversationHistory)
        {
            var settings = ForgeAISettings.instance;
            var apiKey = settings.GetApiKey();

            // Log raw prompt
            try
            {
                string jsonHistory = JsonUtility.ToJson(new ConversationLog { messages = conversationHistory }, true);
                ForgeLogger.Log("RawPrompt", $"Sending to {settings.ModelName}", jsonHistory);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ForgeAI] Failed to log raw prompt: {e.Message}");
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                return "Error: API Key is missing. Please configure it in the settings.";
            }

            var provider = GetProvider(settings.provider);
            if (provider != null)
            {
                var request = new LLMRequest
                {
                    ApiKey = apiKey,
                    Model = settings.ModelName,
                    History = conversationHistory,
                    // Future: Add settings for Temp/MaxTokens here
                    Temperature = 0.7f,
                    MaxTokens = 4096 
                };

                string response = await provider.SendRequestAsync(request);
                ForgeLogger.Log("RawResponse", "Received from LLM", response);
                return response;
            }
            
            return $"Error: Implementation for {settings.provider} is pending or provider not found.";
        }
    }
}
