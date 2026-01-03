using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ForgeAI
{
    public static class BasicTools
    {
        private static string NormalizePath(string path)
        {
            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets") && !path.StartsWith("Packages") && !File.Exists(path) && !Directory.Exists(path))
            {
                string assetsPath = Path.Combine("Assets", path);
                if (File.Exists(assetsPath) || Directory.Exists(assetsPath)) path = assetsPath;
            }
            return path;
        }

        [ForgeTool("Reads the text content of a file.", "path:string", requiresApproval: false)]
        public static string ReadFile(string path)
        {
            path = NormalizePath(path);
            if (!File.Exists(path)) return $"Error: File not found at '{path}'.";
            try
            {
                string text = File.ReadAllText(path);
                if (text.Length > 12000) return text.Substring(0, 12000) + "\n... [Content Truncated]";
                return text;
            }
            catch (System.Exception e) { return $"Error: {e.Message}"; }
        }

        [ForgeTool("Writes or creates a file with content.", "path:string, content:string")]
        public static string WriteFile(string path, string content)
        {
            path = NormalizePath(path);
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, content);
                AssetDatabase.ImportAsset(path);
                return $"Success: Wrote to '{path}'.";
            }
            catch (System.Exception e) { return $"Error: {e.Message}"; }
        }

        [ForgeTool("Lists files and directories. Ignores .meta files and system folders.", "path:string, recursive:bool", requiresApproval: false)]
        public static string ListFiles(string path, bool recursive = false)
        {
            path = NormalizePath(path);
            if (!Directory.Exists(path)) return $"Error: Directory '{path}' not found.";

            try
            {
                var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var allFiles = Directory.GetFiles(path, "*", option)
                    .Where(f => !f.EndsWith(".meta") && !f.Contains("/Library/") && !f.Contains("/Temp/"))
                    .Select(f => f.Replace('\\', '/'));

                var allDirs = Directory.GetDirectories(path, "*", option)
                    .Where(d => !d.Contains("/Library/") && !d.Contains("/Temp/"))
                    .Select(d => d.Replace('\\', '/'));

                return $"Contents of '{path}':\n[Directories]\n{string.Join("\n", allDirs)}\n[Files]\n{string.Join("\n", allFiles)}";
            }
            catch (System.Exception e) { return $"Error: {e.Message}"; }
        }

        [ForgeTool("Searches for a string pattern within text files in a directory.", "path:string, pattern:string", requiresApproval: false)]
        public static string SearchFiles(string path, string pattern)
        {
            path = NormalizePath(path);
            if (!Directory.Exists(path)) return $"Error: Directory '{path}' not found.";

            try
            {
                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".meta") && !f.Contains("/Library/") && !f.Contains("/Temp/"));

                var results = new List<string>();
                int matchCount = 0;

                foreach (var file in files)
                {
                    // Basic binary check
                    if (file.EndsWith(".png") || file.EndsWith(".jpg") || file.EndsWith(".fbx")) continue;

                    string[] lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains(pattern, System.StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add($"{file}:{i + 1}: {lines[i].Trim()}");
                            matchCount++;
                            if (matchCount > 50) break;
                        }
                    }
                    if (matchCount > 50) break;
                }

                if (results.Count == 0) return "No matches found.";
                return $"Found {matchCount} matches:\n" + string.Join("\n", results);
            }
            catch (System.Exception e) { return $"Error: {e.Message}"; }
        }

        [ForgeTool("Replaces a specific text block in a file with new text. REQUIREMENT: oldText must match exactly.", "path:string, oldText:string, newText:string")]
        public static string ReplaceText(string path, string oldText, string newText)
        {
            path = NormalizePath(path);
            if (!File.Exists(path)) return $"Error: File not found at '{path}'.";

            try
            {
                string content = File.ReadAllText(path);
                if (!content.Contains(oldText)) return "Error: Could not find exact match for oldText. Replacement failed.";

                string newContent = content.Replace(oldText, newText);
                File.WriteAllText(path, newContent);
                AssetDatabase.ImportAsset(path);
                return $"Success: Replaced text in '{path}'.";
            }
            catch (System.Exception e) { return $"Error: {e.Message}"; }
        }

        [ForgeTool("Moves or renames an asset/file using Unity's AssetDatabase (preserves meta files). Auto-creates directories.", "source:string, destination:string")]
        public static string MoveAsset(string source, string destination)
        {
            source = NormalizePath(source);
            destination = NormalizePath(destination);

            try
            {
                string targetDir = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                    AssetDatabase.Refresh();
                }

                string error = AssetDatabase.MoveAsset(source, destination);
                if (!string.IsNullOrEmpty(error)) return $"Error: {error}";
                
                return $"Success: Moved '{source}' to '{destination}'.";
            }
            catch (System.Exception e) { return $"Error: {e.Message}"; }
        }

        [ForgeTool("Deletes a file or directory using Unity's AssetDatabase.", "path:string")]
        public static string DeleteAsset(string path)
        {
            path = NormalizePath(path);
            if (AssetDatabase.DeleteAsset(path)) return $"Success: Deleted '{path}'.";
            return $"Error: Failed to delete '{path}'. Check if it exists.";
        }

        [ForgeTool("Creates a directory.", "path:string")]
        public static string CreateDirectory(string path)
        {
            path = NormalizePath(path);
            if (Directory.Exists(path)) return $"Info: Directory '{path}' already exists.";
            
            try
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
                return $"Success: Created directory '{path}'.";
            }
            catch (System.Exception e) { return $"Error: {e.Message}"; }
        }

        [ForgeTool("Moves multiple assets to a single directory.", "sources:string, targetDirectory:string") { UsageInstructions = "sources is a semi-colon separated string (e.g., 'path1;path2;path3')." }]
        public static string BatchMove(string sources, string targetDirectory)
        {
            targetDirectory = NormalizePath(targetDirectory);
            if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);

            string[] sourcePaths = sources.Split(';', System.StringSplitOptions.RemoveEmptyEntries);
            int successCount = 0;
            List<string> errors = new List<string>();

            foreach (var src in sourcePaths)
            {
                string s = NormalizePath(src.Trim());
                string fileName = Path.GetFileName(s);
                string dest = Path.Combine(targetDirectory, fileName).Replace('\\', '/');

                string err = AssetDatabase.MoveAsset(s, dest);
                if (string.IsNullOrEmpty(err)) successCount++;
                else errors.Add($"{s}: {err}");
            }

            AssetDatabase.Refresh();
            string result = $"Moved {successCount}/{sourcePaths.Length} assets.";
            if (errors.Count > 0) result += "\nErrors:\n" + string.Join("\n", errors);
            return result;
        }

        [ForgeTool("Renames multiple assets.", "renamesJson:string") { UsageInstructions = "renamesJson format is '[{\"p\":\"old/path\",\"n\":\"newName\"},...]'. Minimize JSON to save tokens." }]
        public static string BatchRename(string renamesJson)
        {
            try
            {
                // Simple manual parsing to avoid heavy dependencies, 
                // but since we are in Unity, we might use JsonUtility if we have a wrapper.
                // For a CLI agent, a simple regex or targeted split is safer if format is strict.
                // However, let's assume the agent can output a clean format.
                
                var matches = Regex.Matches(renamesJson, @"\{""p"":""([^""]+)"",""n"":""([^""]+)""\}");
                int successCount = 0;
                List<string> errors = new List<string>();

                foreach (Match m in matches)
                {
                    string path = NormalizePath(m.Groups[1].Value);
                    string newName = m.Groups[2].Value;

                    string err = AssetDatabase.RenameAsset(path, newName);
                    if (string.IsNullOrEmpty(err)) successCount++;
                    else errors.Add($"{path}: {err}");
                }

                AssetDatabase.Refresh();
                return $"Renamed {successCount} assets. {(errors.Count > 0 ? "\nErrors: " + string.Join(", ", errors) : "")}";
            }
            catch (System.Exception e) { return $"Error: {e.Message}"; }
        }
    }
}
