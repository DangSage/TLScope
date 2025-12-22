using TLScope.Models;
using TLScope.Services;
using Serilog;
using System.Collections.Concurrent;

namespace TLScope.Services.Mock;

/// <summary>
/// Mock packet capture service for UI testing
/// Simulates device discovery and connection detection without real packet capture
/// </summary>
public class MockPacketCaptureService : IPacketCaptureService
{
    private readonly ConcurrentDictionary<string, Device> _discoveredDevices = new();
    private readonly ConcurrentDictionary<string, Connection> _activeConnections = new();
    private bool _isCapturing;
    private CancellationTokenSource? _simulationCancellation;
    private string? _currentInterfaceName;

    public event EventHandler<Device>? DeviceDiscovered;
    public event EventHandler<Connection>? ConnectionDetected;
    public event EventHandler<string>? LogMessage;

    /// <summary>
    /// Start simulated packet capture
    /// </summary>
    public void StartCapture(string? interfaceName = null, bool promiscuousMode = true)
    {
        if (_isCapturing)
        {
            Log.Warning("Mock capture already running");
            return;
        }

        _isCapturing = true;
        _simulationCancellation = new CancellationTokenSource();
        _currentInterfaceName = interfaceName ?? "mock0 - Mock Network Interface";

        Log.Information($"Starting mock capture on {_currentInterfaceName}");
        LogMessage?.Invoke(this, $"Starting mock capture on {_currentInterfaceName}");

        // Start simulation in background
        Task.Run(() => SimulateNetworkActivity(_simulationCancellation.Token));
    }

    /// <summary>
    /// Stop simulated packet capture
    /// </summary>
    public void StopCapture()
    {
        if (!_isCapturing)
            return;

        _simulationCancellation?.Cancel();
        _isCapturing = false;
        _currentInterfaceName = null;

        Log.Information("Mock capture stopped");
        LogMessage?.Invoke(this, "Mock capture stopped");
    }

    /// <summary>
    /// Simulate network activity by generating random devices and connections
    /// </summary>
    private async Task SimulateNetworkActivity(CancellationToken cancellationToken)
    {
        var random = new Random();
        await Task.Delay(500, cancellationToken); // Initial delay

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Randomly discover new devices
                if (random.Next(100) < 30 && _discoveredDevices.Count < 20)
                {
                    var device = GenerateRandomDevice();
                    if (_discoveredDevices.TryAdd(device.MACAddress, device))
                    {
                        Log.Information($"Mock discovered device: {device}");
                        LogMessage?.Invoke(this, $"Discovered device: {device.IPAddress}");
                        DeviceDiscovered?.Invoke(this, device);
                    }
                }

                // Randomly create connections between existing devices
                if (_discoveredDevices.Count >= 2 && random.Next(100) < 40)
                {
                    var devices = _discoveredDevices.Values.ToList();
                    var srcDevice = devices[random.Next(devices.Count)];
                    var dstDevice = devices[random.Next(devices.Count)];

                    if (srcDevice != dstDevice)
                    {
                        var connection = GenerateRandomConnection(srcDevice, dstDevice);
                        var key = $"{connection.SourceDevice.MACAddress}:{connection.SourcePort}→{connection.DestinationDevice.MACAddress}:{connection.DestinationPort}";

                        if (_activeConnections.TryAdd(key, connection))
                        {
                            Log.Information($"Mock detected connection: {connection}");
                            LogMessage?.Invoke(this, $"Connection: {srcDevice.IPAddress} → {dstDevice.IPAddress} ({connection.Protocol})");
                            ConnectionDetected?.Invoke(this, connection);
                        }
                    }
                }

                // Update existing devices
                if (_discoveredDevices.Count > 0 && random.Next(100) < 20)
                {
                    var device = _discoveredDevices.Values.ElementAt(random.Next(_discoveredDevices.Count));
                    device.LastSeen = DateTime.UtcNow;
                    device.PacketCount += random.Next(1, 100);
                }

                await Task.Delay(random.Next(500, 2000), cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    /// <summary>
    /// Generate a random device for testing
    /// </summary>
    private Device GenerateRandomDevice()
    {
        var random = new Random();
        var macBytes = new byte[6];
        random.NextBytes(macBytes);
        var mac = string.Join(":", macBytes.Select(b => b.ToString("X2")));

        var ip = $"192.168.1.{random.Next(2, 254)}";
        var vendors = new[] { "Dell", "HP", "Apple", "Cisco", "Samsung", "Intel", "TP-Link", null };
        var hostnames = new[] { "desktop", "laptop", "router", "server", "printer", "phone", "tablet", "iot-device" };

        var device = new Device
        {
            MACAddress = mac,
            IPAddress = ip,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            PacketCount = random.Next(1, 1000),
            Vendor = vendors[random.Next(vendors.Length)],
            Hostname = $"{hostnames[random.Next(hostnames.Length)]}-{random.Next(1, 99)}"
        };

        // Add some open ports
        var portCount = random.Next(1, 5);
        for (int i = 0; i < portCount; i++)
        {
            var commonPorts = new[] { 80, 443, 22, 3389, 8080, 8443, 3306, 5432 };
            device.OpenPorts.Add(commonPorts[random.Next(commonPorts.Length)]);
        }

        return device;
    }

    /// <summary>
    /// Generate a random connection between two devices
    /// </summary>
    private Connection GenerateRandomConnection(Device src, Device dst)
    {
        var random = new Random();
        var protocols = new[] { "TCP", "UDP" };
        var protocol = protocols[random.Next(protocols.Length)];
        var commonPorts = new[] { 80, 443, 22, 3389, 8080, 8443, 8442, 53, 123 };

        var connection = new Connection
        {
            SourceDevice = src,
            DestinationDevice = dst,
            Protocol = protocol,
            SourcePort = random.Next(1024, 65535),
            DestinationPort = commonPorts[random.Next(commonPorts.Length)],
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            PacketCount = random.Next(1, 500),
            BytesTransferred = random.Next(1024, 1024 * 1024)
        };

        // Mark TLS peer connections
        if (connection.DestinationPort == 8443 || connection.SourcePort == 8443)
        {
            connection.IsTLSPeerConnection = true;
        }

        return connection;
    }

    /// <summary>
    /// Manually trigger device discovery (for testing)
    /// </summary>
    public void TriggerDeviceDiscovery(Device device)
    {
        if (_discoveredDevices.TryAdd(device.MACAddress, device))
        {
            DeviceDiscovered?.Invoke(this, device);
        }
    }

    /// <summary>
    /// Manually trigger connection detection (for testing)
    /// </summary>
    public void TriggerConnectionDetection(Connection connection)
    {
        ConnectionDetected?.Invoke(this, connection);
    }

    public List<Device> GetDiscoveredDevices()
    {
        return _discoveredDevices.Values.ToList();
    }

    public List<Connection> GetActiveConnections()
    {
        return _activeConnections.Values
            .Where(c => c.IsActive)
            .ToList();
    }

    public void CleanupOldConnections()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        var toRemove = _activeConnections
            .Where(kvp => kvp.Value.LastSeen < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _activeConnections.TryRemove(key, out _);
        }
    }

    public List<string> GetAvailableInterfaces()
    {
        return new List<string>
        {
            "mock0 - Mock Network Interface (Test)",
            "mock1 - Mock WiFi Interface (Test)",
            "mock2 - Mock Ethernet Interface (Test)",
            "mocklo - Mock Loopback (Test)"
        };
    }

    public string? GetCurrentInterface()
    {
        return _currentInterfaceName;
    }

    public bool IsCapturing()
    {
        return _isCapturing;
    }

    public async Task<List<string>> ScanNetworkAsync(string? subnet = null)
    {
        // Mock implementation - simulate a quick scan
        Log.Information("[MOCK] Simulating network scan");
        LogMessage?.Invoke(this, "Mock network scan (not implemented)");
        await Task.Delay(100); // Simulate brief delay
        return new List<string>(); // Return empty list for mock
    }

    public void Dispose()
    {
        StopCapture();
        _simulationCancellation?.Dispose();
    }
}
