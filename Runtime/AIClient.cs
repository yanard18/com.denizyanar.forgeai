using System.Text;
using DenizYanar.AITool;
using UnityEngine;
using UnityEngine.Networking;

namespace DenizYanar.ForgeAI
{
    public static class AIClient
    {
        private const string API_URL = "https://api.openai.com/v1/chat/completions";
        
        // Return type is Awaitable<string>, Unity 6's new standard
        public static async Awaitable<string> SendRequestAsync(string prompt, string apiKey)
        {
            // 1. Prepare Data
            var requestData = new ChatRequest();
            requestData.messages.Add(new Message("system", "You are a Unity helper. Respond with C# code only when asked."));
            requestData.messages.Add(new Message("user", prompt));

            string json = JsonUtility.ToJson(requestData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            // 2. Setup WebRequest
            using (UnityWebRequest webRequest = new UnityWebRequest(API_URL, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", "Bearer " + apiKey);

                // 3. The Unity 6 Magic: Await the AsyncOperation directly
                // Note: In Unity 6, we can await the SendWebRequest operation directly
                // or wrap it if strictly using the Awaitable pattern for older 2023 compatibility.
                // The cleanest Unity 6 native way:
                var operation = webRequest.SendWebRequest();
                
                while (!operation.isDone) 
                {
                    // This creates a non-blocking loop on the main thread
                    await Awaitable.NextFrameAsync();
                }

                // 4. Handle Result
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<ChatResponse>(webRequest.downloadHandler.text);
                    return response.choices[0].message.content;
                }
                else
                {
                    Debug.LogError($"AI Error: {webRequest.error}");
                    return null;
                }
            }
        }
    }
}