namespace TLScope.Models;

/// <summary>
/// Classifies the type of network connection based on routing, topology, and TTL analysis
/// </summary>
public enum ConnectionType
{
    /// <summary>
    /// Connection type not yet determined
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Direct Layer 2 connection on the same network segment (same subnet)
    ///
    /// Characteristics:
    /// - Both devices on same LAN without intermediate routers
    /// - Hop count: 0-1 (direct or through a switch)
    /// - Destination has private IP address (RFC1918)
    /// - High TTL values indicating minimal hops
    ///
    /// Examples:
    /// - Computer to printer on same network
    /// - Laptop to local file server
    /// - Device to local IoT device
    /// </summary>
    DirectL2 = 1,

    /// <summary>
    /// Layer 3 routed connection through local gateway (different private subnet)
    ///
    /// Characteristics:
    /// - Connection routed through local router/gateway
    /// - Stays within private network (no internet traversal)
    /// - Hop count: 2-10 hops through local infrastructure
    /// - Both source and destination have private IP addresses
    /// - Medium TTL values indicating some routing
    ///
    /// Examples:
    /// - Office PC to server in different VLAN
    /// - Device connecting through multiple local routers
    /// - Access to resources in different building on campus network
    /// </summary>
    RoutedL3 = 2,

    /// <summary>
    /// Connection to remote internet host (external/public network)
    ///
    /// Characteristics:
    /// - Destination has public IP address, OR
    /// - Virtual device created for remote host accessed through gateway, OR
    /// - Hop count > 10 (even for private IPs, suggests VPN or complex routing)
    /// - Traffic typically routed through ISP gateway
    /// - Low TTL values indicating many hops
    ///
    /// Examples:
    /// - Web browsing to google.com
    /// - API calls to cloud services
    /// - VPN connections to remote networks
    /// </summary>
    Internet = 3,

    /// <summary>
    /// Special TLScope peer-to-peer connection (TLS-encrypted mesh network)
    ///
    /// Characteristics:
    /// - Encrypted TLS connection on port 8443
    /// - Direct communication between TLScope instances
    /// - Used for peer discovery and graph synchronization
    /// - Authenticated via SSH key signatures
    ///
    /// Takes priority over other connection types when detected.
    /// </summary>
    TLSPeer = 4
}
