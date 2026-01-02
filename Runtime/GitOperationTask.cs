using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DenizYanar.ForgeAI.Tasks
{
    public class GitOperationTask : AITask
    {
        public override string DisplayName => "Git Operations";
        public override bool CanUndo => false; // Git actions are hard to undo automatically

        // ---------------------------------------------------------
        // DATA STRUCTURES
        // ---------------------------------------------------------

        [System.Serializable]
        private class GitCommandOperation
        {
            public string description; // "Staging all files"
            public string command; // "git add ."
        }

        [System.Serializable]
        private class AIResponseWrapper
        {
            public List<GitCommandOperation> operations;
        }

        // ---------------------------------------------------------
        // STATE
        // ---------------------------------------------------------

        private List<GitCommandOperation> _proposedOperations = new();
        private string _consoleOutput; // To show the output of the commands
        private string _rawJsonForDebug;

        private Vector2 _terminalScrollPosition;
        private GUIStyle _cachedLogStyle;

        // ---------------------------------------------------------
        // 1. GENERATE PROMPT (Context: Git Status)
        // ---------------------------------------------------------
        public override string GenerateFullPrompt(string userInstruction)
        {
            // A. Get current Git Status
            string status = RunGitCommand("status");
            string recentLog = RunGitCommand("log -n 3 --oneline");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("You are a Git Expert Assistant.");
            sb.AppendLine("1. You are running in a restricted Windows environment without a shell.");
            sb.AppendLine("2. DO NOT use pipes (|), redirection (>), or chain commands (&&).");
            sb.AppendLine("3. DO NOT use Linux utilities like 'grep', 'sort', 'uniq', 'head', or 'tail'.");
            sb.AppendLine("4. Use ONLY native Git arguments. (e.g., use 'git log -n 5' instead of 'git log | head -n 5').");
            sb.AppendLine("5. while commit use \" instead of '");
            sb.AppendLine(
                "Based on the user's instruction and the current repository status, generate a list of git terminal commands to execute.");
            sb.AppendLine("Return ONLY valid JSON.");
            sb.AppendLine("");
            sb.AppendLine("--- CURRENT GIT STATUS ---");
            sb.AppendLine(string.IsNullOrEmpty(status) ? "(No output or not a git repo)" : status);
            sb.AppendLine("");
            sb.AppendLine("--- RECENT LOG ---");
            sb.AppendLine(recentLog);
            sb.AppendLine("--------------------------");
            sb.AppendLine($"USER INSTRUCTION: \"{userInstruction}\"");
            sb.AppendLine("");
            sb.AppendLine("--- RESPONSE FORMAT ---");
            sb.AppendLine(
                "{ \"operations\": [ { \"description\": \"Stage files\", \"command\": \"git add .\" }, { \"description\": \"Commit\", \"command\": \"git commit -m 'Fix bug'\" } ] }");
            sb.AppendLine("Ensure commands are safe and correct. Do not use interactive commands (like git add -p).");

            return sb.ToString();
        }

        // ---------------------------------------------------------
        // 2. PROCESS RESPONSE
        // ---------------------------------------------------------
        public override void ProcessResponse(string rawResponse)
        {
            _rawJsonForDebug = rawResponse;
            string cleanJson = rawResponse.Replace("```json", "").Replace("```", "").Trim();

            try
            {
                var wrapper = JsonUtility.FromJson<AIResponseWrapper>(cleanJson);
                if (wrapper != null && wrapper.operations != null)
                {
                    _proposedOperations = wrapper.operations;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"JSON Parse Error: {e.Message}");
            }
        }

        public override void DrawUI()
        {
            // Case 0: No Data or Parsing Error
            if (_proposedOperations == null || _proposedOperations.Count == 0)
            {
                if (!string.IsNullOrEmpty(_rawJsonForDebug))
                {
                    EditorGUILayout.HelpBox("No Git commands generated or failed to parse.", MessageType.Warning);
                }

                return;
            }

            // Case 1: Proposed Plan (Before Execution)
            if (!IsExecuted)
            {
                // ... [Your existing Case 1 code remains unchanged] ...
                // (If you want to optimize Case 1 too, move the Font creation to a cached variable!)

                GUILayout.Label($"Execution Plan ({_proposedOperations.Count} Steps)", EditorStyles.boldLabel);
                GUILayout.Space(5);

                for (int i = 0; i < _proposedOperations.Count; i++)
                {
                    var op = _proposedOperations[i];

                    GUILayout.BeginVertical(EditorStyles.helpBox);

                    GUILayout.BeginHorizontal();
                    var stepIcon = EditorGUIUtility.IconContent("d_StepButton").image;
                    GUILayout.Label(stepIcon, GUILayout.Width(16), GUILayout.Height(16));
                    GUILayout.Label($"<b>Step {i + 1}:</b> {op.description}",
                        new GUIStyle(EditorStyles.label) { richText = true });
                    GUILayout.EndHorizontal();

                    GUI.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.1f);
                    GUILayout.BeginVertical(EditorStyles.helpBox);
                    GUI.backgroundColor = Color.white;

                    // Note: For best optimization, cache this font/style outside this loop as well
                    var codeStyle = new GUIStyle(EditorStyles.label);
                    codeStyle.font = Font.CreateDynamicFontFromOSFont("Consolas", 12);
                    if (codeStyle.font == null) codeStyle.font = EditorStyles.standardFont;
                    codeStyle.wordWrap = true;
                    codeStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);

                    GUILayout.Label($"> {op.command}", codeStyle);

                    GUILayout.EndVertical();
                    GUILayout.EndVertical();

                    if (i < _proposedOperations.Count - 1)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(EditorGUIUtility.IconContent("d_scrolldown").image);
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }
                }

                GUILayout.Space(10);

                var defaultColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
                if (GUILayout.Button(
                        new GUIContent(" Confirm & Run All Commands",
                            EditorGUIUtility.IconContent("d_PlayButton").image), GUILayout.Height(30)))
                {
                    Execute();
                }

                GUI.backgroundColor = defaultColor;
            }

            // Case 2: Execution Results (Log) -> OPTIMIZED
            else
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);

                GUILayout.BeginHorizontal();
                GUILayout.Label(EditorGUIUtility.IconContent("d_winbtn_mac_max").image, GUILayout.Width(16),
                    GUILayout.Height(16));
                GUILayout.Label("Terminal Output", EditorStyles.boldLabel);
                GUILayout.EndHorizontal();

                // 1. Optimization: Create style only once
                if (_cachedLogStyle == null)
                {
                    _cachedLogStyle = new GUIStyle(EditorStyles.textArea);
                    _cachedLogStyle.fontSize = 11;
                    _cachedLogStyle.wordWrap = true;
                    _cachedLogStyle.normal.textColor =
                        new Color(0.9f, 0.9f, 0.9f); // Make text slightly lighter for readability
                }

                // 2. Start Scroll View
                // GUILayout.Height(150) restricts the view height, forcing the scrollbar to appear if content is larger
                _terminalScrollPosition =
                    EditorGUILayout.BeginScrollView(_terminalScrollPosition, GUILayout.Height(150));

                // Render the text area. ExpandHeight lets it grow inside the scroll view.
                EditorGUILayout.TextArea(_consoleOutput, _cachedLogStyle, GUILayout.ExpandHeight(true));

                EditorGUILayout.EndScrollView();

                GUILayout.EndVertical();
            }
        }

        // ---------------------------------------------------------
        // 4. EXECUTE LOGIC
        // ---------------------------------------------------------
        public override void Execute()
        {
            if (IsExecuted) return;

            StringBuilder logBuilder = new StringBuilder();
            bool hasError = false;

            foreach (var op in _proposedOperations)
            {
                logBuilder.AppendLine($"> {op.command}");

                // We split "git commit ..." into "commit ..." because RunGitCommand prepends "git" if needed, 
                // or we can execute the raw command line.
                // Let's assume the AI sends full "git add ."

                string args = op.command;
                if (args.StartsWith("git ")) args = args.Substring(4);

                string output = RunGitCommand(args);
                logBuilder.AppendLine(output);
                logBuilder.AppendLine("----------------");
            }

            _consoleOutput = logBuilder.ToString();
            ExecutionResult = "Commands Executed. Check log.";
            IsExecuted = true;
        }

        // ---------------------------------------------------------
        // HELPER: Run Process
        // ---------------------------------------------------------
        private string RunGitCommand(string args)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = Application.dataPath.Replace("/Assets", ""), // Project Root
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(error)) return output + "\nError: " + error;
                    return output;
                }
            }
            catch (System.Exception e)
            {
                return $"FAILED to run git: {e.Message}";
            }
        }

        public override void Undo()
        {
            // Not implemented for Git
        }
    }
}