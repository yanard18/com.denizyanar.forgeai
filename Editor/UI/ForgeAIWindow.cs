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
        private GUIStyle userPromptStyle;

        [MenuItem("Window/ForgeAI Assistant #&f")]
        public static void ShowWindow() => GetWindow<ForgeAIWindow>("ForgeAI");

        private void OnEnable()
        {
            if (agent == null)
            {
                agent = new ForgeAgent();
                agent.OnHistoryChanged += Repaint; 
                agent.OnProcessingStateChanged += HandleProcessingState;
                agent.OnError += HandleError;
                agent.OnActionProposed += Repaint;
            }
        }

        private void OnDisable()
        {
            if (agent != null)
            {
                agent.OnHistoryChanged -= Repaint;
                agent.OnProcessingStateChanged -= HandleProcessingState;
                agent.OnError -= HandleError;
                agent.OnActionProposed -= Repaint;
            }
        }

        private void InitStyles()
        {
            if (userPromptStyle == null)
            {
                userPromptStyle = new GUIStyle(EditorStyles.label)
                {
                    richText = true,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleLeft
                };
            }
        }

        private void HandleProcessingState(bool processing) { isProcessing = processing; Repaint(); }
        private void HandleError(string error) { lastError = error; Repaint(); }
        private void OnGUI() { InitStyles(); DrawToolbar(); DrawChatArea(); DrawInputArea(); }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("ForgeAI", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Preferences", EditorStyles.toolbarButton)) SettingsService.OpenUserPreferences("Preferences/ForgeAI");
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton)) agent.ClearHistory();
            GUILayout.EndHorizontal();
        }

        private void DrawChatArea()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            
            if (agent == null || agent.Interactions.Count == 0)
            {
                GUILayout.Label("Welcome to ForgeAI.", EditorStyles.wordWrappedLabel);
            }
            else
            {
                foreach (var interaction in agent.Interactions)
                {
                    DrawInteraction(interaction);
                }
            }

            if (!string.IsNullOrEmpty(lastError)) EditorGUILayout.HelpBox(lastError, MessageType.Error);
            GUILayout.EndScrollView();
        }

        private void DrawInteraction(ForgeInteraction interaction)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Header
            GUILayout.BeginHorizontal();
            var icon = interaction.IsExpanded ? EditorGUIUtility.IconContent("d_icon dropdown").image : EditorGUIUtility.IconContent("d_forward").image;
            if (GUILayout.Button(icon, EditorStyles.label, GUILayout.Width(16), GUILayout.Height(16))) interaction.IsExpanded = !interaction.IsExpanded;
            if (GUILayout.Button(new GUIContent($"<b>User:</b> {interaction.UserPrompt}", "Click to toggle"), userPromptStyle)) interaction.IsExpanded = !interaction.IsExpanded;
            GUILayout.FlexibleSpace();
            DrawStatus(interaction);
            GUILayout.EndHorizontal();

            // Body
            if (interaction.IsExpanded)
            {
                GUILayout.Space(5);
                
                // AI Response
                if (!string.IsNullOrEmpty(interaction.AIResponse))
                {
                    if (GUILayout.Button(new GUIContent(interaction.AIResponse, "Click to Copy"), EditorStyles.wordWrappedLabel))
                    {
                        EditorGUIUtility.systemCopyBuffer = interaction.AIResponse;
                        ShowNotification(new GUIContent("Copied to Clipboard"));
                    }
                }

                // Proposed Actions (Batch)
                if (interaction.ProposedActions.Count > 0 && interaction.Status == "Waiting for Approval")
                {
                    GUILayout.Space(10);
                    GUILayout.BeginVertical(EditorStyles.helpBox);
                    GUILayout.Label($"Proposed Plan ({interaction.ProposedActions.Count} actions)", EditorStyles.boldLabel);
                    
                    foreach(var action in interaction.ProposedActions)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("•", GUILayout.Width(10));
                        GUILayout.Label($"<b>{action.tool}</b>: {string.Join(", ", action.args)}", new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true });
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.Space(5);
                    GUILayout.BeginHorizontal();
                    GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                    if (GUILayout.Button("Approve Plan", GUILayout.Height(30))) ApproveAction();
                    GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                    if (GUILayout.Button("Reject Plan", GUILayout.Height(30))) RejectAction();
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                }

                // Results
                if (interaction.ActionResults.Count > 0)
                {
                    GUILayout.Space(5);
                    GUILayout.Label("Observations:", EditorStyles.miniBoldLabel);
                    foreach(var res in interaction.ActionResults)
                    {
                        GUILayout.Label($"• {res}", EditorStyles.wordWrappedMiniLabel);
                    }
                }
                
                if (!string.IsNullOrEmpty(interaction.ErrorMessage)) EditorGUILayout.HelpBox(interaction.ErrorMessage, MessageType.Error);
            }
            GUILayout.EndVertical();
        }

        private void DrawStatus(ForgeInteraction interaction)
        {
            string iconName = "d_WaitSpin00";
            if (interaction.Status == "Completed" || interaction.Status == "Action Executed") iconName = "TestPassed";
            else if (interaction.Status == "Error" || interaction.Status == "Action Rejected") iconName = "d_console.erroricon.sml";
            else if (interaction.Status == "Waiting for Approval") iconName = "d_DebuggerAttached";
            GUILayout.Label(new GUIContent(EditorGUIUtility.IconContent(iconName).image), GUILayout.Width(20));
        }

        private async void ApproveAction() => await agent.ApproveActionAsync();
        private async void RejectAction() => await agent.RejectActionAsync();

        private void DrawInputArea()
        {
            if (agent.CurrentInteraction != null && agent.CurrentInteraction.Status == "Waiting for Approval")
            {
                GUILayout.Label("Waiting for action approval...", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            GUI.enabled = !isProcessing;
            GUILayout.BeginHorizontal();
            Event e = Event.current;
            bool enterPressed = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            prompt = EditorGUILayout.TextField(prompt, GUILayout.Height(40));
            if (GUILayout.Button("Send", GUILayout.Width(60), GUILayout.Height(40)) || (enterPressed && !string.IsNullOrWhiteSpace(prompt)))
            {
                if (!string.IsNullOrEmpty(prompt)) { SendPrompt(); e.Use(); }
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.Space(5);
        }

        private async void SendPrompt()
        {
            if (agent == null) OnEnable();
            string currentPrompt = prompt;
            prompt = "";
            lastError = "";
            ForgeLogger.StartNewSession();
            await agent.ChatAsync(currentPrompt);
        }
    }
}