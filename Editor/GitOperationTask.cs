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
        // Optional: We don't display reasoning in the UI row, but asking for it helps the AI think.
    }

    public class GitOperationTask : AIOperationTask<GitCommandOperation>
    {
        public override string DisplayName => "Git Operations";
        public override string ToolDescription => "Executes Git commands (status, log, add, commit, revert). Returns terminal output.";
        public override bool CanUndo => false;

        private Vector2 _terminalScrollPosition;

        public override string GenerateFullPrompt(string userInstruction)
        {
            // 1. Context Gathering
            var status = RunGitCommand("status");
            var recentLog = RunGitCommand("log -n 5 --oneline");

            var sb = new StringBuilder();

            // --- SYSTEM ROLE & GOAL ---
            sb.AppendLine("You are a Senior DevOps Engineer managing a Unity project repository.");
            sb.AppendLine("Your goal is to translate user instructions into precise, safe, and multi-step Git commands.");
            sb.AppendLine("Output must be strictly valid JSON.");

            // --- CONTEXT INJECTION ---
            sb.AppendLine("\n=== REPOSITORY STATE ===");
            sb.AppendLine($"[git status output]:\n{status}");
            sb.AppendLine($"[Recent History]:\n{recentLog}");

            // Inject Orchestrator Context (if any)
            AppendContextIfAvailable(sb);

            // --- THE TASK ---
            sb.AppendLine($"\n=== USER INSTRUCTION ===\n\"{userInstruction}\"");

            // --- CRITICAL RULES (PROMPT ENGINEERING) ---
            sb.AppendLine("\n=== GUIDELINES ===");
            sb.AppendLine("1. NO LAZY SHORTCUTS: Do not just output 'git add .' unless the user explicitly asks to 'commit everything'.");
            sb.AppendLine("2. SPECIFICITY: If the user says 'commit script changes', find the scripts in the status and 'git add' them specifically if possible.");
            sb.AppendLine("3. SAFETY: If the user wants to 'undo', prefer 'git restore' or 'git revert' over 'git reset --hard' unless explicitly requested.");
            sb.AppendLine("4. CHAINING: Break complex tasks into steps (e.g., 'git add <file>', then 'git commit').");
            sb.AppendLine("5. SYNTAX: Use double quotes for messages. No interactive commands.");

            // --- OUTPUT FORMAT ---
            sb.AppendLine("\n=== RESPONSE FORMAT ===");
            sb.AppendLine("Return a JSON object with a list of operations.");
            sb.AppendLine("Example:");
            sb.AppendLine("{");
            sb.AppendLine("  \"operations\": [");
            sb.AppendLine("    { \"description\": \"Stage the player controller script\", \"command\": \"git add Assets/Scripts/PlayerController.cs\" },");
            sb.AppendLine("    { \"description\": \"Commit with descriptive message\", \"command\": \"git commit -m \\\"Fixed player movement bug\\\"\" }");
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // ... (Rest of the class remains identical to your previous Refactor) ...

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
            EditorGUILayout.TextArea(_executionData, EditorStyles.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            
            GUILayout.Label($"Status: {StatusMessage}", EditorStyles.miniLabel);
            GUILayout.EndVertical();
        }

        public override void Execute()
        {
            if (IsExecuted) return;
            StringBuilder logBuilder = new StringBuilder();

            foreach (var op in _proposedOperations)
            {
                logBuilder.AppendLine($"> {op.command}");
                string args = op.command.Trim().StartsWith("git ") ? op.command.Trim().Substring(4) : op.command;
                
                string output = RunGitCommand(args);
                logBuilder.AppendLine(output);
                logBuilder.AppendLine("----------------");
            }

            _executionData = logBuilder.ToString();
            StatusMessage = "Commands executed.";
            IsExecuted = true;
        }

        public override void Undo() { }

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
                    return !string.IsNullOrEmpty(error) ? output + "\n[Git Error]: " + error : output;
                }
            }
            catch (System.Exception e) { return $"[System Error]: {e.Message}"; }
        }
    }
}