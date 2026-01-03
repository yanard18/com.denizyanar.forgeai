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

        private static readonly Dictionary<AIProvider, ILLMProvider> Providers = new Dictionary<AIProvider, ILLMProvider>
        {
            { AIProvider.OpenAI, new OpenAIProvider() }
            // Gemini provider will be added here once implemented
        };

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

            if (Providers.TryGetValue(settings.provider, out var provider))
            {
                string response = await provider.SendRequest(apiKey, settings.ModelName, conversationHistory);
                ForgeLogger.Log("RawResponse", "Received from LLM", response);
                return response;
            }
            
            return $"Error: Implementation for {settings.provider} is pending.";
        }
    }
}