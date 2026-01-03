using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

            var methods = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(m => m.GetCustomAttributes(typeof(ForgeToolAttribute), false).Length > 0);

            availableTools.Clear();
            foreach (var method in methods)
            {
                var attr = (ForgeToolAttribute)method.GetCustomAttributes(typeof(ForgeToolAttribute), false)[0];
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
            int startIndex = response.IndexOf("{");
            int endIndex = response.LastIndexOf("}");

            if (startIndex >= 0 && endIndex > startIndex)
            {
                // Basic check to see if it looks like our tool action
                string jsonCandidate = response.Substring(startIndex, endIndex - startIndex + 1);
                if (jsonCandidate.Contains("\"tool\""))
                {
                    return jsonCandidate;
                }
            }
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

                    return (string)toolInfo.Method.Invoke(null, invokeArgs);
                }
                
                return $"Error: Tool '{action.tool}' not found.";
            }
            catch (Exception e)
            {
                return $"Error executing tool: {e.Message}";
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
