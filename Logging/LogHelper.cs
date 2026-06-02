using System;
using System.IO;
using Newtonsoft.Json;

namespace HandoffExporter.Logging
{
    public class LogHelper : ILogHelper
    {
        private readonly string _logFilePath = "logs.json";

        public void Info(string message, params object[] args)
        {
            var formatted = string.Format(message, args);
            Console.WriteLine("[INFO] " + formatted);
            LogToFile("INFO", formatted);
        }

        public void Warn(string message, params object[] args)
        {
            var formatted = string.Format(message, args);
            Console.WriteLine("[WARN] " + formatted);
            LogToFile("WARN", formatted);
        }

        public void Error(string message, params object[] args)
        {
            var formatted = string.Format(message, args);
            Console.WriteLine("[ERROR] " + formatted);
            LogToFile("ERROR", formatted);
        }

        private void LogToFile(string level, string message)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Message = message
            };
            var json = JsonConvert.SerializeObject(entry) + Environment.NewLine;
            File.AppendAllText(_logFilePath, json);
        }
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
    }
}