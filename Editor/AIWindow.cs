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
        
        // Optimization: Cached Styles
        private GUIStyle _userPromptStyle;
        private GUIStyle _countStyle;
        private bool _stylesInitialized = false;

        [MenuItem("Tools/Forge AI Agent")]
        public static void ShowWindow() => GetWindow<AIWindow>("AI Agent");

        private void OnEnable()
        {
            _availableTaskTemplates = new List<AITask>();

            // This is much faster than standard C# reflection in the Editor
            var taskTypes = TypeCache.GetTypesDerivedFrom<AITask>();

            foreach (var type in taskTypes)
            {
                // Skip abstract classes (like your base AIOperationTask or AITask itself)
                if (type.IsAbstract || type.IsInterface) continue;

                try
                {
                    // Create an instance of the task to get its DisplayName
                    var taskInstance = (AITask)System.Activator.CreateInstance(type);
                    _availableTaskTemplates.Add(taskInstance);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ForgeAI] Failed to load task {type.Name}: {e.Message}");
                }
            }

            _availableTaskTemplates.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName));

            // Update the dropdown array
            _taskDisplayNames = _availableTaskTemplates.Select(x => x.DisplayName).ToArray();
            
            // Force styles to re-init
            _stylesInitialized = false; 
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _userPromptStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft
            };

            _countStyle = new GUIStyle(EditorStyles.miniLabel) 
            { 
                alignment = TextAnchor.MiddleRight 
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles(); // Ensure styles exist

            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Forge AI Assistant", EditorStyles.boldLabel);
            GUILayout.EndVertical();

            GUILayout.Space(5);
            DrawInputArea();
            GUILayout.Space(5);
            DrawTaskHistory();
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        private void DrawInputArea()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);

            // Row 1: Task Selection & File Count
            GUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("d_SettingsIcon"), GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label("Task Mode:", GUILayout.Width(70));
            _selectedTaskIndex = EditorGUILayout.Popup(_selectedTaskIndex, _taskDisplayNames);

            GUILayout.FlexibleSpace();

            int selectionCount = Selection.objects.Length;
            string labelText = $"{selectionCount} File{(selectionCount != 1 ? "s" : "")} Selected";

            if (selectionCount == 0) _countStyle.normal.textColor = new Color(1f, 0.6f, 0.6f);
            else _countStyle.normal.textColor = GUI.skin.label.normal.textColor;

            GUILayout.Label(labelText, _countStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            GUILayout.Label("Instruction:");
            GUI.SetNextControlName("PromptInput");
            _currentPrompt = EditorGUILayout.TextArea(_currentPrompt, GUILayout.Height(60));

            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            var defaultColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);

            if (GUILayout.Button(new GUIContent(" Execute Request", EditorGUIUtility.IconContent("d_PlayButton").image),
                    GUILayout.Height(30)))
            {
                if (!string.IsNullOrWhiteSpace(_currentPrompt))
                {
                    PerformTask(_currentPrompt);
                    _currentPrompt = "";
                    GUI.FocusControl(null);
                }
            }

            GUI.backgroundColor = defaultColor;

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

            // OPTIMIZATION: Collapse all previous interactions to save rendering cost
            foreach (var oldInteraction in _interactions)
            {
                oldInteraction.IsExpanded = false;
            }

            var template = _availableTaskTemplates[_selectedTaskIndex];
            var activeTask = (AITask)System.Activator.CreateInstance(template.GetType());

            string fullPromptToSend = activeTask.GenerateFullPrompt(userPrompt);

            // Handle the case where the task aborts (e.g. no files selected)
            if (string.IsNullOrEmpty(fullPromptToSend))
            {
                var errorInteraction = new AIInteraction
                {
                    UserPrompt = userPrompt,
                    Status = "Aborted",
                    ErrorMessage = "Task requires selected files, but none were found.",
                    IsCompleted = true,
                    IsExpanded = true // Keep error visible
                };
                
                _interactions.Add(errorInteraction);
                _shouldAutoScroll = true;
                Repaint();
                return;
            }

            var newInteraction = new AIInteraction
            {
                UserPrompt = userPrompt,
                Status = "Thinking...",
                ActiveTask = activeTask,
                IsExpanded = true // Only expand the newest one
            };

            _interactions.Add(newInteraction);
            _shouldAutoScroll = true;

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

            // Optional: Limit history to last 50 items to prevent eventual memory/perf bloat
            // int startIndex = Mathf.Max(0, _interactions.Count - 50);
            
            for (int i = 0; i < _interactions.Count; i++)
            {
                DrawInteractionItem(_interactions[i]);
            }

            if (_shouldAutoScroll && Event.current.type == EventType.Repaint)
            {
                _scrollPosition.y = float.MaxValue;
                _shouldAutoScroll = false;
                Repaint();
            }
        }

        private void DrawInteractionItem(AIInteraction interaction)
        {
            // Outer container
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(2);

            // --- HEADER ROW (Click to Toggle Expand) ---
            GUILayout.BeginHorizontal();

            // 1. Foldout / Icon
            // We use a button that acts as a toggle for IsExpanded
            var icon = interaction.IsExpanded 
                ? EditorGUIUtility.IconContent("d_icon dropdown").image 
                : EditorGUIUtility.IconContent("d_forward").image;

            if (GUILayout.Button(icon, EditorStyles.label, GUILayout.Width(16), GUILayout.Height(16)))
            {
                interaction.IsExpanded = !interaction.IsExpanded;
            }

            // 2. User Prompt (Clicking text also toggles expand)
            if (GUILayout.Button(new GUIContent($"<b>User:</b> {interaction.UserPrompt}", "Click to toggle details"), _userPromptStyle))
            {
                interaction.IsExpanded = !interaction.IsExpanded;
            }

            GUILayout.FlexibleSpace();

            // 3. Status
            DrawStatus(interaction);

            GUILayout.EndHorizontal();

            // --- OPTIMIZATION: ONLY DRAW BODY IF EXPANDED ---
            if (interaction.IsExpanded)
            {
                GUILayout.Space(5);
                
                if (interaction.ActiveTask != null)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(15); // Indent
                    GUILayout.BeginVertical();
                    
                    // This is the expensive call. Skipping it when collapsed saves huge FPS.
                    interaction.ActiveTask.DrawUI();
                    
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }
                else if (!string.IsNullOrEmpty(interaction.ErrorMessage))
                {
                    EditorGUILayout.HelpBox(interaction.ErrorMessage, MessageType.Error);
                }
                
                GUILayout.Space(5);
            }

            GUILayout.EndVertical();
        }

        private void DrawStatus(AIInteraction interaction)
        {
            Color statusColor;
            string iconName;

            if (!string.IsNullOrEmpty(interaction.ErrorMessage))
            {
                statusColor = new Color(1f, 0.4f, 0.4f);
                iconName = "d_console.erroricon.sml";
            }
            else if (interaction.IsCompleted)
            {
                statusColor = new Color(0.4f, 1f, 0.4f);
                iconName = "d_winbtn_mac_max";
            }
            else
            {
                statusColor = new Color(1f, 0.9f, 0.4f);
                iconName = "d_WaitSpin00";
            }

            var originalContentColor = GUI.contentColor;
            GUI.contentColor = statusColor;

            GUILayout.Label(new GUIContent($" {interaction.Status}", EditorGUIUtility.IconContent(iconName).image),
                EditorStyles.boldLabel, GUILayout.Width(80));

            GUI.contentColor = originalContentColor;
        }

        public class AIInteraction
        {
            public string UserPrompt;
            public string Status;
            public bool IsCompleted;
            public string ErrorMessage;
            public AITask ActiveTask;
            
            // New field to track UI state per item
            public bool IsExpanded = true; 
        }
    }
}