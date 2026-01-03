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
            public MethodInfo Method;
            public string Description;
            public string ParameterHints;
        }

        private static Dictionary<string, ToolInfo> availableTools = new Dictionary<string, ToolInfo>(StringComparer.OrdinalIgnoreCase);
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

        public static bool RequiresApproval(string toolName)
        {
            Initialize();
            if (availableTools.TryGetValue(toolName, out var info))
            {
                var attr = info.Method.GetCustomAttribute<ForgeToolAttribute>();
                return attr != null && attr.RequiresApproval;
            }
            return true; 
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
            sb.AppendLine("### Guidelines:");
            sb.AppendLine("1. **ReAct Pattern**: Always Reason before Acting. Use the format:");
            sb.AppendLine("   Thought: [Your analysis of the state and what needs to be done]");
            sb.AppendLine("   ```json");
            sb.AppendLine("   { \"tool\": \"ToolName\", \"args\": [\"arg1\", \"arg2\"] }");
            sb.AppendLine("   ```");
            sb.AppendLine("2. **File Editing**: When using `ReplaceText`, you MUST ensure `oldText` matches the file content EXACTLY (including whitespace). ALWAYS `ReadFile` before editing to get the exact string.");
            sb.AppendLine("3. **Path Safety**: Do not guess file paths. Use `ListFiles` to explore if you are unsure.");
            sb.AppendLine("4. **Batching**: You can output MULTIPLE tool blocks in one response to perform actions in sequence. For moving many files to one folder, use `BatchMove`. For renaming many files, use `BatchRename`.");
            sb.AppendLine("5. **Bulk Formats**:");
            sb.AppendLine("   - `BatchMove(sources, targetDir)`: sources is a semi-colon separated string (e.g., 'path1;path2;path3').");
            sb.AppendLine("   - `BatchRename(renamesJson)`: format is '[{\"p\":\"old/path\",\"n\":\"newName\"},...]'.");
            sb.AppendLine("6. **Completion**: If you can answer the user's request directly or have finished the task, simply provide the final answer without a tool block.");
            sb.AppendLine("7. **Action Enforcement**: If your response implies an action (e.g., 'I will move...', 'Creating file...'), you **MUST** include the JSON tool block. Describing the plan without the JSON is strictly forbidden.");
            sb.AppendLine("");
            sb.AppendLine("CRITICAL: Do not just say you will do something. DO IT by outputting the JSON.");
            sb.AppendLine("Begin!");
            
            return sb.ToString();
        }

        public static List<ToolAction> ExtractAllActions(string response)
        {
            var actions = new List<ToolAction>();
            
            int index = 0;
            while (index < response.Length)
            {
                int start = response.IndexOf('{', index);
                if (start == -1) break;

                int end = FindMatchingBrace(response, start);
                if (end != -1)
                {
                    string candidate = response.Substring(start, end - start + 1);
                    if (candidate.Contains("\"tool\"") || candidate.Contains("'tool'"))
                    {
                        var action = ParseToolAction(candidate);
                        if (action != null && !string.IsNullOrEmpty(action.tool))
                        {
                            actions.Add(action);
                        }
                    }
                    index = end + 1;
                }
                else
                {
                    index = start + 1;
                }
            }
            
            if (actions.Count > 0)
            {
                ForgeLogger.Log("ActionParsing", $"Extracted {actions.Count} actions");
            }
            return actions;
        }

        private static int FindMatchingBrace(string text, int startIndex)
        {
            int depth = 0;
            bool inQuote = false;
            char quoteChar = '\0';

            for (int i = startIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuote)
                {
                    if (c == quoteChar && text[i - 1] != '\\') inQuote = false;
                }
                else
                {
                    if (c == '"' || c == '\'') { inQuote = true; quoteChar = c; }
                    else if (c == '{') depth++;
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0) return i;
                    }
                }
            }
            return -1;
        }

        public static string ExtractActionJson(string response)
        {
            var actions = ExtractAllActions(response);
            if (actions.Count > 0) return "found"; 
            return null;
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

                ForgeLogger.Log("ToolExecution", $"Invoking {action.tool}", $"Args: {string.Join(", ", action.args)}");

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
                        try 
                        {
                            invokeArgs[i] = Convert.ChangeType(action.args[i], paramType);
                        }
                        catch
                        {
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

        public class ToolAction
        {
            public string tool;
            public string[] args;
        }

        public static ToolAction ParseToolAction(string json)
        {
            var action = new ToolAction();
            
            var toolMatch = Regex.Match(json, @"[""']tool[""']\s*:\s*[""']([^""']+)[""']");
            if (toolMatch.Success)
            {
                action.tool = toolMatch.Groups[1].Value;
            }

            var argsStartMatch = Regex.Match(json, @"[""']args[""']\s*:\s*\[");
            if (argsStartMatch.Success)
            {
                int arrayStartIndex = argsStartMatch.Index + argsStartMatch.Length;
                int end = -1;
                int depth = 0;
                bool inQuote = false;
                char quoteChar = '\0';
                for(int i = arrayStartIndex - 1; i < json.Length; i++) {
                     char c = json[i];
                     if(inQuote) { if(c==quoteChar && json[i-1]!='\\') inQuote=false; }
                     else {
                         if(c=='"'||c=='\'') { inQuote=true; quoteChar=c; }
                         else if(c=='[') depth++;
                         else if(c==']') { depth--; if(depth==0) { end=i; break; } }
                     }
                }

                if (end != -1)
                {
                    string argsContent = json.Substring(arrayStartIndex, end - arrayStartIndex);
                    action.args = ParseArgumentList(argsContent);
                }
                else action.args = new string[0];
            }
            else action.args = new string[0];

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
                    if (c == quoteChar && content[i - 1] != '\\') inQuote = false; 
                    else currentArg.Append(c);
                }
                else
                {
                    if (c == '"' || c == '\'') { inQuote = true; quoteChar = c; }
                    else if (c == ',') { list.Add(currentArg.ToString().Trim()); currentArg.Clear(); }
                    else if (!char.IsWhiteSpace(c)) currentArg.Append(c);
                }
            }
            if (currentArg.Length > 0) list.Add(currentArg.ToString().Trim());
            return list.ToArray();
        }
    }
}