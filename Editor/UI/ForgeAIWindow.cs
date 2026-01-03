using UnityEngine;
using UnityEditor;

namespace ForgeAI
{
    public class ForgeAIWindow : EditorWindow
    {
        private string prompt = "";
        private Vector2 scrollPosition;
        private bool isProcessing = false;
        private ForgeAgent agent;
        private string lastError = "";

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
                agent.OnHistoryChanged += Repaint; // Simple: Repaint whenever history changes
                agent.OnProcessingStateChanged += HandleProcessingState;
                agent.OnError += HandleError;
            }
        }

        private void OnDisable()
        {
            if (agent != null)
            {
                agent.OnHistoryChanged -= Repaint;
                agent.OnProcessingStateChanged -= HandleProcessingState;
                agent.OnError -= HandleError;
            }
        }

        private void HandleProcessingState(bool processing)
        {
            isProcessing = processing;
            Repaint();
        }

        private void HandleError(string error)
        {
            lastError = error;
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
                agent.ClearHistory();
                lastError = "";
            }
            
            GUILayout.EndHorizontal();
        }

        private void DrawChatArea()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            
            if (agent == null || agent.History.Count == 0)
            {
                GUILayout.Label("Welcome to ForgeAI. Select a provider and enter your API Key to start.", EditorStyles.wordWrappedLabel);
            }
            else
            {
                foreach (var msg in agent.History)
                {
                    // Filter out system messages from visual chat for cleaner UI
                    if (msg.role == "system") continue;

                    GUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    // Simple styling
                    if (msg.role == "user") 
                    {
                        GUILayout.Label($"<b>User:</b> {msg.content}", new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true });
                    }
                    else if (msg.role == "assistant")
                    {
                        GUILayout.Label($"<b>AI:</b> {msg.content}", new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true });
                    }
                    else
                    {
                        GUILayout.Label($"<i>{msg.role}: {msg.content}</i>", new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true });
                    }
                    
                    GUILayout.EndVertical();
                    GUILayout.Space(5);
                }
            }

            if (!string.IsNullOrEmpty(lastError))
            {
                 GUILayout.BeginVertical(EditorStyles.helpBox);
                 GUI.color = Color.red;
                 GUILayout.Label($"Error: {lastError}", EditorStyles.wordWrappedLabel);
                 GUI.color = Color.white;
                 GUILayout.EndVertical();
            }
            
            GUILayout.EndScrollView();
        }

        private void DrawInputArea()
        {
            GUI.enabled = !isProcessing;
            GUILayout.BeginHorizontal();
            
            // Check for Enter key
            Event e = Event.current;
            bool enterPressed = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            
            prompt = EditorGUILayout.TextField(prompt, GUILayout.Height(40));
            
            if (GUILayout.Button("Send", GUILayout.Width(60), GUILayout.Height(40)) || (enterPressed && !string.IsNullOrWhiteSpace(prompt)))
            {
                if (!string.IsNullOrEmpty(prompt))
                {
                    SendPrompt();
                    e.Use(); // Consume event to prevent double firing
                }
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            EditorGUILayout.Space(5);
        }

        private async void SendPrompt()
        {
            if (agent == null) OnEnable();
            
            string currentPrompt = prompt;
            prompt = "";
            lastError = ""; // Clear previous errors on new request
            
            ForgeLogger.StartNewSession();
            await agent.ChatAsync(currentPrompt);
        }
    }
}
