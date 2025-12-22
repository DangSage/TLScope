using System.Net;

namespace TLScope.Models;

/// <summary>
/// Represents another TLScope client on the network
/// </summary>
public class TLSPeer
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Username of the peer
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// IP address of the peer
    /// </summary>
    public string IPAddress { get; set; } = string.Empty;

    /// <summary>
    /// Port the peer is listening on for TLS connections
    /// </summary>
    public int Port { get; set; } = 8443;

    /// <summary>
    /// SSH public key of the peer (used for authentication)
    /// </summary>
    public string SSHPublicKey { get; set; } = string.Empty;

    /// <summary>
    /// Avatar appearance enum from appearances.json
    /// </summary>
    public string AvatarType { get; set; } = "APPEARANCE_DEFAULT";

    /// <summary>
    /// Color generated from SSH key hash (RGB hex format)
    /// </summary>
    public string AvatarColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// When this peer was first discovered
    /// </summary>
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last successful connection to this peer
    /// </summary>
    public DateTime LastConnected { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Is currently connected via TLS?
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Has this peer's identity been cryptographically verified via challenge-response?
    /// </summary>
    public bool IsVerified { get; set; }

    /// <summary>
    /// Timestamp of last successful cryptographic verification
    /// </summary>
    public DateTime? LastVerified { get; set; }

    /// <summary>
    /// Combined SSH randomart + avatar art for display
    /// Stored as raw string with newline separators
    /// </summary>
    public string? CombinedRandomartAvatar { get; set; }

    /// <summary>
    /// TLScope version the peer is running
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Number of devices this peer knows about
    /// </summary>
    public int DeviceCount { get; set; }

    public override string ToString()
    {
        return $"{Username}@{IPAddress} [{(IsConnected ? "●" : "○")}]";
    }
}
