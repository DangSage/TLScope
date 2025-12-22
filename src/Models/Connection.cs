namespace TLScope.Models;

/// <summary>
/// Represents a network connection between two devices
/// </summary>
public class Connection
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Source device
    /// </summary>
    public int SourceDeviceId { get; set; }
    public Device SourceDevice { get; set; } = null!;

    /// <summary>
    /// Destination device
    /// </summary>
    public int DestinationDeviceId { get; set; }
    public Device DestinationDevice { get; set; } = null!;

    /// <summary>
    /// Protocol (TCP, UDP, ICMP, etc.)
    /// </summary>
    public string Protocol { get; set; } = string.Empty;

    /// <summary>
    /// Source port (if applicable)
    /// </summary>
    public int? SourcePort { get; set; }

    /// <summary>
    /// Destination port (if applicable)
    /// </summary>
    public int? DestinationPort { get; set; }

    /// <summary>
    /// First packet seen for this connection
    /// </summary>
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last packet seen for this connection
    /// </summary>
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total number of packets in this connection
    /// </summary>
    public long PacketCount { get; set; }

    /// <summary>
    /// Total bytes transferred
    /// </summary>
    public long BytesTransferred { get; set; }

    /// <summary>
    /// Number of packets seen in the last 30 seconds (for rate calculation)
    /// Reset periodically to track recent activity
    /// </summary>
    public long RecentPacketCount { get; set; }

    /// <summary>
    /// Last time packet rate was reset (for rate calculation window)
    /// </summary>
    public DateTime LastRateUpdate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Is this connection currently active? (seen within last 30 seconds)
    /// </summary>
    public bool IsActive
    {
        get
        {
            var timeSinceLastSeen = DateTime.UtcNow - LastSeen;
            return timeSinceLastSeen.TotalSeconds < 30;
        }
    }

    /// <summary>
    /// Connection state (for TCP: ESTABLISHED, SYN_SENT, etc.)
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Is this a TLS connection between TLScope peers?
    /// </summary>
    public bool IsTLSPeerConnection { get; set; }

    /// <summary>
    /// Minimum TTL (Time To Live) observed for packets in this connection
    /// Used to determine network topology and routing hops
    /// </summary>
    public int? MinTTL { get; set; }

    /// <summary>
    /// Maximum TTL observed for packets in this connection
    /// </summary>
    public int? MaxTTL { get; set; }

    /// <summary>
    /// Average TTL observed for packets in this connection
    /// Used to classify connection type (direct L2, routed L3, internet)
    /// </summary>
    public int? AverageTTL { get; set; }

    /// <summary>
    /// Number of packets with TTL data used for statistics
    /// Higher count indicates more reliable topology classification
    /// </summary>
    public int PacketCountForTTL { get; set; }

    /// <summary>
    /// Type of connection based on routing topology and TTL analysis
    /// </summary>
    public ConnectionType Type { get; set; } = ConnectionType.Unknown;

    public override string ToString()
    {
        var portInfo = SourcePort.HasValue && DestinationPort.HasValue
            ? $"{SourcePort}→{DestinationPort}"
            : string.Empty;

        return $"{SourceDevice} → {DestinationDevice} ({Protocol} {portInfo})";
    }
}
