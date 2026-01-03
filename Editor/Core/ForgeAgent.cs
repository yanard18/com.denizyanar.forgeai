using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ForgeAI
{
    /// <summary>
    /// The "Brain" of the AI. Manages conversation state and the ReAct loop.
    /// Decoupled from the UI (EditorWindow).
    /// </summary>
    public class ForgeAgent
    {
        private List<ChatMessage> conversation = new List<ChatMessage>();
        private const int MAX_STEPS = 5;

        public IReadOnlyList<ChatMessage> History => conversation;

        // Events for the View to subscribe to
        public event Action OnHistoryChanged; 
        public event Action<bool> OnProcessingStateChanged;
        public event Action<string> OnError;

        public ForgeAgent()
        {
            // Initialize with system prompt
            conversation.Add(new ChatMessage { role = "system", content = ReActEngine.GetSystemPrompt() });
        }

        public void ClearHistory()
        {
            conversation.Clear();
            conversation.Add(new ChatMessage { role = "system", content = ReActEngine.GetSystemPrompt() });
            OnHistoryChanged?.Invoke();
        }

        public async Task ChatAsync(string userPrompt)
        {
            if (string.IsNullOrWhiteSpace(userPrompt)) return;

            NotifyProcessing(true);

            try
            {
                // 1. Add User Message
                conversation.Add(new ChatMessage { role = "user", content = userPrompt });
                OnHistoryChanged?.Invoke();
                ForgeLogger.Log("User", userPrompt);

                // 2. Start ReAct Loop
                int step = 0;
                while (step < MAX_STEPS)
                {
                    // Get response from LLM
                    string response = await LLMClient.SendRequest(conversation);

                    if (response.StartsWith("Error:"))
                    {
                        OnError?.Invoke(response);
                        ForgeLogger.Log("Error", response);
                        break;
                    }

                    conversation.Add(new ChatMessage { role = "assistant", content = response });
                    OnHistoryChanged?.Invoke();
                    ForgeLogger.Log("AI", response);

                    // Check for Tool Action
                    var jsonAction = ReActEngine.ExtractActionJson(response);
                    if (!string.IsNullOrEmpty(jsonAction))
                    {
                        // Execute Tool
                        var observation = ReActEngine.ExecuteTool(jsonAction);
                        
                        // Feed observation back to LLM
                        ForgeLogger.Log("Observation", observation, "Action: " + jsonAction);
                        conversation.Add(new ChatMessage { role = "user", content = "Observation: " + observation });
                        OnHistoryChanged?.Invoke();
                    }
                    else
                    {
                        // No tool execution requested, we are done
                        break;
                    }

                    step++;
                    await Task.Yield(); // keep UI responsive
                }
            }
            catch (Exception e)
            {
                string msg = "Critical Error: " + e.Message;
                OnError?.Invoke(msg);
                ForgeLogger.Log("Exception", e.Message, e.StackTrace);
                Debug.LogException(e);
            }
            finally
            {
                NotifyProcessing(false);
            }
        }

        private void NotifyProcessing(bool isProcessing)
        {
            OnProcessingStateChanged?.Invoke(isProcessing);
        }
    }
}
