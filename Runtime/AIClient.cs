using System.Collections.Generic;
using System.Text;
using DenizYanar.AITool;
using UnityEngine;
using UnityEngine.Networking;

namespace DenizYanar.ForgeAI
{
    public static class AIClient
    {
        private const string ApiUrl = "https://api.openai.com/v1/chat/completions";
        private const string ContentType = "application/json";
        private const string SystemInstruction = "You are a Unity helper. Respond with C# code only when asked.";

        public static async Awaitable<string> SendRequestAsync(string userPrompt, string apiKey)
        {
            var requestPayload = new ChatRequest
            {
                messages = new List<Message>
                {
                    new("system", SystemInstruction),
                    new("user", userPrompt)
                }
            };

            var json = JsonUtility.ToJson(requestPayload);
            var bodyRaw = Encoding.UTF8.GetBytes(json);

            using var webRequest = new UnityWebRequest(ApiUrl, "POST");

            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", ContentType);
            webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            var operation = webRequest.SendWebRequest();

            // Manual await loop for Awaitable compatibility
            while (!operation.isDone)
            {
                await Awaitable.NextFrameAsync();
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"AI Error: {webRequest.error}");
                return null;
            }

            var response = JsonUtility.FromJson<ChatResponse>(webRequest.downloadHandler.text);
            return response?.choices?[0].message.content;
        }
    }
}