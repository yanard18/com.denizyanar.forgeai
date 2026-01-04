using System;
using System.IO;
using UnityEngine;

namespace ForgeAI
{
    public static class ForgeLogger
    {
        private static string _currentLogPath;

        public static void StartNewSession()
        {
            if (!ForgeAISettings.instance.EnableLogging) return;

            string root = Path.GetDirectoryName(Application.dataPath);
            string dir = Path.Combine(root, "Logs", "ForgeAI");
            
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Simple format: 2023-10-27_14-30-00.log
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _currentLogPath = Path.Combine(dir, $"{timestamp}.log");
        }

        public static void LogRaw(string header, string content)
        {
            if (!ForgeAISettings.instance.EnableLogging) return;

            if (string.IsNullOrEmpty(_currentLogPath)) StartNewSession();

            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] {header}");
                sb.AppendLine("================================================================================");
                sb.AppendLine(content);
                sb.AppendLine();
                sb.AppendLine();

                File.AppendAllText(_currentLogPath, sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ForgeAI] Logging failed: {e.Message}");
            }
        }
    }
}
