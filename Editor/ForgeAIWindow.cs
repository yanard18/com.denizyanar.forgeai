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
            GUILayout.Label("ForgeAI Settings", EditorStyles.boldLabel);
            
            ForgeAISettings.instance.provider = (AIProvider)EditorGUILayout.EnumPopup(ForgeAISettings.instance.provider, EditorStyles.toolbarPopup, GUILayout.Width(100));
            
            if (GUILayout.Button("Save Config", EditorStyles.toolbarButton))
            {
                ForgeAISettings.instance.Save();
            }
            
            if (GUILayout.Button("Clear Chat", EditorStyles.toolbarButton))
            {
                chatHistory.Clear();
                conversation.Clear();
            }
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // API Key field
            GUILayout.BeginHorizontal();
            GUILayout.Label("API Key:", GUILayout.Width(60));
            string currentKey = ForgeAISettings.instance.GetApiKey();
            string newKey = EditorGUILayout.PasswordField(currentKey);
            if (newKey != currentKey)
            {
                ForgeAISettings.instance.SetApiKey(newKey);
            }
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Model:", GUILayout.Width(60));
            ForgeAISettings.instance.ModelName = EditorGUILayout.TextField(ForgeAISettings.instance.ModelName);
            GUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
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
            Repaint(); // Update UI immediately

            int maxSteps = 5;
            int step = 0;

            while (step < maxSteps)
            {
                string response = await LLMClient.SendRequest(conversation);
                
                // Check for errors (simple check, robust app needs better error handling)
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
                    
                    // Feed observation back to AI
                    conversation.Add(new ChatMessage { role = "user", content = "Observation: " + observation });
                }
                else
                {
                    // No tool used, final answer
                    break;
                }
                
                step++;
                Repaint();
                // Small delay to prevent UI freeze if API is super fast (unlikely)
                await System.Threading.Tasks.Task.Yield();
            }

            isProcessing = false;
            Repaint();
        }
    }
}
