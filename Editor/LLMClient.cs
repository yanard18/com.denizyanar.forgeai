using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ForgeAI
{
    public class ChatMessage
    {
        public string role;
        public string content;
    }

    public static class LLMClient
    {
        [Serializable]
        private class OpenAIRequest
        {
            public string model;
            public List<OpenAIMessage> messages;
        }

        [Serializable]
        private class OpenAIMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private class OpenAIResponse
        {
            public List<OpenAIChoice> choices;
        }

        [Serializable]
        private class OpenAIChoice
        {
            public OpenAIMessage message;
        }

        public static async Task<string> SendRequest(List<ChatMessage> conversationHistory)
        {
            var settings = ForgeAISettings.instance;
            var apiKey = settings.GetApiKey();

            if (string.IsNullOrEmpty(apiKey))
            {
                return "Error: API Key is missing. Please configure it in the settings.";
            }

            if (settings.provider == AIProvider.OpenAI)
            {
                return await SendOpenAIRequest(apiKey, settings.ModelName, conversationHistory);
            }
            else
            {
                return "Error: Gemini implementation is pending.";
            }
        }

        private static async Task<string> SendOpenAIRequest(string apiKey, string model, List<ChatMessage> history)
        {
            var apiMessages = new List<OpenAIMessage>();
            foreach (var msg in history)
            {
                apiMessages.Add(new OpenAIMessage { role = msg.role, content = msg.content });
            }

            var requestData = new OpenAIRequest
            {
                model = model,
                messages = apiMessages
            };

            // Unity JsonUtility doesn't serialize Lists at root level well, or nested lists sometimes.
            // But here List<OpenAIMessage> is inside OpenAIRequest, which is fine.
            string json = JsonUtility.ToJson(requestData);

            using (UnityWebRequest www = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Authorization", "Bearer " + apiKey);

                var operation = www.SendWebRequest();

                while (!operation.isDone) await Task.Yield();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    return $"Error: {www.error} - {www.downloadHandler.text}";
                }

                var responseJson = www.downloadHandler.text;
                var responseObj = JsonUtility.FromJson<OpenAIResponse>(responseJson);
                
                if (responseObj != null && responseObj.choices != null && responseObj.choices.Count > 0)
                {
                    return responseObj.choices[0].message.content;
                }
                
                return "Error: Empty response from AI.";
            }
        }
    }
}
