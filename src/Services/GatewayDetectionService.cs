using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TLScope.Models;
using TLScope.Utilities;
using Serilog;

namespace TLScope.Services;

/// <summary>
/// Service for detecting network gateways/routers
/// Uses routing table analysis with ARP pattern fallback
/// </summary>
public class GatewayDetectionService : IGatewayDetectionService
{
    private readonly Dictionary<string, int> _arpDestinationCounts = new();
    private IPAddress? _lastDetectedGateway;

    public event EventHandler<Device>? GatewayDetected;
    public event EventHandler<string>? LogMessage;

    public GatewayDetectionService()
    {
        // Subscribe to network changes
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
    }

    /// <summary>
    /// Detect the default gateway from system routing table
    /// </summary>
    public IPAddress? DetectDefaultGateway()
    {
        try
        {
            // Method 1: Use NetworkInterface API (cross-platform)
            var gateway = DetectGatewayFromNetworkInterface();
            if (gateway != null)
            {
                Log.Information($"[GATEWAY] Detected default gateway via NetworkInterface: {gateway}");
                LogMessage?.Invoke(this, $"Default gateway detected: {gateway}");
                _lastDetectedGateway = gateway;
                return gateway;
            }

            // Method 2: Parse /proc/net/route (Linux fallback)
            if (OperatingSystem.IsLinux())
            {
                gateway = DetectGatewayFromProcNetRoute();
                if (gateway != null)
                {
                    Log.Information($"[GATEWAY] Detected default gateway via /proc/net/route: {gateway}");
                    LogMessage?.Invoke(this, $"Default gateway detected: {gateway}");
                    _lastDetectedGateway = gateway;
                    return gateway;
                }
            }

            Log.Warning("[GATEWAY] Could not detect default gateway from routing table");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GATEWAY] Failed to detect default gateway");
            return null;
        }
    }

    /// <summary>
    /// Detect gateway using .NET NetworkInterface API (cross-platform)
    /// </summary>
    private IPAddress? DetectGatewayFromNetworkInterface()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderByDescending(ni => ni.Speed); // Prefer faster interfaces

            foreach (var ni in interfaces)
            {
                var ipProps = ni.GetIPProperties();
                var gateways = ipProps.GatewayAddresses
                    .Where(g => g.Address.AddressFamily == AddressFamily.InterNetwork) // IPv4 only
                    .Select(g => g.Address)
                    .Where(addr => !IPAddress.IsLoopback(addr))
                    .ToList();

                if (gateways.Any())
                {
                    var gateway = gateways.First();
                    Log.Debug($"[GATEWAY] Found gateway {gateway} on interface {ni.Name}");
                    return gateway;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[GATEWAY] NetworkInterface API failed");
            return null;
        }
    }

    /// <summary>
    /// Parse /proc/net/route to find default gateway (Linux)
    /// </summary>
    private IPAddress? DetectGatewayFromProcNetRoute()
    {
        try
        {
            const string routeFile = "/proc/net/route";
            if (!File.Exists(routeFile))
                return null;

            var lines = File.ReadAllLines(routeFile);

            foreach (var line in lines.Skip(1)) // Skip header
            {
                var fields = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length < 3)
                    continue;

                // Field 1: Destination (00000000 = default route)
                // Field 2: Gateway
                var destination = fields[1];
                var gatewayHex = fields[2];

                if (destination == "00000000" && gatewayHex != "00000000")
                {
                    // Parse hex IP address (little-endian)
                    if (uint.TryParse(gatewayHex, System.Globalization.NumberStyles.HexNumber, null, out uint gwAddr))
                    {
                        var bytes = new byte[]
                        {
                            (byte)(gwAddr & 0xFF),
                            (byte)((gwAddr >> 8) & 0xFF),
                            (byte)((gwAddr >> 16) & 0xFF),
                            (byte)((gwAddr >> 24) & 0xFF)
                        };
                        return new IPAddress(bytes);
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[GATEWAY] Failed to parse /proc/net/route");
            return null;
        }
    }

    /// <summary>
    /// Get all gateways from the routing table
    /// </summary>
    public List<(IPAddress Gateway, IPAddress Network, IPAddress Netmask)> GetRoutingTable()
    {
        var routes = new List<(IPAddress Gateway, IPAddress Network, IPAddress Netmask)>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var ni in interfaces)
            {
                var ipProps = ni.GetIPProperties();
                var unicastAddresses = ipProps.UnicastAddresses;

                foreach (var gateway in ipProps.GatewayAddresses)
                {
                    if (gateway.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    // Try to find matching network for this gateway
                    foreach (var addr in unicastAddresses)
                    {
                        if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                            continue;

                        var netmask = addr.IPv4Mask;
                        var network = GetNetworkAddress(addr.Address, netmask);

                        routes.Add((gateway.Address, network, netmask));
                    }
                }
            }

            Log.Debug($"[GATEWAY] Found {routes.Count} routes in routing table");
            return routes;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GATEWAY] Failed to get routing table");
            return routes;
        }
    }

    /// <summary>
    /// Calculate network address from IP and netmask
    /// </summary>
    private IPAddress GetNetworkAddress(IPAddress address, IPAddress netmask)
    {
        var ipBytes = address.GetAddressBytes();
        var maskBytes = netmask.GetAddressBytes();
        var networkBytes = new byte[ipBytes.Length];

        for (int i = 0; i < ipBytes.Length; i++)
        {
            networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
        }

        return new IPAddress(networkBytes);
    }

    /// <summary>
    /// Match gateway IP addresses to discovered devices
    /// </summary>
    public List<Device> IdentifyGatewayDevices(List<Device> devices)
    {
        var gatewayDevices = new List<Device>();

        try
        {
            // Get default gateway
            var defaultGateway = DetectDefaultGateway();

            // Get all routes
            var routes = GetRoutingTable();
            var allGatewayIPs = routes.Select(r => r.Gateway).Distinct().ToList();

            if (defaultGateway != null && !allGatewayIPs.Contains(defaultGateway))
            {
                allGatewayIPs.Add(defaultGateway);
            }

            foreach (var device in devices)
            {
                if (string.IsNullOrEmpty(device.IPAddress))
                    continue;

                if (!IPAddress.TryParse(device.IPAddress, out var deviceIP))
                    continue;

                // Check if this device is a known gateway
                if (allGatewayIPs.Any(gw => gw.Equals(deviceIP)))
                {
                    device.IsGateway = true;

                    if (defaultGateway != null && deviceIP.Equals(defaultGateway))
                    {
                        device.IsDefaultGateway = true;
                        device.GatewayRole = "Default";
                        Log.Information($"[GATEWAY] Marked device {device.IPAddress} ({device.MACAddress}) as default gateway");
                    }
                    else
                    {
                        device.GatewayRole = "Secondary";
                        Log.Information($"[GATEWAY] Marked device {device.IPAddress} ({device.MACAddress}) as secondary gateway");
                    }

                    gatewayDevices.Add(device);
                    GatewayDetected?.Invoke(this, device);
                }
            }

            if (gatewayDevices.Count == 0)
            {
                Log.Warning("[GATEWAY] No gateway devices found in discovered devices list");

                // Try ARP-based fallback
                var inferredGateway = InferGatewayFromARPPatterns(devices);
                if (inferredGateway != null)
                {
                    inferredGateway.IsGateway = true;
                    inferredGateway.IsDefaultGateway = true;
                    inferredGateway.GatewayRole = "Default (Inferred)";
                    gatewayDevices.Add(inferredGateway);
                    Log.Information($"[GATEWAY] Inferred gateway from ARP patterns: {inferredGateway.IPAddress}");
                }
            }

            return gatewayDevices;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GATEWAY] Failed to identify gateway devices");
            return gatewayDevices;
        }
    }

    /// <summary>
    /// Infer gateway from ARP traffic patterns (fallback method)
    /// Gateway typically has the most diverse set of destination IPs
    /// </summary>
    public Device? InferGatewayFromARPPatterns(List<Device> devices)
    {
        try
        {
            // If we have ARP destination counts, use them
            if (_arpDestinationCounts.Any())
            {
                var mostDiverseMAC = _arpDestinationCounts
                    .OrderByDescending(kvp => kvp.Value)
                    .FirstOrDefault().Key;

                if (!string.IsNullOrEmpty(mostDiverseMAC))
                {
                    var gateway = devices.FirstOrDefault(d =>
                        d.MACAddress.Equals(mostDiverseMAC, StringComparison.OrdinalIgnoreCase));

                    if (gateway != null)
                    {
                        Log.Information($"[GATEWAY] ARP pattern analysis suggests {gateway.IPAddress} is gateway");
                        return gateway;
                    }
                }
            }

            // Fallback: Device with most connections to remote IPs is likely gateway
            var deviceWithMostRemoteConnections = devices
                .Where(d => d.IsLocal && !d.IsVirtualDevice)
                .OrderByDescending(d => d.PacketCount)
                .FirstOrDefault();

            if (deviceWithMostRemoteConnections != null)
            {
                Log.Information($"[GATEWAY] Traffic analysis suggests {deviceWithMostRemoteConnections.IPAddress} might be gateway");
                return deviceWithMostRemoteConnections;
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[GATEWAY] Failed to infer gateway from ARP patterns");
            return null;
        }
    }

    /// <summary>
    /// Track ARP destination for pattern analysis
    /// Called by PacketCaptureService when processing ARP packets
    /// </summary>
    public void TrackARPDestination(string sourceMac, string destinationIP)
    {
        if (string.IsNullOrEmpty(sourceMac))
            return;

        if (!_arpDestinationCounts.ContainsKey(sourceMac))
        {
            _arpDestinationCounts[sourceMac] = 0;
        }

        _arpDestinationCounts[sourceMac]++;
    }

    /// <summary>
    /// Refresh gateway information and update device flags
    /// </summary>
    public void RefreshGateways(List<Device> devices)
    {
        try
        {
            Log.Information("[GATEWAY] Refreshing gateway information");

            // Clear existing gateway flags
            foreach (var device in devices)
            {
                device.IsGateway = false;
                device.IsDefaultGateway = false;
                device.GatewayRole = null;
            }

            // Re-identify gateways
            var gateways = IdentifyGatewayDevices(devices);

            LogMessage?.Invoke(this, $"Gateway refresh complete: {gateways.Count} gateway(s) identified");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GATEWAY] Failed to refresh gateways");
        }
    }

    /// <summary>
    /// Handle network address changes
    /// </summary>
    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        Log.Information("[GATEWAY] Network address changed - gateway may have changed");
        LogMessage?.Invoke(this, "Network change detected - will refresh gateway information");

        // Clear cached gateway
        _lastDetectedGateway = null;
    }
}
