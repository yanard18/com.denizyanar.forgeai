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
        private ForgeAgent agent;

        [MenuItem("Window/ForgeAI Assistant #&f")]
        public static void ShowWindow()
        {
            GetWindow<ForgeAIWindow>("ForgeAI");
        }

        private void OnEnable()
        {
            if (agent == null)
            {
                agent = new ForgeAgent();
                agent.OnMessageReceived += HandleMessage;
                agent.OnProcessingStateChanged += HandleProcessingState;
                agent.OnError += HandleError;
            }
        }

        private void OnDisable()
        {
            if (agent != null)
            {
                agent.OnMessageReceived -= HandleMessage;
                agent.OnProcessingStateChanged -= HandleProcessingState;
                agent.OnError -= HandleError;
            }
        }

        private void HandleMessage(string role, string content)
        {
            chatHistory.Add($"{role}: {content}");
            Repaint();
        }

        private void HandleProcessingState(bool processing)
        {
            isProcessing = processing;
            Repaint();
        }

        private void HandleError(string error)
        {
            chatHistory.Add($"Error: {error}");
            Repaint();
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
                agent.ClearHistory();
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

        private void DrawInputArea()
        {
            GUI.enabled = !isProcessing;
            GUILayout.BeginHorizontal();
            prompt = EditorGUILayout.TextField(prompt, GUILayout.Height(40));
            if (GUILayout.Button("Send", GUILayout.Width(60), GUILayout.Height(40)))
            {
                if (!string.IsNullOrEmpty(prompt))
                {
                    SendPrompt();
                }
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            EditorGUILayout.Space(5);
        }

        private async void SendPrompt()
        {
            if (agent == null) OnEnable(); // Safety check
            
            string currentPrompt = prompt;
            prompt = ""; // Clear input immediately
            
            ForgeLogger.StartNewSession();
            await agent.ChatAsync(currentPrompt);
        }
    }
}