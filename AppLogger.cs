using System;
using System.Text;

namespace Rapid_Downloader
{
    public static class AppLogger
    {
        private static readonly StringBuilder _logBuffer = new();
        private static readonly object _lock = new();

        public static event Action<string>? OnLogAdded;

        public static void Info(string message) => Log("INFO", message);
        public static void Warn(string message) => Log("WARN", message);
        public static void Error(string message, Exception? ex = null)
        {
            string details = ex != null ? $"{message} | Exception: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}" : message;
            Log("ERROR", details);
        }
        public static void Http(string message) => Log("HTTP", message);

        private static void Log(string level, string message)
        {
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level,-5}] {message}";
            lock (_lock)
            {
                _logBuffer.AppendLine(entry);
            }
            OnLogAdded?.Invoke(entry);
        }

        public static string GetAllLogs()
        {
            lock (_lock)
            {
                return _logBuffer.ToString();
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _logBuffer.Clear();
            }
        }
    }
}