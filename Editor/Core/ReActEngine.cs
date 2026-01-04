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
                    ForgeLogger.Log("Engine", $"Failed to instantiate tool {type.Name}: {e.Message}");
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
            sb.AppendLine("");
            sb.AppendLine("Begin!");
            
            return sb.ToString();
        }

        public static List<ToolAction> ExtractAllActions(string response)
        {
            var actions = new List<ToolAction>();
            
            // Regex to find "Action: <Name>" followed optionally by "Action Input:"
            // We iterate through matches.
            
            string pattern = @"Action:\s*([a-zA-Z0-9_]+)";
            var matches = Regex.Matches(response, pattern);

            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                string toolName = match.Groups[1].Value.Trim();
                
                // Find start of Action Input
                int inputStartSearchIndex = match.Index + match.Length;
                int nextActionIndex = (i + 1 < matches.Count) ? matches[i + 1].Index : response.Length;
                
                // Look for "Action Input:" literal within this range
                string inputLabel = "Action Input:";
                int labelIndex = response.IndexOf(inputLabel, inputStartSearchIndex, nextActionIndex - inputStartSearchIndex, StringComparison.OrdinalIgnoreCase);

                string toolInput = "";
                if (labelIndex != -1)
                {
                    int contentStart = labelIndex + inputLabel.Length;
                    toolInput = response.Substring(contentStart, nextActionIndex - contentStart).Trim();
                }

                actions.Add(new ToolAction { tool = toolName, rawInput = toolInput });
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
                    ForgeLogger.Log("ToolExecution", $"Invoking {action.tool}", action.rawInput);
                    string result = toolInfo.Instance.Execute(action.rawInput);
                    ForgeLogger.Log("ToolResult", $"Result of {action.tool}", result);
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