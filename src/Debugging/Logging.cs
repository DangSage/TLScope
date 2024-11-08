using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using TLScope.src.Utilities;

namespace TLScope.src.Debugging {
    public static class Logging {
        private static readonly BlockingCollection<string> logQueue = new();
        private static readonly CancellationTokenSource cts = new();
        private static readonly Task logTask = Task.Run(ProcessLogQueue, cts.Token);
        private static readonly string logFile = Path.Combine(Utilities.Environment.LogPath, "tlscope.log");
        
        public static void Write(string message) {
            logQueue.Add($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        public static void Error(string message, Exception ex, bool isFatal = false) {
            var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] ERROR: {message}";
            logMessage += $"\n\t└> {ex.GetType().Name}: {ex.Message}\n\t└> Stack Trace: {ex.StackTrace}";
            logMessage += ex.InnerException != null ? $"\n\t└> Inner Exception: {ex.InnerException.Message}" : "";
            logQueue.Add(logMessage);

            if (isFatal) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(logMessage);
                Console.WriteLine("\nThis error is fatal. See log file for more details.");
                logQueue.Add("FATAL: CLOSING APPLICATION");
                cts.Cancel();
                try {
                    logTask.Wait();
                } catch (AggregateException ae) {
                    ae.Handle(ex => ex is TaskCanceledException);
                }
                Console.ResetColor();
                OnProcessExit(null, EventArgs.Empty);
                System.Environment.Exit(1);
            }
        }

        private static async Task ProcessLogQueue() {
            using StreamWriter sw = new(logFile, false) {
                AutoFlush = true
            };
            await sw.WriteLineAsync($"======= Logging Session Started. {DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff} =======");
            foreach (var logMessage in logQueue.GetConsumingEnumerable(cts.Token)) {
                await sw.WriteLineAsync(logMessage);
            }
        }

        private static void OnProcessExit(object? sender, EventArgs e) {
            cts.Cancel();
            try {
                logTask.Wait();
            } catch (AggregateException ae) {
                ae.Handle(ex => ex is TaskCanceledException);
            }

            using StreamWriter sw = new(logFile, true);
            sw.WriteLine($"======= Logging Session Ended. {DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff} =======");

            string latestLogFile = Path.Combine(Utilities.Environment.LogPath, $"{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}.log");
            File.Copy(logFile, latestLogFile, true);
        }

        public static void MakeLogFileWritable() {
            if (File.Exists(logFile)) {
                File.SetAttributes(logFile, FileAttributes.Normal);
            }
        }
    }
}
