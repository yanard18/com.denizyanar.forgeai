using UnityEngine;
using UnityEditor;

namespace ForgeAI
{
    public class ForgeAIWindow : EditorWindow
    {
        private string prompt = "";
        private Vector2 scrollPosition;
        private System.Collections.Generic.List<string> chatHistory = new System.Collections.Generic.List<string>();
        private bool isProcessing = false;

        [MenuItem("Window/ForgeAI Assistant")]
        public static void ShowWindow()
        {
            GetWindow<ForgeAIWindow>("ForgeAI");
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawChatArea();
            DrawInputArea();
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("ForgeAI", EditorStyles.boldLabel);
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Preferences", EditorStyles.toolbarButton))
            {
                SettingsService.OpenUserPreferences("Preferences/ForgeAI");
            }

            if (GUILayout.Button("Clear Chat", EditorStyles.toolbarButton))
            {
                chatHistory.Clear();
                conversation.Clear();
            }
            
            GUILayout.EndHorizontal();
        }

        private void DrawChatArea()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            
            if (chatHistory.Count == 0)
            {
                GUILayout.Label("Welcome to ForgeAI. Select a provider and enter your API Key to start.", EditorStyles.wordWrappedLabel);
            }
            else
            {
                foreach (var msg in chatHistory)
                {
                    GUILayout.BeginVertical(EditorStyles.helpBox);
                    GUILayout.Label(msg, EditorStyles.wordWrappedLabel);
                    GUILayout.EndVertical();
                    GUILayout.Space(5);
                }
            }
            
            GUILayout.EndScrollView();
        }

        private System.Collections.Generic.List<ChatMessage> conversation = new System.Collections.Generic.List<ChatMessage>();

        private void DrawInputArea()
        {
            GUI.enabled = !isProcessing;
            GUILayout.BeginHorizontal();
            prompt = EditorGUILayout.TextField(prompt, GUILayout.Height(40));
            if (GUILayout.Button("Send", GUILayout.Width(60), GUILayout.Height(40)))
            {
                if (!string.IsNullOrEmpty(prompt))
                {
                    ProcessUserRequest(prompt);
                    prompt = "";
                }
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            EditorGUILayout.Space(5);
        }

        private async void ProcessUserRequest(string userPrompt)
        {
            if (conversation.Count == 0)
            {
                conversation.Add(new ChatMessage { role = "system", content = ReActEngine.GetSystemPrompt() });
            }

            conversation.Add(new ChatMessage { role = "user", content = userPrompt });
            chatHistory.Add("User: " + userPrompt);
            
            isProcessing = true;
            Repaint();

            try
            {
                int maxSteps = 5;
                int step = 0;

                while (step < maxSteps)
                {
                    string response = await LLMClient.SendRequest(conversation);
                    
                    if (response.StartsWith("Error:"))
                    {
                        chatHistory.Add("System: " + response);
                        break;
                    }

                    conversation.Add(new ChatMessage { role = "assistant", content = response });
                    chatHistory.Add("AI: " + response);
                    
                    string jsonAction = ReActEngine.ExtractActionJson(response);
                    if (!string.IsNullOrEmpty(jsonAction))
                    {
                        chatHistory.Add("System: Executing tool...");
                        string observation = ReActEngine.ExecuteTool(jsonAction);
                        chatHistory.Add("Observation: " + observation);
                        
                        conversation.Add(new ChatMessage { role = "user", content = "Observation: " + observation });
                    }
                    else
                    {
                        break;
                    }
                    
                    step++;
                    Repaint();
                    await System.Threading.Tasks.Task.Yield();
                }
            }
            catch (System.Exception e)
            {
                chatHistory.Add("Critical Error: " + e.Message);
                Debug.LogException(e);
            }
            finally
            {
                isProcessing = false;
                Repaint();
            }
        }
    }
}
