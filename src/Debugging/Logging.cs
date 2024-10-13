// TLScope writting to a log file, the log will be a class that will be written to similar to the console API in .NET

using System;
using System.IO;

namespace TLScope.src.Debugging {
    public static class Logging {
        private static string logPath = Path.Combine(Environment.CurrentDirectory, "logs");
        private static string logFile = Path.Combine(logPath, "tlscope.log");
        
        public static void Write(string message) {
            if (!Directory.Exists(logPath)) {
                Directory.CreateDirectory(logPath);
                MakeLogFileWritable();
            }

            using StreamWriter sw = new(logFile, true);  // Append mode
            sw.WriteLine($"[{DateTime.Now}] {message}");
        }

        public static void MakeLogFileWritable() {
            // Remove the ReadOnly attribute
            File.SetAttributes(logFile, File.GetAttributes(logFile) & ~FileAttributes.ReadOnly);
        }
    }
}