using System;
using System.IO;
using UnityEngine;

namespace ForgeAI
{
    public static class ForgeLogger
    {
        [Serializable]
        private class LogEntry
        {
            public string timestamp;
            public string step; // "User", "AI", "Tool", "System", "Error"
            public string content;
            public string details;
        }

        private static string GetLogPath()
        {
            // Project Root/Logs/ForgeAI/
            string root = Path.GetDirectoryName(Application.dataPath);
            string dir = Path.Combine(root, "Logs", "ForgeAI");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, $"{DateTime.Now:yyyy-MM-dd}.jsonl");
        }

        public static void Log(string step, string content, string details = "")
        {
            if (!ForgeAISettings.instance.EnableLogging) return;

            try
            {
                var entry = new LogEntry
                {
                    timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                    step = step,
                    content = content,
                    details = details
                };

                // Minimal JSON formatting
                string json = JsonUtility.ToJson(entry);
                string path = GetLogPath();
                
                // Append line
                File.AppendAllText(path, json + Environment.NewLine);
            }
            catch (Exception e)
            {
                // Fallback to console if logging fails, to warn the user
                Debug.LogWarning($"[ForgeAI] Logging failed: {e.Message}");
            }
        }
    }
}
