using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ForgeAI
{
    public static class ReActEngine
    {
        private struct ToolInfo
        {
            public MethodInfo Method;
            public string Description;
            public string ParameterHints;
        }

        private static Dictionary<string, ToolInfo> availableTools = new Dictionary<string, ToolInfo>();
        private static bool initialized = false;

        public static void Initialize()
        {
            if (initialized) return;

            var methods = TypeCache.GetMethodsWithAttribute<ForgeToolAttribute>();

            availableTools.Clear();
            foreach (var method in methods)
            {
                if (!method.IsStatic || !method.IsPublic) continue;

                var attr = method.GetCustomAttribute<ForgeToolAttribute>();
                availableTools[method.Name] = new ToolInfo
                {
                    Method = method,
                    Description = attr.Description,
                    ParameterHints = attr.ParameterHints
                };
            }
            initialized = true;
        }

        public static string GetSystemPrompt()
        {
            Initialize();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("You are an intelligent Unity Editor Assistant.");
            sb.AppendLine("You can manipulate the scene and editor using the following tools:");
            sb.AppendLine("");

            foreach (var tool in availableTools)
            {
                sb.AppendLine($"- {tool.Key}({tool.Value.ParameterHints}): {tool.Value.Description}");
            }

            sb.AppendLine("");
            sb.AppendLine("To use a tool, your response MUST contain a JSON block like this:");
            sb.AppendLine("```json");
            sb.AppendLine("{ \"tool\": \"ToolName\", \"args\": [\"arg1\", \"arg2\"] }");
            sb.AppendLine("```");
            sb.AppendLine("Do not use tools if you can answer directly.");
            sb.AppendLine("When you receive an Observation, analyze it and decide the next step.");
            
            return sb.ToString();
        }

        public static string ExtractActionJson(string response)
        {
            // Non-greedy match for a JSON object containing a "tool" property
            var match = System.Text.RegularExpressions.Regex.Match(response, @"\{[^{}]*?""tool""[^{}]*?\}", System.Text.RegularExpressions.RegexOptions.Singleline);
            
            if (match.Success)
            {
                ForgeLogger.Log("ActionParsing", "Extracted JSON Action", match.Value);
                return match.Value;
            }
            ForgeLogger.Log("ActionParsing", "No Action JSON found in response");
            return null;
        }

        public static string ExecuteTool(string jsonAction)
        {
            Initialize();
            try
            {
                // Simple JSON parsing wrapper
                var action = JsonUtility.FromJson<ToolAction>(jsonAction);
                
                if (action == null || string.IsNullOrEmpty(action.tool))
                {
                    return "Error: Could not parse action JSON.";
                }

                ForgeLogger.Log("ToolExecution", $"Invoking {action.tool}", jsonAction);

                if (availableTools.TryGetValue(action.tool, out var toolInfo))
                {
                    var parameters = toolInfo.Method.GetParameters();
                    if (action.args == null || action.args.Length != parameters.Length)
                    {
                        return $"Error: Argument count mismatch. Expected {parameters.Length}, got {action.args?.Length ?? 0}.";
                    }

                    object[] invokeArgs = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var paramType = parameters[i].ParameterType;
                        invokeArgs[i] = Convert.ChangeType(action.args[i], paramType);
                    }

                    string result = (string)toolInfo.Method.Invoke(null, invokeArgs);
                    ForgeLogger.Log("ToolResult", $"Result of {action.tool}", result);
                    return result;
                }
                
                string notFoundMsg = $"Error: Tool '{action.tool}' not found.";
                ForgeLogger.Log("ToolError", "Tool Not Found", notFoundMsg);
                return notFoundMsg;
            }
            catch (Exception e)
            {
                string errorMsg = $"Error executing tool: {e.Message}";
                ForgeLogger.Log("ToolError", "Exception", errorMsg);
                return errorMsg;
            }
        }

        [Serializable]
        private class ToolAction
        {
            public string tool;
            public string[] args;
        }
    }
}
