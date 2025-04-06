using System;
using System.IO;
using System.Text;

namespace ipx.revit.reports.Services
{
    public class LoggingService
    {
        private readonly string _logFilePath;
        private readonly bool _isProduction;

        public LoggingService(string environment = "debug")
        {
            _isProduction = environment?.ToLower() == "production";
            
            if (!_isProduction)
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _logFilePath = Path.Combine(desktopPath, $"RevitReportLog_{timestamp}.txt");
            }
            else
            {
                _logFilePath = string.Empty;
            }
        }

        public void Log(string message, string level = "INFO")
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logMessage = $"[{timestamp}] [{level}] {message}";

            // Always write to console
            Console.WriteLine(logMessage);

            // Write to file if not in production
            if (!_isProduction && !string.IsNullOrEmpty(_logFilePath))
            {
                try
                {
                    File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to write to log file: {ex.Message}");
                }
            }
        }

        public void LogError(string message, Exception? ex = null)
        {
            StringBuilder errorMessage = new StringBuilder(message);
            if (ex != null)
            {
                errorMessage.AppendLine($"\nException: {ex.Message}");
                errorMessage.AppendLine($"Stack Trace: {ex.StackTrace}");
            }
            Log(errorMessage.ToString(), "ERROR");
        }

        public void LogWarning(string message)
        {
            Log(message, "WARNING");
        }

        public void LogDebug(string message)
        {
            Log(message, "DEBUG");
        }
    }
} 