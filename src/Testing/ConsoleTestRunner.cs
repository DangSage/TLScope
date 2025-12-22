using TLScope.Models;
using TLScope.Services;
using TLScope.Services.Mock;
using Serilog;

namespace TLScope.Testing;

/// <summary>
/// Simple console-based test runner for UI testing without Terminal.Gui
/// Reliable, CI/CD friendly, works on all terminals
/// </summary>
public class ConsoleTestRunner
{
    private readonly MockPacketCaptureService _captureService;
    private readonly MockGraphService _graphService;
    private readonly MockTlsPeerService _peerService;
    private readonly User _testUser;
    private readonly TestScenario _scenario;
    private readonly CancellationTokenSource _cancellation = new();
    private DateTime _lastStatsUpdate = DateTime.UtcNow;

    public ConsoleTestRunner(TestScenario scenario)
    {
        _scenario = scenario;

        _testUser = new User
        {
            Username = "test-user",
            Email = "test@tlscope.local",
            SSHPublicKey = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQC... test@tlscope",
            AvatarType = "ROBOT",
            AvatarColor = "#4ECDC4"
        };

        _captureService = new MockPacketCaptureService();
        _graphService = new MockGraphService();
        _peerService = new MockTlsPeerService(_testUser);

        _captureService.DeviceDiscovered += OnDeviceDiscovered;
        _graphService.DeviceAdded += OnDeviceAdded;
        _peerService.PeerDiscovered += OnPeerDiscovered;
        _peerService.PeerConnected += OnPeerConnected;
        _peerService.PeerDisconnected += OnPeerDisconnected;

        _captureService.DeviceDiscovered += (sender, device) => _graphService.AddDevice(device);
        _captureService.ConnectionDetected += (sender, connection) => _graphService.AddConnection(connection);
    }

    public void Run()
    {
        try
        {
            PrintHeader();
            LoadTestData();
            StartServices();
            RunStatisticsLoop();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in console test runner");
            Console.WriteLine($"\n❌ Error: {ex.Message}");
        }
        finally
        {
            Cleanup();
        }
    }

    private void PrintHeader()
    {
        Console.Clear();
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         TLScope UI Test - Console Mode                    ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"📋 Test Scenario: {_scenario}");
        Console.WriteLine($"👤 Test User: {_testUser.Username}");
        Console.WriteLine($"⚙️  Mode: Console (Text-based)");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to stop...");
        Console.WriteLine(new string('─', 60));
        Console.WriteLine();
    }

    private void LoadTestData()
    {
        var (devices, connections) = _scenario switch
        {
            TestScenario.Simple => TestDataGenerator.GenerateSimpleNetwork(),
            TestScenario.Complex => TestDataGenerator.GenerateComplexNetwork(),
            TestScenario.StressTest => TestDataGenerator.GenerateStressTestNetwork(),
            _ => (new List<Device>(), new List<Connection>())
        };

        Console.WriteLine($"📦 Loading test data...");
        Console.WriteLine($"   Devices: {devices.Count}");
        Console.WriteLine($"   Connections: {connections.Count}");
        Console.WriteLine();

        foreach (var device in devices)
        {
            _captureService.TriggerDeviceDiscovery(device);
        }

        foreach (var connection in connections)
        {
            _captureService.TriggerConnectionDetection(connection);
        }

        var peers = TestDataGenerator.GenerateTlsPeers(5);
        foreach (var peer in peers)
        {
            _peerService.TriggerPeerDiscovery(peer);
        }

        Console.WriteLine("✅ Test data loaded");
        Console.WriteLine(new string('─', 60));
        Console.WriteLine();
    }

    private void StartServices()
    {
        Console.WriteLine("🚀 Starting mock services...");
        _captureService.StartCapture("mock-interface");
        _peerService.Start();
        Console.WriteLine("✅ Services started - simulating network activity");
        Console.WriteLine(new string('─', 60));
        Console.WriteLine();
    }

    private void RunStatisticsLoop()
    {
        Console.WriteLine("📊 Live Activity (Ctrl+C to stop):");
        Console.WriteLine();

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            _cancellation.Cancel();
        };

        PrintStatistics();

        while (!_cancellation.Token.IsCancellationRequested)
        {
            Thread.Sleep(1000);

            if ((DateTime.UtcNow - _lastStatsUpdate).TotalSeconds >= 5)
            {
                Console.WriteLine();
                PrintStatistics();
                _lastStatsUpdate = DateTime.UtcNow;
            }
        }

        Console.WriteLine();
        Console.WriteLine("🛑 Stopping test...");
    }

    private void PrintStatistics()
    {
        var stats = _graphService.GetStatistics();
        var peers = _peerService.GetDiscoveredPeers();
        var connectedPeers = peers.Count(p => p.IsConnected);

        var timestamp = DateTime.Now.ToString("HH:mm:ss");

        Console.WriteLine($"[{timestamp}] 📊 Network Statistics:");
        Console.WriteLine($"   Devices: {stats.ActiveDevices}/{stats.TotalDevices} active");
        Console.WriteLine($"   Connections: {stats.ActiveConnections}/{stats.TotalConnections} active");
        Console.WriteLine($"   Peers: {connectedPeers}/{peers.Count} connected");
        Console.WriteLine($"   Data: {FormatBytes(stats.TotalBytesTransferred)} transferred");
    }

    private void OnDeviceDiscovered(object? sender, Device device)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var vendor = device.Vendor != null ? $" [{device.Vendor}]" : "";
        Console.WriteLine($"[{timestamp}] 🔍 Device Discovered: {device.IPAddress} ({device.MACAddress}){vendor}");

        if (device.Hostname != null)
        {
            Console.WriteLine($"             Hostname: {device.Hostname}");
        }
    }

    private void OnDeviceAdded(object? sender, Device device)
    {
    }

    private void OnPeerDiscovered(object? sender, TLSPeer peer)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] 👥 Peer Discovered: {peer.Username} @ {peer.IPAddress}");
        Console.WriteLine($"             Avatar: {peer.AvatarType} {peer.AvatarColor}");
    }

    private void OnPeerConnected(object? sender, TLSPeer peer)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] ✅ Peer Connected: {peer.Username}");
    }

    private void OnPeerDisconnected(object? sender, TLSPeer peer)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] ❌ Peer Disconnected: {peer.Username}");
    }

    private void Cleanup()
    {
        Console.WriteLine("🧹 Cleaning up...");
        _captureService.StopCapture();
        _peerService.Stop();
        _captureService.Dispose();
        _peerService.Dispose();
        _cancellation.Dispose();
        Console.WriteLine("✅ Cleanup complete");
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
