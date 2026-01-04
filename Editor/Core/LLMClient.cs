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

        private static ILLMProvider GetProvider(AIProvider providerType)
        {
            switch (providerType)
            {
                case AIProvider.OpenAI: return new OpenAIProvider();
                default: return null;
            }
        }

        public static async Task<string> SendRequest(List<ChatMessage> conversationHistory)
        {
            var settings = ForgeAISettings.instance;
            var apiKey = settings.GetApiKey();

            if (string.IsNullOrEmpty(apiKey))
                return "Error: API Key is missing. Please configure it in the settings.";

            // Log Request (All prompts sent)
            try
            {
                // Pretty print JSON to ensure \n are readable
                string jsonHistory = JsonUtility.ToJson(new ConversationLog { messages = conversationHistory }, true);
                ForgeLogger.LogRaw("SENDING TO LLM", jsonHistory);
            }
            catch (Exception e) { Debug.LogWarning($"[ForgeAI] Log failed: {e.Message}"); }

            var provider = GetProvider(settings.provider);
            if (provider != null)
            {
                var request = new LLMRequest
                {
                    ApiKey = apiKey,
                    Model = settings.ModelName,
                    History = conversationHistory,
                    Temperature = 0.7f,
                    MaxTokens = 4096 
                };

                string response = await provider.SendRequestAsync(request);

                // Log Response (All prompt received)
                ForgeLogger.LogRaw("RECEIVED FROM LLM", response);
                
                return response;
            }
            
            return $"Error: Provider {settings.provider} not found.";
        }
    }
}
