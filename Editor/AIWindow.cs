using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using DenizYanar.ForgeAI.Editor;

namespace DenizYanar.ForgeAI.Tasks
{
    public class AIWindow : EditorWindow
    {
        private string _currentPrompt = "";
        private Vector2 _scrollPosition;
        private bool _shouldAutoScroll = false;

        // Task Selection
        private int _selectedTaskIndex = 0;
        private List<AITask> _availableTaskTemplates;
        private string[] _taskDisplayNames;

        private readonly List<AIInteraction> _interactions = new();

        [MenuItem("Tools/Forge AI Agent")]
        public static void ShowWindow() => GetWindow<AIWindow>("AI Agent");

        private void OnEnable()
        {
            // Initialize available tasks
            _availableTaskTemplates = new List<AITask>
            {
                new MessageTask(),
                new BatchMoveTask(),
                new BatchRenameTask(),
                new GitOperationTask(),
            };

            _taskDisplayNames = _availableTaskTemplates.Select(x => x.DisplayName).ToArray();
        }

        private void OnGUI()
        {
            // Simple Header with Padding
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Forge AI Assistant", EditorStyles.boldLabel);
            GUILayout.EndVertical();

            GUILayout.Space(5);

            DrawInputArea();

            GUILayout.Space(5);

            DrawTaskHistory();
        }

        private void DrawInputArea()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);

            // Row 1: Task Selection
            GUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("d_SettingsIcon"), GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label("Task Mode:", GUILayout.Width(70));
            _selectedTaskIndex = EditorGUILayout.Popup(_selectedTaskIndex, _taskDisplayNames);
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Row 2: Prompt Area
            GUILayout.Label("Instruction:");
            GUI.SetNextControlName("PromptInput");
            _currentPrompt = EditorGUILayout.TextArea(_currentPrompt, GUILayout.Height(60));

            GUILayout.Space(5);

            // Row 3: Buttons
            GUILayout.BeginHorizontal();

            // Highlight the Execute button
            var defaultColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f); // Soft Blue

            if (GUILayout.Button(new GUIContent(" Execute Request", EditorGUIUtility.IconContent("d_PlayButton").image),
                    GUILayout.Height(30)))
            {
                if (!string.IsNullOrWhiteSpace(_currentPrompt))
                {
                    PerformTask(_currentPrompt);
                    _currentPrompt = "";
                    GUI.FocusControl(null); // Deselect text area
                }
            }

            GUI.backgroundColor = defaultColor; // Reset color

            if (GUILayout.Button(new GUIContent(" Clear", EditorGUIUtility.IconContent("d_TreeEditor.Trash").image),
                    GUILayout.Height(30), GUILayout.Width(80)))
            {
                _interactions.Clear();
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            GUILayout.EndVertical();
        }

        private async void PerformTask(string userPrompt)
        {
            var storedKey = AIProjectPreferences.APIKey;
            if (string.IsNullOrEmpty(storedKey))
            {
                Debug.LogError("No API Key found.");
                return;
            }

            var template = _availableTaskTemplates[_selectedTaskIndex];
            var activeTask = (AITask)System.Activator.CreateInstance(template.GetType());

            string fullPromptToSend = activeTask.GenerateFullPrompt(userPrompt);

            var newInteraction = new AIInteraction
            {
                UserPrompt = userPrompt,
                Status = "Thinking...",
                ActiveTask = activeTask
            };

            _interactions.Add(newInteraction);
            _shouldAutoScroll = true; // Trigger scroll to bottom

            var rawResponse = await AIClient.SendRequestAsync(fullPromptToSend, storedKey);

            if (string.IsNullOrEmpty(rawResponse))
            {
                newInteraction.Status = "Error";
                newInteraction.ErrorMessage = "AI returned no data.";
            }
            else
            {
                activeTask.ProcessResponse(rawResponse);
                newInteraction.Status = "Ready";
                newInteraction.IsCompleted = true;
            }

            Repaint();
        }

        private void DrawTaskHistory()
        {
            GUILayout.Label("Interaction History:", EditorStyles.boldLabel);

            using var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPosition);
            _scrollPosition = scrollView.scrollPosition;

            for (int i = 0; i < _interactions.Count; i++)
            {
                DrawInteractionItem(_interactions[i]);
            }

            // Auto-scroll logic
            if (_shouldAutoScroll && Event.current.type == EventType.Repaint)
            {
                _scrollPosition.y = float.MaxValue;
                _shouldAutoScroll = false;
                Repaint();
            }
        }

        private void DrawInteractionItem(AIInteraction interaction)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(4);

            // --- HEADER ROW (User + Status) ---
            GUILayout.BeginHorizontal();

            // 1. User Icon (Fixed Size)
            var userIcon = EditorGUIUtility.IconContent("d_FilterByLabel").image;
            GUILayout.Label(userIcon, GUILayout.Width(16), GUILayout.Height(16));

            // 2. Clickable User Prompt (Flexible Width + Word Wrap)
            // We create a custom style based on the default label but enforce wrapping
            var promptStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft // Ensure text starts from the left
            };

            // We use a Button that looks like a Label.
            // This handles the "Click" detection automatically.
            // We remove the default button padding/background by using the label style.
            GUIContent content = new GUIContent($"<b>User:</b> {interaction.UserPrompt}",
                "Click to copy prompt to clipboard");

            if (GUILayout.Button(content, promptStyle))
            {
                GUIUtility.systemCopyBuffer = interaction.UserPrompt;
                ShowNotification(new GUIContent("Prompt Copied!")); // Shows a temporary toast message in the window
            }

            // 3. Status Section (Right Aligned)
            // We allow the prompt to take up all available space, pushing the status to the right.
            // If the prompt is long, it will wrap, and the status will stay on the right.
            GUILayout.FlexibleSpace();

            // Determine Status Color & Icon
            Color statusColor;
            string iconName;

            if (!string.IsNullOrEmpty(interaction.ErrorMessage))
            {
                statusColor = new Color(1f, 0.4f, 0.4f); // Red
                iconName = "d_console.erroricon.sml";
            }
            else if (interaction.IsCompleted)
            {
                statusColor = new Color(0.4f, 1f, 0.4f); // Green
                iconName = "d_winbtn_mac_max";
            }
            else
            {
                statusColor = new Color(1f, 0.9f, 0.4f); // Yellow
                iconName = "d_WaitSpin00";
            }

            // Draw Status
            var originalContentColor = GUI.contentColor;
            GUI.contentColor = statusColor;

            // Using a Layout Option for width ensures the status doesn't jump around too much
            GUILayout.Label(new GUIContent($" {interaction.Status}", EditorGUIUtility.IconContent(iconName).image),
                EditorStyles.boldLabel, GUILayout.Width(80)); // Fixed width for status helps alignment

            GUI.contentColor = originalContentColor;

            GUILayout.EndHorizontal();

            // --- BODY ROW (Task UI) ---
            if (interaction.ActiveTask != null)
            {
                GUILayout.Space(5);
                GUILayout.BeginHorizontal();
                GUILayout.Space(10); // Indent
                GUILayout.BeginVertical();
                interaction.ActiveTask.DrawUI();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            else if (!string.IsNullOrEmpty(interaction.ErrorMessage))
            {
                EditorGUILayout.HelpBox(interaction.ErrorMessage, MessageType.Error);
            }

            GUILayout.Space(4);
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }

        public class AIInteraction
        {
            public string UserPrompt;
            public string Status;
            public bool IsCompleted;
            public string ErrorMessage;
            public AITask ActiveTask;
        }
    }
}