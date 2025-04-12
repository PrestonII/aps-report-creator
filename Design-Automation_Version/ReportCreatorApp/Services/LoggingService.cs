using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ipx.revit.reports.Services
{
    public static class LoggingService
    {
        private static readonly string _logFilePath;
        private static readonly bool _isProduction; // the app is running in production
        private static readonly bool _isDev; // the app is running on a local Revit machine
        private static readonly bool _isDebug; // the app is being tested on the client/server side
        private static string _environment = "debug";

        public static void SetEnvironment(string environment = "debug")
        {
            _environment = environment;
        }

        static LoggingService()
        {
            _isProduction = _environment?.ToLower() == "production";
            _isDev = _environment?.ToLower() == "development";
            _isDebug = _environment?.ToLower() == "debug";

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            if (_isDev)
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                _logFilePath = Path.Combine(desktopPath, $"RevitReportLog_{timestamp}.txt");
            }
            else if (_isDebug)
            {
                _logFilePath = $"RevitReportLog_{timestamp}.txt";
            }
            else if (_isProduction)
            {
                _logFilePath = $"RevitReportLog_{timestamp}.txt";
            }
            else { _logFilePath = String.Empty; }
        }

        public static void Log(string message, string level = "INFO")
        {
            try 
            { 
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logMessage = $"[{timestamp}] [IPX:] [{level}] {message}";

                // Always write to console
                Console.WriteLine(logMessage);

                // Write to file if not in production
                if (!string.IsNullOrEmpty(_logFilePath))
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
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine("Could not log the last message due to an error!");
            }
        }

        public static void LogError(string message, Exception? ex = null)
        {
            StringBuilder errorMessage = new StringBuilder(message);
            if (ex != null)
            {
                errorMessage.AppendLine($"\nException: {ex.Message}");
                errorMessage.AppendLine($"Stack Trace: {ex.StackTrace}");
            }
            Log(errorMessage.ToString(), "ERROR");
        }

        public static void LogWarning(string message)
        {
            Log(message, "WARNING");
        }

        public static void LogDebug(string message)
        {
            Log(message, "DEBUG");
        }
    }
}