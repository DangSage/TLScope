using System.Net.NetworkInformation;

namespace TLScope.Services;

/// <summary>
/// Interface for network scanning services
/// Provides active network discovery using ICMP ping sweeps
/// </summary>
public interface INetworkScanService
{
    /// <summary>
    /// Event raised when a device responds to a ping
    /// </summary>
    event EventHandler<(string ipAddress, PingReply reply)>? DeviceResponded;

    /// <summary>
    /// Event raised when a network scan completes
    /// </summary>
    event EventHandler<NetworkScanResult>? ScanCompleted;

    /// <summary>
    /// Performs ICMP ping sweep on a subnet to discover active devices
    /// </summary>
    /// <param name="subnet">Base IP address (e.g., "192.168.1")</param>
    /// <param name="startHost">Starting host number (default: 1)</param>
    /// <param name="endHost">Ending host number (default: 254)</param>
    /// <returns>List of responsive IP addresses</returns>
    Task<List<string>> PingSweepAsync(string subnet, int startHost = 1, int endHost = 254);

    /// <summary>
    /// Auto-detect local subnet from network interface and scan it
    /// </summary>
    /// <param name="interfaceName">Optional network interface name</param>
    /// <returns>List of responsive IP addresses</returns>
    Task<List<string>> ScanLocalNetworkAsync(string? interfaceName = null);

    /// <summary>
    /// Gets the subnet address from a network interface
    /// </summary>
    /// <param name="interfaceName">Optional network interface name (uses first active if null)</param>
    /// <returns>Subnet base address (e.g., "192.168.1") or null if unable to detect</returns>
    string? GetLocalSubnet(string? interfaceName = null);
}

/// <summary>
/// Result of a network scan operation
/// </summary>
public class NetworkScanResult
{
    /// <summary>
    /// List of IP addresses that responded to ping
    /// </summary>
    public List<string> ResponsiveHosts { get; set; } = new();

    /// <summary>
    /// Total number of hosts scanned
    /// </summary>
    public int TotalScanned { get; set; }

    /// <summary>
    /// Duration of the scan
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Subnet that was scanned
    /// </summary>
    public string Subnet { get; set; } = string.Empty;
}
