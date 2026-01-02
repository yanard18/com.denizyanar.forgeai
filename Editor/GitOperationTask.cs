using System.Diagnostics; 
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DenizYanar.ForgeAI.Tasks
{
    [System.Serializable]
    public class GitCommandOperation
    {
        public string description;
        public string command;
    }

    public class GitOperationTask : AIOperationTask<GitCommandOperation>
    {
        public override string DisplayName => "Git Operations";
        
        // 1. NEW: Describe the tool for the Agent Orchestrator
        public override string ToolDescription => "Executes Git commands (log, status, add, commit, revert). Returns the terminal output.";
        
        public override bool CanUndo => false;

        private string _consoleOutput;
        private Vector2 _terminalScrollPosition;

        // 2. NEW: This is the bridge. It lets the Orchestrator read the private _consoleOutput.
        public override string GetExecutionResult()
        {
            if (string.IsNullOrEmpty(_consoleOutput))
            {
                return "[GitOperationTask] Command executed, but no output was captured.";
            }
            return _consoleOutput;
        }

        public override string GenerateFullPrompt(string userInstruction)
        {
            string status = RunGitCommand("status");
            string recentLog = RunGitCommand("log -n 30 --oneline --all");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("You are a Git Expert. Return ONLY valid JSON.");
            sb.AppendLine($"Instruction: \"{userInstruction}\"");
            sb.AppendLine("--- STATUS ---");
            sb.AppendLine(status);
            sb.AppendLine("--- LOG ---");
            sb.AppendLine(recentLog);
            sb.AppendLine("Format: { \"operations\": [ { \"description\": \"Stage files\", \"command\": \"git add .\" } ] }");
            sb.AppendLine("Rules: No pipes (|), no interactive commands, use double quotes for commit messages.");
            return sb.ToString();
        }

        protected override void DrawOperationRow(GitCommandOperation op, int index)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("d_StepButton").image, GUILayout.Width(16), GUILayout.Height(16));
            GUILayout.Label($"<b>Step {index + 1}:</b> {op.description}", new GUIStyle(EditorStyles.label) { richText = true });
            GUILayout.EndHorizontal();

            var codeStyle = new GUIStyle(EditorStyles.label) 
            { 
                wordWrap = true, 
                normal = { textColor = new Color(0.2f, 0.8f, 0.2f) },
                font = Font.CreateDynamicFontFromOSFont("Consolas", 12) ?? EditorStyles.standardFont 
            };
            
            GUILayout.Label($"> {op.command}", codeStyle);
            GUILayout.EndVertical();
        }

        protected override void DrawExecutionResultUI()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Terminal Output", EditorStyles.boldLabel);

            _terminalScrollPosition = EditorGUILayout.BeginScrollView(_terminalScrollPosition, GUILayout.Height(150));
            
            var logStyle = new GUIStyle(EditorStyles.textArea) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.9f, 0.9f, 0.9f) } };
            EditorGUILayout.TextArea(_consoleOutput, logStyle, GUILayout.ExpandHeight(true));
            
            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        public override void Execute()
        {
            if (IsExecuted) return;
            StringBuilder logBuilder = new StringBuilder();

            foreach (var op in _proposedOperations)
            {
                logBuilder.AppendLine($"> {op.command}");
                string args = op.command.StartsWith("git ") ? op.command.Substring(4) : op.command;
                
                // Capture output
                string output = RunGitCommand(args);
                logBuilder.AppendLine(output);
                logBuilder.AppendLine("----------------");
            }

            // This string is what GetExecutionResult() will return
            _consoleOutput = logBuilder.ToString();
            
            ExecutionResult = "Done"; 
            IsExecuted = true;
        }

        public override void Undo() { /* Not supported */ }

        private string RunGitCommand(string args)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = Application.dataPath.Replace("/Assets", ""),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return !string.IsNullOrEmpty(error) ? output + "\nError: " + error : output;
                }
            }
            catch (System.Exception e) { return $"FAILED: {e.Message}"; }
        }
    }
}