using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ForgeAI
{
    public class OpenAIProvider : ILLMProvider
    {
        [Serializable]
        private class OpenAIRequest
        {
            public string model;
            public List<OpenAIMessage> messages;
            public float temperature;
            public int max_tokens;
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

        public async Task<string> SendRequestAsync(LLMRequest request)
        {
            var apiMessages = new List<OpenAIMessage>();
            foreach (var msg in request.History)
            {
                apiMessages.Add(new OpenAIMessage { role = msg.role, content = msg.content });
            }

            var requestData = new OpenAIRequest
            {
                model = request.Model,
                messages = apiMessages,
                temperature = request.Temperature,
                max_tokens = request.MaxTokens
            };

            string json = JsonUtility.ToJson(requestData);

            using (UnityWebRequest www = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Authorization", "Bearer " + request.ApiKey);

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
