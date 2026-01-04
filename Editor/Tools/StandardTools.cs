using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ForgeAI
{
    // ----------------------------------------------------------------------- 
    // FILE MANAGEMENT
    // ----------------------------------------------------------------------- 

    [ForgeTool("ListFiles", "Lists files and directories.", requiresApproval: false)]
    public class ListFilesTool : IForgeTool
    {
        public string GetPromptDefinition()
        {
            return @"### ListFiles
Description: Lists files and directories in the project. Ignores .meta files.
Usage:
Path: <directory_path> (Optional: add 'Recursive: true' on next line)";
        }

        public string Execute(string rawInput)
        {
            try
            {
                var lines = rawInput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                string path = ".";
                bool recursive = false;

                foreach (var line in lines)
                {
                    if (line.StartsWith("Path:", StringComparison.OrdinalIgnoreCase))
                        path = line.Substring(5).Trim();
                    else if (line.StartsWith("Recursive:", StringComparison.OrdinalIgnoreCase))
                        bool.TryParse(line.Substring(10).Trim(), out recursive);
                    else if (path == "." && !line.Contains(":")) 
                        path = line.Trim();
                }

                path = ToolUtils.NormalizePath(path);
                if (!Directory.Exists(path)) return $"Error: Directory '{{path}}' not found.";

                var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                
                // Get Dirs
                var allDirs = Directory.GetDirectories(path, "*", option)
                    .Where(d => !d.Contains("/Library/") && !d.Contains("/Temp/") && !d.Contains("/.git/"))
                    .Select(d => d.Replace('\\', '/'))
                    .ToList();

                // Get Files
                var allFiles = Directory.GetFiles(path, "*", option)
                    .Where(f => !f.EndsWith(".meta") && !f.Contains("/Library/") && !f.Contains("/Temp/") && !f.Contains("/.git/"))
                    .Select(f => f.Replace('\\', '/'))
                    .ToList();

                if (allDirs.Count == 0 && allFiles.Count == 0) return $"Directory '{{path}}' is empty.";

                return $"Contents of '{{path}}':\n[Dirs]\n{string.Join("\n", allDirs)}\n[Files]\n{string.Join("\n", allFiles)}";
            }
            catch (Exception e) { return $"Error: {{e.Message}}"; }
        }
    }

    [ForgeTool("MoveAsset", "Moves or Renames an asset.")]
    public class MoveAssetTool : IForgeTool
    {
        public string GetPromptDefinition()
        {
            return @"### MoveAsset
Description: Moves or Renames an asset. Use this for RENAME by keeping the same directory but changing the filename.
Usage:
From: Assets/Models/OldName.fbx
To: Assets/Models/NewName.fbx";
        }

        public string Execute(string rawInput)
        {
            try
            {
                using (var reader = new StringReader(rawInput))
                {
                    string fromPath = null;
                    string toPath = null;
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (line.StartsWith("From:", StringComparison.OrdinalIgnoreCase))
                            fromPath = line.Substring(5).Trim();
                        else if (line.StartsWith("To:", StringComparison.OrdinalIgnoreCase))
                            toPath = line.Substring(3).Trim();
                    }

                    if (string.IsNullOrEmpty(fromPath) || string.IsNullOrEmpty(toPath))
                        return "Error: Missing 'From:' or 'To:' arguments.";

                    fromPath = ToolUtils.NormalizePath(fromPath);
                    // Do NOT normalize 'To' indiscriminately, as we might be creating a new path.
                    // But we should ensure it uses forward slashes.
                    toPath = toPath.Replace('\\', '/').Trim(); 
                    if (toPath.StartsWith("/") || toPath.StartsWith("\\")) toPath = toPath.Substring(1);
                    if (!toPath.StartsWith("Assets") && !toPath.StartsWith("Packages")) toPath = Path.Combine("Assets", toPath);

                    if (!File.Exists(fromPath) && !Directory.Exists(fromPath))
                        return $"Error: Source '{{fromPath}}' does not exist.";

                    // Ensure destination folder exists
                    string destDir = Path.GetDirectoryName(toPath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                        AssetDatabase.Refresh();
                    }

                    string error = AssetDatabase.MoveAsset(fromPath, toPath);
                    if (!string.IsNullOrEmpty(error)) return $"Error: {{error}}";

                    return $"Success: Moved/Renamed '{{fromPath}}' to '{{toPath}}'.";
                }
            }
            catch (Exception e) { return $"Error: {{e.Message}}"; }
        }
    }

    [ForgeTool("DeleteAsset", "Deletes a file or directory.")]
    public class DeleteAssetTool : IForgeTool
    {
        public string GetPromptDefinition()
        {
            return @"### DeleteAsset
Description: Deletes a file or directory.
Usage:
<relative_path>";
        }

        public string Execute(string rawInput)
        {
            try
            {
                string path = ToolUtils.NormalizePath(rawInput.Trim());
                if (AssetDatabase.DeleteAsset(path)) return $"Success: Deleted '{{path}}'.";
                return $"Error: Failed to delete '{{path}}' (Check if it exists).";
            }
            catch (Exception e) { return $"Error: {{e.Message}}"; }
        }
    }

    // ----------------------------------------------------------------------- 
    // CONTENT EDITING
    // ----------------------------------------------------------------------- 

    [ForgeTool("ReadFile", "Reads text content.", requiresApproval: false)]
    public class ReadFileTool : IForgeTool
    {
        public string GetPromptDefinition()
        {
            return @"### ReadFile
Description: Reads the text content of a file. Truncates large files.
Usage:
<relative_path>";
        }

        public string Execute(string rawInput)
        {
            try
            {
                string path = ToolUtils.NormalizePath(rawInput.Trim());
                
                if (!File.Exists(path)) return $"Error: File not found at '{{path}}'.";
                
                // Guard against binary files to prevent garbage output
                if (ToolUtils.IsBinaryFile(path)) 
                    return $"Error: '{{path}}' appears to be a binary file. Cannot read text content.";

                string text = File.ReadAllText(path);
                if (text.Length > 12000) return text.Substring(0, 12000) + "\n... [Content Truncated]";
                return text;
            }
            catch (Exception e) { return $"Error: {{e.Message}}"; }
        }
    }

    [ForgeTool("WriteFile", "Writes content to a file.")]
    public class WriteFileTool : IForgeTool
    {
        public string GetPromptDefinition()
        {
            return @"### WriteFile
Description: Writes or Overwrites a file with specific content. Creates directories if needed.
Usage:
Path: <relative_path>
Content:
<file_content>";
        }

        public string Execute(string rawInput)
        {
            try
            {
                using (var reader = new StringReader(rawInput))
                {
                    string firstLine = reader.ReadLine();
                    string path = null;

                    if (firstLine != null && firstLine.StartsWith("Path:", StringComparison.OrdinalIgnoreCase))
                        path = firstLine.Substring(5).Trim();
                    else
                        return "Error: Input must start with 'Path: <path>'.";

                    string line = reader.ReadLine();
                    while (line != null && string.IsNullOrWhiteSpace(line)) line = reader.ReadLine(); 

                    if (line == null || !line.StartsWith("Content:", StringComparison.OrdinalIgnoreCase))
                        return "Error: Missing 'Content:' marker.";

                    string content = reader.ReadToEnd();
                    if (content.StartsWith("\n")) content = content.Substring(1);
                    if (content.StartsWith("\r\n")) content = content.Substring(2);

                    path = ToolUtils.NormalizePath(path);
                    
                    if (ToolUtils.IsBinaryFile(path))
                        return $"Error: Cannot write text to binary file '{{path}}'.";

                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    
                    File.WriteAllText(path, content);
                    AssetDatabase.ImportAsset(path);
                    return $"Success: Wrote to '{{path}}'.";
                }
            }
            catch (Exception e) { return $"Error: {{e.Message}}"; }
        }
    }

    [ForgeTool("ReplaceText", "Replaces text in a file.")]
    public class ReplaceTextTool : IForgeTool
    {
        public string GetPromptDefinition()
        {
            return @"### ReplaceText
Description: Replaces a text block in a file. Old text must match EXACTLY.
Usage:
Path: <path>
Old:
<exact_old_text>
New:
<new_text>";
        }

        public string Execute(string rawInput)
        {
            try
            {
                using (var reader = new StringReader(rawInput))
                {
                    string pathLine = reader.ReadLine();
                    if (pathLine == null || !pathLine.StartsWith("Path:", StringComparison.OrdinalIgnoreCase))
                        return "Error: Input must start with 'Path: <path>'";

                    string path = ToolUtils.NormalizePath(pathLine.Substring(5).Trim());

                    // Guard
                    if (ToolUtils.IsBinaryFile(path))
                        return $"Error: Cannot replace text in binary file '{{path}}'.";

                    string line = reader.ReadLine();
                    while (line != null && string.IsNullOrWhiteSpace(line)) line = reader.ReadLine();
                    
                    if (line == null || !line.Equals("Old:", StringComparison.OrdinalIgnoreCase))
                        return "Error: Missing 'Old:' marker.";

                    var oldSb = new System.Text.StringBuilder();
                    line = reader.ReadLine();
                    while (line != null)
                    {
                        if (line.Equals("New:", StringComparison.OrdinalIgnoreCase)) break;
                        oldSb.AppendLine(line);
                        line = reader.ReadLine();
                    }

                    if (line == null) return "Error: Missing 'New:' marker.";

                    string oldText = oldSb.ToString().TrimEnd('\r', '\n'); 
                    string newText = reader.ReadToEnd();
                    if (newText.StartsWith("\n")) newText = newText.Substring(1);
                    else if (newText.StartsWith("\r\n")) newText = newText.Substring(2);

                    if (!File.Exists(path)) return $"Error: File '{{path}}' not found.";
                    
                    string content = File.ReadAllText(path);
                    if (!content.Contains(oldText)) 
                    {
                        return $"Error: Could not find exact match for Old text in '{{path}}'.";
                    }

                    string newContent = content.Replace(oldText, newText);
                    File.WriteAllText(path, newContent);
                    AssetDatabase.ImportAsset(path);
                    return $"Success: Replaced text in '{{path}}'.";
                }
            }
            catch (Exception e) { return $"Error: {{e.Message}}"; }
        }
    }

    internal static class ToolUtils
    {
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            path = path.Replace('\\', '/').Trim();
            
            if (!path.StartsWith("Assets") && !path.StartsWith("Packages") && !File.Exists(path) && !Directory.Exists(path))
            {
                string assetsPath = Path.Combine("Assets", path);
                if (File.Exists(assetsPath) || Directory.Exists(assetsPath)) return assetsPath;
            }
            return path;
        }

        private static readonly HashSet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".js", ".shader", ".json", ".xml", ".txt", ".md", ".uss", ".uxml", ".yaml", ".html", ".css", ".meta", ".ini", ".config", ".csv"
        };

        public static bool IsBinaryFile(string path)
        {
            string ext = Path.GetExtension(path);
            return !TextExtensions.Contains(ext);
        }
    }
}