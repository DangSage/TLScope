namespace TLScope.Models;

/// <summary>
/// Classifies the type of network connection based on routing and topology
/// </summary>
public enum ConnectionType
{
    /// <summary>
    /// Connection type not yet determined
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Direct Layer 2 connection on the same network segment
    /// Both devices are on the same LAN without routing (TTL ~64/128/255)
    /// </summary>
    DirectL2 = 1,

    /// <summary>
    /// Layer 3 routed connection through local gateway
    /// Connection goes through router but stays within local network
    /// </summary>
    RoutedL3 = 2,

    /// <summary>
    /// Connection to remote internet host
    /// Traffic routed through gateway to external networks
    /// </summary>
    Internet = 3,

    /// <summary>
    /// Special TLScope peer-to-peer connection
    /// Encrypted TLS connection between TLScope clients
    /// </summary>
    TLSPeer = 4
}
