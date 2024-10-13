using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace TLScope.src.Debugging {
    public static class Logging {
        private static readonly string logPath = Path.Combine(Environment.CurrentDirectory, "logs");
        private static readonly string logFile = Path.Combine(logPath, "tlscope.log");

        static Logging() {
            if (!Directory.Exists(logPath)) {
                Directory.CreateDirectory(logPath);
                MakeLogFileWritable();
            }
            using StreamWriter sw = new(logFile, true);  // Append mode
            sw.WriteLine($"\n\n======= Logging Session. {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} =======");
        }

        public static void Write(string message, 
                                 [CallerFilePath] string filePath = "", 
                                 [CallerLineNumber] int lineNumber = 0, 
                                 [CallerMemberName] string memberName = "") {
            string fileName = Path.GetFileName(filePath);
            string logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {fileName}:{lineNumber} ({memberName}) - {message}";
            using StreamWriter sw = new(logFile, true);  // Append mode
            sw.WriteLine(logMessage);
        }

        public static void Error(string message, Exception? ex = null, bool isFatal = false,
                                 [CallerFilePath] string filePath = "",
                                 [CallerLineNumber] int lineNumber = 0,
                                 [CallerMemberName] string memberName = "") {
            string fileName = Path.GetFileName(filePath);
            string logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] ERROR was caught @ {fileName}:{lineNumber} ({memberName}) - '{message}'";
            if (ex != null) {
                logMessage += $"\n\t└> {ex.GetType().Name}: {ex.Message}\n\t└> Stack Trace: {ex.StackTrace}";
                // full exception details
                logMessage += ex.InnerException != null ? $"\n\t└> Inner Exception: {ex.InnerException.Message}" : "";
            }

            using StreamWriter sw = new(logFile, true);  // Append mode
            sw.WriteLine(logMessage);
            sw.Flush();

            if (isFatal) {
                Console.ForegroundColor = ConsoleColor.Red; // Set text color to red
                Console.WriteLine(logMessage);
                Console.WriteLine("\nThis error is fatal. See log file for more details.");
                sw.WriteLine($"FATAL: CLOSING APPLICATION");
                sw.Flush();
                sw.Close();
                Console.ResetColor(); // Reset text color to default
                Environment.Exit(1);
            }
            sw.Close();
        }

        public static void MakeLogFileWritable() {
            // Remove the ReadOnly attribute
            if (File.Exists(logFile)) {
                File.SetAttributes(logFile, FileAttributes.Normal);
            }
        }
    }
}