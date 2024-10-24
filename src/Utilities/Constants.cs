using System;
using System.IO;

using TLScope.src.Debugging;

namespace TLScope.src.Utilities {
    public static class Constants {
        public const string IconArt = @"
  |-====]
    /|\
   / | \ ";
        public const string GoodbyeMessage = "Thank you for using TLScope!";
        public const string RepositoryUrl = "https://github.com/DangSage/TLScope";
    }

    /// <summary>
    /// Constants for the environment, including paths, OS dependent values, etc.
    /// </summary>
    public static class Environment {
        // Determine the OS
        public static readonly string OS = System.Environment.OSVersion.Platform.ToString();

        // Define OS-dependent AppDataPath
        public static readonly string AppDataPath = GetAppDataPath();

        private static string GetAppDataPath() {
            if (OS.Contains("Win")) {
                return Path.Combine(System.Environment.GetEnvironmentVariable("APPDATA")
                        ?? "C:\\Users\\Default\\AppData\\Roaming", "TLScope");
            } else if (OS.Contains("Unix") || OS.Contains("Linux")) {
                return Path.Combine(System.Environment.GetEnvironmentVariable("HOME")
                        ?? "", ".config", "TLScope");
            } else if (OS.Contains("Mac")) {
                return Path.Combine(System.Environment.GetEnvironmentVariable("HOME")
                        ?? "", "Library", "Application Support", "TLScope");
            } else {
                throw new PlatformNotSupportedException("Unsupported OS");
            }
        }

        public static readonly string LogPath = Path.Combine(AppDataPath, "logs");
        public static readonly string DatabasePath = Path.Combine(AppDataPath, "tlscope.db");
        public static readonly string LogFile = Path.Combine(LogPath, "tlscope.log");

        public static void SetEnvironmentVariables() {
            // Set the environment variables
            System.Environment.SetEnvironmentVariable("APPDATA", AppDataPath);
            System.Environment.SetEnvironmentVariable("TLSCOPE_LOG", LogFile);
            System.Environment.SetEnvironmentVariable("TLSCOPE_DB", DatabasePath);

            if (!Directory.Exists(AppDataPath)) {
                Directory.CreateDirectory(AppDataPath);
            }
            if (!Directory.Exists(LogPath)) {
                Directory.CreateDirectory(LogPath);
            }

            Logging.Write("Environment variables set:"
                + $"\nRunning on OS: {OS}"
                + $"\nAppDataPath: {AppDataPath}"
                + $"\nLogPath: {LogPath}"
                + $"\nDatabasePath: {DatabasePath}"
                + $"\nLogFile: {LogFile}");
        }
    }
}
