using System.Net;
using TLScope.Utilities;

namespace TLScope.Models;

/// <summary>
/// Represents a network device discovered through packet capture
/// </summary>
public class Device
{
    /// <summary>
    /// Unique identifier for the device
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// MAC address (unique hardware identifier)
    /// </summary>
    public string MACAddress { get; set; } = string.Empty;

    /// <summary>
    /// IP address (can change over time)
    /// </summary>
    public string IPAddress { get; set; } = string.Empty;

    /// <summary>
    /// Hostname if discovered (via DNS, mDNS, NetBIOS)
    /// </summary>
    public string? Hostname { get; set; }

    /// <summary>
    /// Device name (user-friendly name if available)
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// Operating system fingerprint (if detected)
    /// </summary>
    public string? OperatingSystem { get; set; }

    /// <summary>
    /// Vendor information from MAC OUI lookup
    /// </summary>
    public string? Vendor { get; set; }

    /// <summary>
    /// First time this device was seen on the network
    /// </summary>
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last time this device sent/received traffic
    /// </summary>
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Is this device currently active based on time since last seen?
    /// Gateways stay active for 30 minutes, TLS peers for 20 minutes, local devices for 15 minutes, remote for 10 minutes
    /// </summary>
    public bool IsActive
    {
        get
        {
            var timeSinceLastSeen = DateTime.UtcNow - LastSeen;

            // Gateways are critical infrastructure - keep them visible longest
            if (IsGateway)
                return timeSinceLastSeen.TotalMinutes < 30;

            // TLS peers are important connections
            if (IsTLScopePeer)
                return timeSinceLastSeen.TotalMinutes < 20;

            // Local devices stay longer than remote
            if (IsLocal)
                return timeSinceLastSeen.TotalMinutes < 15;

            // Remote/Internet devices
            return timeSinceLastSeen.TotalMinutes < 10;
        }
    }

    /// <summary>
    /// Is this device currently active? (hybrid check: time-based OR has active connections)
    /// </summary>
    public bool IsActiveHybrid(IEnumerable<Connection> allConnections)
    {
        // Time-based check (existing logic)
        if (IsActive)
            return true;

        // Connection-based check: device is active if it has at least one active connection
        return allConnections.Any(c =>
            (c.SourceDeviceId == Id || c.DestinationDeviceId == Id) &&
            c.IsActive);
    }

    /// <summary>
    /// Total number of packets sent/received
    /// </summary>
    public long PacketCount { get; set; }

    /// <summary>
    /// Total bytes transferred
    /// </summary>
    public long BytesTransferred { get; set; }

    /// <summary>
    /// Open ports discovered through traffic analysis (denormalized for performance)
    /// </summary>
    public List<int> OpenPorts { get; set; } = new();

    /// <summary>
    /// Detailed port information (normalized)
    /// </summary>
    public List<DevicePort> Ports { get; set; } = new();

    /// <summary>
    /// Is this device a TLScope peer client?
    /// </summary>
    public bool IsTLScopePeer { get; set; }

    /// <summary>
    /// Connection to TLSPeer if this is a peer
    /// </summary>
    public int? TLSPeerId { get; set; }
    public TLSPeer? TLSPeer { get; set; }

    /// <summary>
    /// Is this device a network gateway/router?
    /// </summary>
    public bool IsGateway { get; set; }

    /// <summary>
    /// Is this the default gateway for the network?
    /// </summary>
    public bool IsDefaultGateway { get; set; }

    /// <summary>
    /// Role of this gateway (e.g., "Default", "Secondary", "VPN")
    /// </summary>
    public string? GatewayRole { get; set; }

    /// <summary>
    /// Number of network hops from the local device (derived from TTL analysis)
    /// 0 = same device, 1 = directly connected, 2+ = routed
    /// </summary>
    public int? HopCount { get; set; }

    /// <summary>
    /// Is this device on the local network? (RFC1918 private, loopback, link-local)
    /// Computed from IPAddress - true for private/local addresses, false for public internet
    /// </summary>
    public bool IsLocal => !string.IsNullOrEmpty(IPAddress) &&
                           IPAddressValidator.IsLocalAddress(IPAddress);

    /// <summary>
    /// Is this a virtual device (remote host without local MAC)?
    /// Virtual devices represent internet hosts accessed through gateways
    /// </summary>
    public bool IsVirtualDevice => MACAddress?.StartsWith("virtual-",
                                   StringComparison.OrdinalIgnoreCase) ?? false;

    public override string ToString()
    {
        return $"{DeviceName ?? Hostname ?? IPAddress} ({MACAddress})";
    }
}
