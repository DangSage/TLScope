using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Serilog;
using TLScope.Utilities;

namespace TLScope.Services;

/// <summary>
/// Provides active network discovery using ICMP ping sweeps
/// Efficiently scans local networks to discover devices that may not be actively transmitting traffic
/// </summary>
public class NetworkScanService : INetworkScanService
{
    private readonly int _timeout;
    private readonly int _maxConcurrency;

    /// <summary>
    /// Event raised when a device responds to a ping
    /// </summary>
    public event EventHandler<(string ipAddress, PingReply reply)>? DeviceResponded;

    /// <summary>
    /// Event raised when a network scan completes
    /// </summary>
    public event EventHandler<NetworkScanResult>? ScanCompleted;

    /// <summary>
    /// Creates a new NetworkScanService
    /// </summary>
    /// <param name="timeout">Ping timeout in milliseconds (default: 500ms)</param>
    /// <param name="maxConcurrency">Maximum concurrent pings (default: 50)</param>
    public NetworkScanService(int timeout = 500, int maxConcurrency = 50)
    {
        _timeout = timeout;
        _maxConcurrency = maxConcurrency;
    }

    /// <summary>
    /// Performs ICMP ping sweep on a subnet to discover active devices
    /// </summary>
    /// <param name="subnet">Base IP address (e.g., "192.168.1")</param>
    /// <param name="startHost">Starting host number (default: 1)</param>
    /// <param name="endHost">Ending host number (default: 254)</param>
    /// <returns>List of responsive IP addresses</returns>
    public async Task<List<string>> PingSweepAsync(string subnet, int startHost = 1, int endHost = 254)
    {
        var stopwatch = Stopwatch.StartNew();
        Log.Information($"[NETWORK-SCAN] Starting ping sweep on {subnet}.{startHost}-{endHost} (timeout: {_timeout}ms, concurrency: {_maxConcurrency})");

        // Generate IP addresses
        var ipAddresses = Enumerable.Range(startHost, endHost - startHost + 1)
            .Select(i => $"{subnet}.{i}")
            .Where(ip => !IPAddressValidator.IsUtilityAddress(ip,
                filterLoopback: true,
                filterBroadcast: true,
                filterMulticast: true,
                filterLinkLocal: true,
                filterReserved: true))
            .ToList();

        Log.Debug($"[NETWORK-SCAN] Generated {ipAddresses.Count} IP addresses to scan");

        // Perform parallel ping sweep with rate limiting
        var activeIps = new ConcurrentBag<string>();
        var scannedCount = 0;

        await Parallel.ForEachAsync(ipAddresses,
            new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrency },
            async (ip, ct) =>
            {
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(ip, _timeout);

                    Interlocked.Increment(ref scannedCount);

                    if (reply.Status == IPStatus.Success)
                    {
                        activeIps.Add(ip);
                        Log.Information($"[NETWORK-SCAN] ✓ {ip} responded in {reply.RoundtripTime}ms");

                        // Raise event for responsive device
                        DeviceResponded?.Invoke(this, (ip, reply));
                    }
                    else
                    {
                        Log.Debug($"[NETWORK-SCAN] ✗ {ip} - {reply.Status}");
                    }
                }
                catch (PingException ex)
                {
                    Log.Debug($"[NETWORK-SCAN] ✗ {ip} - Ping failed: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[NETWORK-SCAN] ✗ {ip} - Unexpected error: {ex.Message}");
                }
            });

        stopwatch.Stop();
        var results = activeIps.OrderBy(ip => ip, new IPAddressComparer()).ToList();

        Log.Information($"[NETWORK-SCAN] Scan complete: {results.Count}/{ipAddresses.Count} hosts responded in {stopwatch.Elapsed.TotalSeconds:F2}s");

        // Raise completion event
        var scanResult = new NetworkScanResult
        {
            ResponsiveHosts = results,
            TotalScanned = ipAddresses.Count,
            Duration = stopwatch.Elapsed,
            Subnet = subnet
        };
        ScanCompleted?.Invoke(this, scanResult);

        return results;
    }

    /// <summary>
    /// Auto-detect local subnet from network interface and scan it
    /// </summary>
    /// <param name="interfaceName">Optional network interface name</param>
    /// <returns>List of responsive IP addresses</returns>
    public async Task<List<string>> ScanLocalNetworkAsync(string? interfaceName = null)
    {
        var subnet = GetLocalSubnet(interfaceName);

        if (subnet == null)
        {
            Log.Warning("[NETWORK-SCAN] Unable to auto-detect local subnet");
            return new List<string>();
        }

        Log.Information($"[NETWORK-SCAN] Auto-detected subnet: {subnet}.0/24");
        return await PingSweepAsync(subnet);
    }

    /// <summary>
    /// Gets the subnet address from a network interface
    /// </summary>
    /// <param name="interfaceName">Optional network interface name (uses first active if null)</param>
    /// <returns>Subnet base address (e.g., "192.168.1") or null if unable to detect</returns>
    public string? GetLocalSubnet(string? interfaceName = null)
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            NetworkInterface? selectedInterface;

            if (!string.IsNullOrEmpty(interfaceName))
            {
                selectedInterface = interfaces.FirstOrDefault(ni =>
                    ni.Name.Equals(interfaceName, StringComparison.OrdinalIgnoreCase));

                if (selectedInterface == null)
                {
                    Log.Warning($"[NETWORK-SCAN] Interface '{interfaceName}' not found");
                    return null;
                }
            }
            else
            {
                // Use first active non-loopback interface with an IPv4 address
                selectedInterface = interfaces.FirstOrDefault(ni =>
                    ni.GetIPProperties().UnicastAddresses.Any(ua =>
                        ua.Address.AddressFamily == AddressFamily.InterNetwork &&
                        IPAddressValidator.IsLocalAddress(ua.Address.ToString())));

                if (selectedInterface == null)
                {
                    Log.Warning("[NETWORK-SCAN] No suitable network interface found");
                    return null;
                }
            }

            // Get first IPv4 local address
            var ipAddress = selectedInterface.GetIPProperties().UnicastAddresses
                .FirstOrDefault(ua =>
                    ua.Address.AddressFamily == AddressFamily.InterNetwork &&
                    IPAddressValidator.IsLocalAddress(ua.Address.ToString()));

            if (ipAddress == null)
            {
                Log.Warning($"[NETWORK-SCAN] No IPv4 address found on interface '{selectedInterface.Name}'");
                return null;
            }

            // Extract subnet (assuming /24 - first 3 octets)
            var ipParts = ipAddress.Address.ToString().Split('.');
            if (ipParts.Length != 4)
            {
                Log.Warning($"[NETWORK-SCAN] Invalid IP address format: {ipAddress.Address}");
                return null;
            }

            var subnet = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}";
            Log.Debug($"[NETWORK-SCAN] Detected subnet {subnet}.0/24 from interface '{selectedInterface.Name}' ({ipAddress.Address})");

            return subnet;
        }
        catch (Exception ex)
        {
            Log.Error($"[NETWORK-SCAN] Error detecting local subnet: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// IP address comparer for sorting (handles proper numeric ordering)
/// </summary>
internal class IPAddressComparer : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if (x == null || y == null)
            return string.Compare(x, y);

        if (IPAddress.TryParse(x, out var ipX) && IPAddress.TryParse(y, out var ipY))
        {
            var bytesX = ipX.GetAddressBytes();
            var bytesY = ipY.GetAddressBytes();

            for (int i = 0; i < Math.Min(bytesX.Length, bytesY.Length); i++)
            {
                if (bytesX[i] != bytesY[i])
                    return bytesX[i].CompareTo(bytesY[i]);
            }

            return bytesX.Length.CompareTo(bytesY.Length);
        }

        return string.Compare(x, y);
    }
}
