using System.Collections.Generic;
using DenizYanar.ForgeAI.Editor; // Assuming your Preference script is here
using UnityEditor;
using UnityEngine;

namespace DenizYanar.ForgeAI
{
    public class AIWindow : EditorWindow
    {
        private string _currentPrompt = "";
        private Vector2 _scrollPosition;
        private readonly List<AITask> _tasks = new();

        [MenuItem("Tools/Unity 6 AI Agent")]
        public static void ShowWindow() => GetWindow<AIWindow>("AI Agent");

        private void OnGUI()
        {
            GUILayout.Label("AI Assistant (Multi-Threaded)", EditorStyles.boldLabel);

            DrawInputArea();
            DrawTaskHistory();
        }

        private void DrawInputArea()
        {
            GUILayout.Label("New Prompt:");
            _currentPrompt = EditorGUILayout.TextArea(_currentPrompt, GUILayout.Height(60));

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Send Request", GUILayout.Height(30)))
            {
                if (!string.IsNullOrWhiteSpace(_currentPrompt))
                {
                    FireAndForgetTask(_currentPrompt);
                    _currentPrompt = ""; // Clear input for the next one
                    GUI.FocusControl(null); // Unfocus text area
                }
            }

            if (GUILayout.Button("Clear History", GUILayout.Height(30), GUILayout.Width(100)))
            {
                _tasks.Clear();
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(15);
        }

        private void DrawTaskHistory()
        {
            GUILayout.Label("Active Tasks & History:", EditorStyles.boldLabel);

            using var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPosition);
            _scrollPosition = scrollView.scrollPosition;

            // Iterate backwards to show newest tasks at the top
            for (int i = _tasks.Count - 1; i >= 0; i--)
            {
                var task = _tasks[i];
                DrawTaskItem(task);
            }
        }

        private void DrawTaskItem(AITask task)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            // Header: Status and Time
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>Q:</b> {task.Prompt}",
                new GUIStyle(EditorStyles.label) { richText = true, wordWrap = false });

            var statusStyle = task.IsCompleted ? EditorStyles.label : EditorStyles.miniLabel;
            var statusColor = task.IsCompleted ? (task.HasError ? Color.red : Color.green) : Color.yellow;

            var originalColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Label(task.Status, statusStyle, GUILayout.Width(80));
            GUI.color = originalColor;

            GUILayout.EndHorizontal();

            // Body: The Response
            if (!string.IsNullOrEmpty(task.Response))
            {
                GUILayout.Space(5);
                EditorGUILayout.TextArea(task.Response, EditorStyles.wordWrappedLabel);
            }

            GUILayout.EndVertical();
            GUILayout.Space(5);
        }

        private async void FireAndForgetTask(string prompt)
        {
            var storedKey = AIProjectPreferences.APIKey;

            if (string.IsNullOrEmpty(storedKey))
            {
                if (EditorUtility.DisplayDialog("Missing Key", "Please set your API Key in Preferences.", "Open Prefs",
                        "Cancel"))
                {
                    SettingsService.OpenUserPreferences("Preferences/AI Assistant");
                }

                return;
            }

            // 1. Create and register the task
            var newTask = new AITask { Prompt = prompt, Status = "Thinking..." };
            _tasks.Add(newTask);

            // 2. Run the logic (does not block the UI)
            var result = await AIClient.SendRequestAsync(prompt, storedKey);

            // 3. Update the specific task instance
            if (string.IsNullOrEmpty(result))
            {
                newTask.Response = "Error: No response or failure.";
                newTask.Status = "Failed";
                newTask.HasError = true;
            }
            else
            {
                newTask.Response = result;
                newTask.Status = "Done";
            }

            newTask.IsCompleted = true;
            Repaint(); // Refresh UI to show the new state
        }

        // Simple data class to hold state for each request
        private class AITask
        {
            public string Prompt;
            public string Response;
            public string Status;
            public bool IsCompleted;
            public bool HasError;
        }
    }
}