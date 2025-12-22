using TLScope.Services;

namespace TLScope.Models;

/// <summary>
/// Aggregates all data needed for comprehensive LaTeX report generation
/// </summary>
public class ReportData
{
    // User and session information
    public User? CurrentUser { get; set; }
    public DateTime ReportGeneratedAt { get; set; } = DateTime.Now;
    public TimeSpan SessionDuration { get; set; }
    public string NetworkInterface { get; set; } = "N/A";
    public bool IsCaptureRunning { get; set; }

    // Network data
    public List<Device> Devices { get; set; } = new();
    public List<Connection> Connections { get; set; } = new();
    public List<TLSPeer> Peers { get; set; } = new();

    // Statistics
    public NetworkStatistics? Statistics { get; set; }
    public int VertexCut { get; set; }
    public Device? MostConnectedDevice { get; set; }
    public int MostConnectedCount { get; set; }

    // Configuration
    public FilterConfiguration? FilterConfig { get; set; }
    public HashSet<string> ExcludedIPs { get; set; } = new();
    public HashSet<string> ExcludedHostnames { get; set; } = new();
    public HashSet<string> ExcludedMACs { get; set; } = new();

    // Export formats
    public string DotGraphContent { get; set; } = string.Empty;

    // Calculated distributions
    public Dictionary<string, int> ProtocolDistribution { get; set; } = new();
    public Dictionary<int, int> PortDistribution { get; set; } = new();

    // Computed properties
    public int TotalDevices => Devices.Count;
    public int ActiveDevices => Devices.Count(d => d.IsActive);
    public int InactiveDevices => TotalDevices - ActiveDevices;
    public int TotalConnections => Connections.Count;
    public int ActiveConnections => Connections.Count(c => c.IsActive);
    public long TotalPackets => Devices.Sum(d => d.PacketCount);
    public long TotalBytes => Devices.Sum(d => d.BytesTransferred);
    public int TLSPeersDiscovered => Peers.Count;
    public int ConnectedPeers => Peers.Count(p => p.IsConnected);
    public double AverageConnectionsPerDevice => TotalDevices > 0 ? TotalConnections / (double)TotalDevices : 0;
}
