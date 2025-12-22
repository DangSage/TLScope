using TLScope.Models;
using System.Net;

namespace TLScope.Services;

/// <summary>
/// Interface for gateway detection services
/// </summary>
public interface IGatewayDetectionService
{
    /// <summary>
    /// Detect the default gateway from system routing table
    /// </summary>
    /// <returns>Default gateway IP address, or null if not found</returns>
    IPAddress? DetectDefaultGateway();

    /// <summary>
    /// Get all gateways from the routing table
    /// </summary>
    /// <returns>List of (gateway IP, network IP, netmask) tuples</returns>
    List<(IPAddress Gateway, IPAddress Network, IPAddress Netmask)> GetRoutingTable();

    /// <summary>
    /// Match gateway IP addresses to discovered devices
    /// </summary>
    /// <param name="devices">List of discovered devices</param>
    /// <returns>List of devices that are identified as gateways</returns>
    List<Device> IdentifyGatewayDevices(List<Device> devices);

    /// <summary>
    /// Analyze ARP traffic patterns to infer gateway (fallback method)
    /// </summary>
    /// <param name="devices">List of discovered devices</param>
    /// <returns>Device likely to be the gateway based on ARP analysis</returns>
    Device? InferGatewayFromARPPatterns(List<Device> devices);

    /// <summary>
    /// Refresh gateway information and update device flags
    /// </summary>
    /// <param name="devices">List of devices to update</param>
    void RefreshGateways(List<Device> devices);

    event EventHandler<Device>? GatewayDetected;
    event EventHandler<string>? LogMessage;
}
