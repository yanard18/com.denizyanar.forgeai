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

        private static string _currentLogFilePath;

        public static void StartNewSession()
        {
            // Project Root/Logs/ForgeAI/yyyy-MM-dd/
            string root = Path.GetDirectoryName(Application.dataPath);
            string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
            string dir = Path.Combine(root, "Logs", "ForgeAI", dateFolder);
            
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // File format: HH-mm-ss_Request.log
            string timestamp = DateTime.Now.ToString("HH-mm-ss");
            _currentLogFilePath = Path.Combine(dir, $"{timestamp}_Request.log");
        }

        public static void Log(string step, string content, string details = "")
        {
            if (!ForgeAISettings.instance.EnableLogging) return;

            try
            {
                // Ensure we have a valid log file path (fallback if StartNewSession wasn't called explicitely)
                if (string.IsNullOrEmpty(_currentLogFilePath))
                {
                    StartNewSession();
                }

                var sb = new System.Text.StringBuilder();

                sb.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] [{step}]");
                sb.AppendLine(content);
                
                if (!string.IsNullOrEmpty(details))
                {
                    sb.AppendLine("--- Details ---");
                    sb.AppendLine(details);
                }
                
                sb.AppendLine("------------------------------------------------------------------------------------------------");
                sb.AppendLine();

                File.AppendAllText(_currentLogFilePath, sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ForgeAI] Logging failed: {e.Message}");
            }
        }
    }
}
