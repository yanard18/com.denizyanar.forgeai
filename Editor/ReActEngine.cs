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
            // Find the first '{' and the last '}'
            int start = response.IndexOf('{');
            int end = response.LastIndexOf('}');

            if (start != -1 && end != -1 && end > start)
            {
                string jsonCandidate = response.Substring(start, end - start + 1);
                ForgeLogger.Log("ActionParsing", "Extracted JSON Candidate", jsonCandidate);
                return jsonCandidate;
            }
            
            ForgeLogger.Log("ActionParsing", "No JSON brackets found in response");
            return null;
        }

        public static string ExecuteTool(string jsonAction)
        {
            Initialize();
            try
            {
                var action = ParseToolAction(jsonAction);
                
                if (action == null || string.IsNullOrEmpty(action.tool))
                {
                    return "Error: Could not parse action from response.";
                }

                ForgeLogger.Log("ToolExecution", $"Invoking {action.tool}", $"Args: {string.Join(", ", action.args)}");

                if (availableTools.TryGetValue(action.tool, out var toolInfo))
                {
                    var parameters = toolInfo.Method.GetParameters();
                    // Allow for optional parameters or fuzzy matching in future, but for now strict count check
                    // We might want to fill missing args with defaults, but let's stick to strict for safety first.
                    if (action.args == null || action.args.Length != parameters.Length)
                    {
                        return $"Error: Argument count mismatch. Expected {parameters.Length}, got {action.args?.Length ?? 0}.";
                    }

                    object[] invokeArgs = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var paramType = parameters[i].ParameterType;
                        try 
                        {
                            invokeArgs[i] = Convert.ChangeType(action.args[i], paramType);
                        }
                        catch
                        {
                            // fallback for basic types if Convert fails directly
                            invokeArgs[i] = action.args[i];
                        }
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

        private class ToolAction
        {
            public string tool;
            public string[] args;
        }

        /// <summary>
        /// A robust, dependency-free parser that tolerates common LLM JSON errors 
        /// (single quotes, mixed types, trailing commas).
        /// </summary>
        private static ToolAction ParseToolAction(string json)
        {
            var action = new ToolAction();
            
            // 1. Extract Tool Name
            // Look for "tool" or 'tool', followed by colon, then quotes
            var toolMatch = System.Text.RegularExpressions.Regex.Match(json, @"[""']tool[""']\s*:\s*[""']([^""']+)[""']");
            if (toolMatch.Success)
            {
                action.tool = toolMatch.Groups[1].Value;
            }

            // 2. Extract Arguments Array
            // Look for "args" or 'args', followed by colon, then start bracket
            var argsStartMatch = System.Text.RegularExpressions.Regex.Match(json, @"[""']args[""']\s*:\s*\[");
            if (argsStartMatch.Success)
            {
                int arrayStartIndex = argsStartMatch.Index + argsStartMatch.Length;
                int arrayEndIndex = -1;
                int depth = 0;
                bool inQuote = false;
                char quoteChar = '\0';

                // Find the matching closing bracket ']'
                for (int i = arrayStartIndex; i < json.Length; i++)
                {
                    char c = json[i];
                    if (inQuote)
                    {
                        if (c == quoteChar && json[i - 1] != '\\') inQuote = false;
                    }
                    else
                    {
                        if (c == '"' || c == '\'') { inQuote = true; quoteChar = c; }
                        else if (c == '[') depth++;
                        else if (c == ']')
                        {
                            if (depth == 0)
                            {
                                arrayEndIndex = i;
                                break;
                            }
                            depth--;
                        }
                    }
                }

                if (arrayEndIndex != -1)
                {
                    string argsContent = json.Substring(arrayStartIndex, arrayEndIndex - arrayStartIndex);
                    action.args = ParseArgumentList(argsContent);
                }
                else
                {
                    action.args = new string[0];
                }
            }
            else
            {
                action.args = new string[0];
            }

            return action;
        }

        private static string[] ParseArgumentList(string content)
        {
            var list = new List<string>();
            var currentArg = new System.Text.StringBuilder();
            bool inQuote = false;
            char quoteChar = '\0';

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];

                if (inQuote)
                {
                    if (c == quoteChar && content[i - 1] != '\\') 
                    {
                        inQuote = false; 
                    }
                    else
                    {
                        currentArg.Append(c);
                    }
                }
                else
                {
                    if (c == '"' || c == '\'') 
                    {
                        inQuote = true; 
                        quoteChar = c;
                    }
                    else if (c == ',')
                    {
                        // End of argument
                        list.Add(currentArg.ToString().Trim());
                        currentArg.Clear();
                    }
                    else
                    {
                        // Capture unquoted values (numbers, booleans)
                        // Ignore whitespace if we haven't started capturing yet? 
                        // Actually simplistic capture is fine, we Trim() later.
                        if (!char.IsWhiteSpace(c))
                        {
                            currentArg.Append(c);
                        }
                    }
                }
            }

            // Add the last argument
            if (currentArg.Length > 0)
            {
                list.Add(currentArg.ToString().Trim());
            }

            return list.ToArray();
        }
    }
}
