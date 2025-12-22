using System.Text.Json;
using TLScope.Utilities;

namespace TLScope.Models;

/// <summary>
/// Configuration for visualization and display settings
/// </summary>
public class DisplayConfiguration
{
    private static readonly string ConfigFilePath = ConfigurationHelper.GetConfigFilePath("display.json");
    private static DisplayConfiguration? _cachedConfig = null;
    private static FileSystemWatcher? _configWatcher = null;
    private static readonly object _cacheLock = new object();

    /// <summary>
    /// Use ASCII characters for connections instead of Unicode math dots
    /// </summary>
    public bool UseAsciiConnections { get; set; } = false;

    /// <summary>
    /// Minimum edge strength (packet count) to display
    /// Edges below this threshold are hidden (unless MST, gateway, or TLS peer)
    /// </summary>
    public int MinEdgeStrengthToShow { get; set; } = 10;

    /// <summary>
    /// Load display configuration from file (cached)
    /// </summary>
    public static DisplayConfiguration Load()
    {
        lock (_cacheLock)
        {
            // Return cached config if available
            if (_cachedConfig != null)
            {
                return _cachedConfig;
            }

            // Load from file
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    _cachedConfig = JsonSerializer.Deserialize<DisplayConfiguration>(json) ?? new DisplayConfiguration();
                }
                else
                {
                    _cachedConfig = new DisplayConfiguration();
                }

                // Set up file watcher if not already initialized
                if (_configWatcher == null)
                {
                    InitializeFileWatcher();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load display configuration: {ex.Message}");
                _cachedConfig = new DisplayConfiguration();
            }

            return _cachedConfig;
        }
    }

    /// <summary>
    /// Initialize file watcher to invalidate cache on config changes
    /// </summary>
    private static void InitializeFileWatcher()
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigFilePath);
            var fileName = Path.GetFileName(ConfigFilePath);

            if (directory != null && Directory.Exists(directory))
            {
                _configWatcher = new FileSystemWatcher(directory)
                {
                    Filter = fileName,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };

                _configWatcher.Changed += (sender, e) =>
                {
                    lock (_cacheLock)
                    {
                        _cachedConfig = null; // Invalidate cache
                    }
                };

                _configWatcher.EnableRaisingEvents = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to initialize config file watcher: {ex.Message}");
        }
    }

    /// <summary>
    /// Save display configuration to file
    /// </summary>
    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigFilePath, json);

            // Update cache with current instance
            lock (_cacheLock)
            {
                _cachedConfig = this;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to save display configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Get a summary of current display settings
    /// </summary>
    public string GetSummary()
    {
        var connectionType = UseAsciiConnections
            ? "ASCII connections"
            : "Unicode math dots";

        return $"{connectionType} | Min packet threshold: {MinEdgeStrengthToShow} | Showing: important edges (MST + gateways + threshold)";
    }
}
