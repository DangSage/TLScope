using Serilog;

namespace TLScope.Utilities;

/// <summary>
/// Helper utility for managing OS-specific configuration file paths
/// </summary>
public static class ConfigurationHelper
{
    private static readonly string ConfigDirectoryName = "tlscope";
    private static string? _configDirectory;

    /// <summary>
    /// Get the OS-specific configuration directory for TLScope
    /// Creates the directory if it doesn't exist
    /// </summary>
    /// <returns>Full path to config directory</returns>
    public static string GetConfigDirectory()
    {
        if (_configDirectory != null)
            return _configDirectory;

        try
        {
            // Determine OS-specific config path
            string baseConfigPath;

            if (OperatingSystem.IsWindows())
            {
                // Windows: %APPDATA%\TLScope\
                baseConfigPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _configDirectory = Path.Combine(baseConfigPath, "TLScope");
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                // Linux/macOS: ~/.config/tlscope/
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var configDir = Path.Combine(homeDir, ".config");
                _configDirectory = Path.Combine(configDir, ConfigDirectoryName);
            }
            else
            {
                // Fallback: use current directory
                Log.Warning("Unknown operating system, using current directory for config");
                _configDirectory = Directory.GetCurrentDirectory();
                return _configDirectory;
            }

            // Create directory if it doesn't exist
            if (!Directory.Exists(_configDirectory))
            {
                Directory.CreateDirectory(_configDirectory);
                Log.Information($"Created configuration directory: {_configDirectory}");
            }

            return _configDirectory;
        }
        catch (Exception ex)
        {
            // If we can't access the OS-specific directory, fall back to current directory
            Log.Warning($"Failed to create config directory: {ex.Message}. Using current directory as fallback.");
            _configDirectory = Directory.GetCurrentDirectory();
            return _configDirectory;
        }
    }

    /// <summary>
    /// Get the full path for a configuration file
    /// </summary>
    /// <param name="filename">Name of the config file (e.g., "filters.json")</param>
    /// <returns>Full path to the config file</returns>
    public static string GetConfigFilePath(string filename)
    {
        var configDir = GetConfigDirectory();
        return Path.Combine(configDir, filename);
    }

    /// <summary>
    /// Migrate an old config file from the working directory to the new config directory
    /// </summary>
    /// <param name="oldFilename">Old filename in working directory</param>
    /// <param name="newFilename">New filename in config directory</param>
    /// <returns>True if migration occurred, false if file didn't exist or already migrated</returns>
    public static bool MigrateConfigFile(string oldFilename, string newFilename)
    {
        try
        {
            var oldPath = Path.Combine(Directory.GetCurrentDirectory(), oldFilename);
            var newPath = GetConfigFilePath(newFilename);

            // Only migrate if old file exists and new file doesn't
            if (File.Exists(oldPath) && !File.Exists(newPath))
            {
                File.Copy(oldPath, newPath);
                Log.Information($"Migrated config file: {oldFilename} -> {newPath}");

                // Optionally delete the old file (commented out for safety)
                // File.Delete(oldPath);

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to migrate config file {oldFilename}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get a display-friendly path for logging (replaces home directory with ~)
    /// </summary>
    /// <param name="fullPath">Full path to format</param>
    /// <returns>Formatted path string</returns>
    public static string GetDisplayPath(string fullPath)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (fullPath.StartsWith(homeDir))
            {
                return fullPath.Replace(homeDir, "~");
            }
        }

        return fullPath;
    }
}
