using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ForgeAI
{
    public static class ReActEngine
    {
        private struct ToolInfo
        {
            public IForgeTool Instance;
            public string Name;
            public string Description;
            public bool RequiresApproval;
        }

        private static Dictionary<string, ToolInfo> availableTools = new Dictionary<string, ToolInfo>(StringComparer.OrdinalIgnoreCase);
        private static bool initialized = false;

        public static void Initialize()
        {
            if (initialized) return;

            availableTools.Clear();
            var toolTypes = TypeCache.GetTypesWithAttribute<ForgeToolAttribute>();

            foreach (var type in toolTypes)
            {
                if (!typeof(IForgeTool).IsAssignableFrom(type)) continue;

                var attr = (ForgeToolAttribute)Attribute.GetCustomAttribute(type, typeof(ForgeToolAttribute));
                try
                {
                    var instance = (IForgeTool)Activator.CreateInstance(type);
                    availableTools[attr.Name] = new ToolInfo
                    {
                        Instance = instance,
                        Name = attr.Name,
                        Description = attr.Description,
                        RequiresApproval = attr.RequiresApproval
                    };
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ForgeAI] Failed to instantiate tool {type.Name}: {e.Message}");
                }
            }
            initialized = true;
        }

        public static bool RequiresApproval(string toolName)
        {
            Initialize();
            if (availableTools.TryGetValue(toolName, out var info))
            {
                return info.RequiresApproval;
            }
            return true; 
        }

        public static string GetSystemPrompt()
        {
            Initialize();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("You are an intelligent Unity Editor Assistant (ForgeAI).");
            sb.AppendLine("You have access to the following tools:");
            sb.AppendLine("");

            foreach (var tool in availableTools.Values)
            {
                sb.AppendLine(tool.Instance.GetPromptDefinition());
                sb.AppendLine(""); 
            }

            sb.AppendLine("### Guidelines:");
            sb.AppendLine("1. **ReAct Protocol**: You MUST reason before acting. If you decide an action is needed, output the Action block IMMEDIATELY in the same response. Do not ask for permission if the task is clear.");
            sb.AppendLine("   Format:");
            sb.AppendLine("   Thought: [Reasoning]");
            sb.AppendLine("   Action: [ToolName]");
            sb.AppendLine("   Action Input:");
            sb.AppendLine("   [Raw Text Argument]");
            sb.AppendLine("");
            sb.AppendLine("2. **No Stalling**: Do not say 'I will now do X' without actually providing the 'Action:' block. If you are ready to act, act.");
            sb.AppendLine("3. **Multiple Actions**: You can propose multiple actions in one response by repeating the Action/Action Input blocks.");
            sb.AppendLine("4. **Safety**: Do not use ReplaceText on binary files (.fbx, .png). Use MoveAsset to rename.");
            sb.AppendLine("5. **Memory Constraint**: Past observations are HIDDEN to save memory. You MUST summarize important findings (e.g., file paths found) in your 'Thought' block immediately after acting, or you will lose them.");
            sb.AppendLine("");
            sb.AppendLine("Begin!");
            
            return sb.ToString();
        }

        public static List<ToolAction> ExtractAllActions(string response)
        {
            var actions = new List<ToolAction>();
            
            // Matches "Action: <Name>"
            var actionMatches = Regex.Matches(response, @"^Action:\s*([a-zA-Z0-9_]+)", RegexOptions.Multiline);

            for (int i = 0; i < actionMatches.Count; i++)
            {
                var match = actionMatches[i];
                string toolName = match.Groups[1].Value.Trim();
                
                // Determine the range for this action's input
                int startSearch = match.Index + match.Length;
                int endSearch = (i + 1 < actionMatches.Count) ? actionMatches[i + 1].Index : response.Length;
                
                // Look for "Action Input:"
                string inputMarker = "Action Input:";
                int inputIndex = response.IndexOf(inputMarker, startSearch, endSearch - startSearch, StringComparison.OrdinalIgnoreCase);

                if (inputIndex != -1)
                {
                    int contentStart = inputIndex + inputMarker.Length;
                    string rawContent = response.Substring(contentStart, endSearch - contentStart);

                    // Refinement: Stop at next "Thought:" if it appears before the next action
                    int thoughtIndex = rawContent.IndexOf("Thought:", StringComparison.OrdinalIgnoreCase);
                    if (thoughtIndex != -1)
                    {
                        rawContent = rawContent.Substring(0, thoughtIndex);
                    }

                    actions.Add(new ToolAction { tool = toolName, rawInput = rawContent.Trim() });
                }
                else
                {
                    actions.Add(new ToolAction { tool = toolName, rawInput = "" });
                }
            }
            
            return actions;
        }

        public static string ExecuteTool(ToolAction action)
        {
            Initialize();
            try
            {
                if (action == null || string.IsNullOrEmpty(action.tool))
                {
                    return "Error: Invalid tool action.";
                }

                if (availableTools.TryGetValue(action.tool, out var toolInfo))
                {
                    string result = toolInfo.Instance.Execute(action.rawInput);
                    // Logging handled by Agent via Context
                    return result;
                }
                
                return $"Error: Tool '{action.tool}' not found.";
            }
            catch (Exception e)
            {
                return $"Error executing tool: {e.Message}";
            }
        }

        public class ToolAction
        {
            public string tool;
            public string rawInput;
        }
    }
}
