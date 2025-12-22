using Serilog;
using Serilog.Events;

namespace TLScope.Utilities;

/// <summary>
/// Centralized logging manager for TLScope
/// </summary>
public static class LogManager
{
    private static readonly List<LogEntry> _recentLogs = new();
    private static readonly object _lock = new();
    private const int MaxLogEntries = 500;

    public static event EventHandler<LogEntry>? LogAdded;

    /// <summary>
    /// Initialize Serilog with proper configuration
    /// </summary>
    public static void Initialize(LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: "logs/tlscope-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10_000_000 // 10MB per file
            )
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Warning // Only show warnings+ in console
            )
            .WriteTo.Sink(new InMemoryLogSink()) // Custom sink for UI
            .CreateLogger();

        Log.Information("Logging system initialized at level: {Level}", minimumLevel);
    }

    /// <summary>
    /// Add log entry to in-memory collection
    /// </summary>
    internal static void AddLogEntry(LogEntry entry)
    {
        lock (_lock)
        {
            _recentLogs.Insert(0, entry);

            // Keep only recent logs
            if (_recentLogs.Count > MaxLogEntries)
            {
                _recentLogs.RemoveAt(_recentLogs.Count - 1);
            }
        }

        LogAdded?.Invoke(null, entry);
    }

    /// <summary>
    /// Get recent log entries
    /// </summary>
    public static List<LogEntry> GetRecentLogs(int count = 100, LogEventLevel? minimumLevel = null)
    {
        lock (_lock)
        {
            var logs = _recentLogs.Take(count);

            if (minimumLevel.HasValue)
            {
                logs = logs.Where(l => l.Level >= minimumLevel.Value);
            }

            return logs.ToList();
        }
    }

    /// <summary>
    /// Clear all in-memory logs
    /// </summary>
    public static void ClearLogs()
    {
        lock (_lock)
        {
            _recentLogs.Clear();
        }
    }

    /// <summary>
    /// Get log statistics
    /// </summary>
    public static LogStatistics GetStatistics()
    {
        lock (_lock)
        {
            return new LogStatistics
            {
                TotalLogs = _recentLogs.Count,
                ErrorCount = _recentLogs.Count(l => l.Level == LogEventLevel.Error || l.Level == LogEventLevel.Fatal),
                WarningCount = _recentLogs.Count(l => l.Level == LogEventLevel.Warning),
                InfoCount = _recentLogs.Count(l => l.Level == LogEventLevel.Information),
                DebugCount = _recentLogs.Count(l => l.Level == LogEventLevel.Debug)
            };
        }
    }

    /// <summary>
    /// Create a contextual logger
    /// </summary>
    public static ILogger ForContext<T>()
    {
        return Log.ForContext<T>();
    }

    /// <summary>
    /// Create a contextual logger with name
    /// </summary>
    public static ILogger ForContext(string name)
    {
        return Log.ForContext("SourceContext", name);
    }

    /// <summary>
    /// Shutdown logging system
    /// </summary>
    public static void Shutdown()
    {
        Log.Information("Shutting down logging system");
        Log.CloseAndFlush();
    }
}

/// <summary>
/// In-memory log entry
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogEventLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? SourceContext { get; set; }
    public Exception? Exception { get; set; }

    public string LevelIcon => Level switch
    {
        LogEventLevel.Debug => "🔍",
        LogEventLevel.Information => "ℹ️",
        LogEventLevel.Warning => "⚠️",
        LogEventLevel.Error => "❌",
        LogEventLevel.Fatal => "💀",
        _ => "•"
    };

    public override string ToString()
    {
        var contextStr = !string.IsNullOrEmpty(SourceContext) ? $"[{SourceContext}] " : "";
        return $"[{Timestamp:HH:mm:ss}] {LevelIcon} {contextStr}{Message}";
    }
}

/// <summary>
/// Log statistics
/// </summary>
public class LogStatistics
{
    public int TotalLogs { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public int DebugCount { get; set; }

    public override string ToString()
    {
        return $"Logs: {TotalLogs} (E:{ErrorCount} W:{WarningCount} I:{InfoCount} D:{DebugCount})";
    }
}

/// <summary>
/// Custom Serilog sink for in-memory storage
/// </summary>
internal class InMemoryLogSink : Serilog.Core.ILogEventSink
{
    public void Emit(Serilog.Events.LogEvent logEvent)
    {
        var entry = new LogEntry
        {
            Timestamp = logEvent.Timestamp.DateTime,
            Level = logEvent.Level,
            Message = logEvent.RenderMessage(),
            SourceContext = logEvent.Properties.ContainsKey("SourceContext")
                ? logEvent.Properties["SourceContext"].ToString().Trim('"')
                : null,
            Exception = logEvent.Exception
        };

        LogManager.AddLogEntry(entry);
    }
}
