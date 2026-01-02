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
                new MessageTask(), // Assuming you have a basic text chat task
                new BatchMoveTask()
            };

            _taskDisplayNames = _availableTaskTemplates.Select(x => x.DisplayName).ToArray();
        }

        private void OnGUI()
        {
            GUILayout.Label("AI Assistant", EditorStyles.boldLabel);
            DrawInputArea();
            DrawTaskHistory();
        }

        private void DrawInputArea()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            // Task Selector
            GUILayout.BeginHorizontal();
            GUILayout.Label("Task Type:", GUILayout.Width(80));
            _selectedTaskIndex = EditorGUILayout.Popup(_selectedTaskIndex, _taskDisplayNames);
            GUILayout.EndHorizontal();

            GUILayout.Label("Instruction:");
            _currentPrompt = EditorGUILayout.TextArea(_currentPrompt, GUILayout.Height(60));

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Execute Request", GUILayout.Height(30)))
            {
                if (!string.IsNullOrWhiteSpace(_currentPrompt))
                {
                    PerformTask(_currentPrompt);
                    _currentPrompt = "";
                    GUI.FocusControl(null);
                }
            }

            if (GUILayout.Button("Clear History", GUILayout.Height(30), GUILayout.Width(100)))
            {
                _interactions.Clear();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private async void PerformTask(string userPrompt)
        {
            var storedKey = AIProjectPreferences.APIKey; // Ensure you have this class
            if (string.IsNullOrEmpty(storedKey))
            {
                Debug.LogError("No API Key found.");
                return;
            }

            // 1. Create a specific instance of the selected task
            // We use Activator to create a fresh copy of the selected template type
            var template = _availableTaskTemplates[_selectedTaskIndex];
            var activeTask = (AITask)System.Activator.CreateInstance(template.GetType());

            // 2. Ask the TASK to build the prompt (injecting context/json rules)
            string fullPromptToSend = activeTask.GenerateFullPrompt(userPrompt);

            // 3. Create UI Interaction container
            var newInteraction = new AIInteraction
            {
                UserPrompt = userPrompt,
                Status = "Thinking...",
                ActiveTask = activeTask // Store the logic inside the history item
            };
            _interactions.Add(newInteraction);

            // 4. Send Request
            var rawResponse = await AIClient.SendRequestAsync(fullPromptToSend, storedKey);

            if (string.IsNullOrEmpty(rawResponse))
            {
                newInteraction.Status = "Error";
                newInteraction.ErrorMessage = "AI returned no data.";
            }
            else
            {
                // 5. Pass response back to the Task to parse
                activeTask.ProcessResponse(rawResponse);

                newInteraction.Status = "Ready";
                newInteraction.IsCompleted = true;
            }

            Repaint();
        }

        private void DrawTaskHistory()
        {
            GUILayout.Label("History:", EditorStyles.boldLabel);

            using var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPosition);
            _scrollPosition = scrollView.scrollPosition;

            // Iterate backwards to show newest at bottom (or top depending on pref)
            // Here we iterate backwards to render recent at bottom if using Flex layout, 
            // but standard Unity GUI draws top-down. 
            // Let's draw normally (Oldest -> Newest) or reverse. 
            // Usually chat logs are Top=Old, Bottom=New.
            for (int i = 0; i < _interactions.Count; i++)
            {
                DrawInteractionItem(_interactions[i]);
            }
        }

        private void DrawInteractionItem(AIInteraction interaction)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>User:</b> {interaction.UserPrompt}",
                new GUIStyle(EditorStyles.label) { richText = true });
            GUILayout.FlexibleSpace();
            GUILayout.Label(interaction.Status, EditorStyles.miniLabel);
            GUILayout.EndHorizontal();

            // Task Body
            if (interaction.ActiveTask != null)
            {
                GUILayout.Space(5);
                interaction.ActiveTask.DrawUI();
            }
            else if (!string.IsNullOrEmpty(interaction.ErrorMessage))
            {
                EditorGUILayout.HelpBox(interaction.ErrorMessage, MessageType.Error);
            }

            GUILayout.EndVertical();
            GUILayout.Space(10);
        }

        // Simple container for History
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
