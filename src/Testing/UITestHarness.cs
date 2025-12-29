using TLScope.Models;
using TLScope.Services.Mock;
using TLScope.Views;
using TLScope.Utilities;
using Serilog;
using Microsoft.Extensions.DependencyInjection;

namespace TLScope.Testing;

/// <summary>
/// UI Test Harness for testing TLScope UI without real network capture
/// Wires up mock services and provides test scenarios
/// </summary>
public class UITestHarness
{
    private readonly MockPacketCaptureService _mockCaptureService;
    private readonly MockGraphService _mockGraphService;
    private readonly MockTlsPeerService _mockPeerService;
    private readonly User _testUser;

    public UITestHarness(TestScenario scenario = TestScenario.Simple)
    {
        _testUser = new User
        {
            Username = "test-user",
            Email = "test@tlscope.local",
            SSHPublicKey = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQC... test@tlscope"
        };

        _mockCaptureService = new MockPacketCaptureService();
        _mockGraphService = new MockGraphService();
        _mockPeerService = new MockTlsPeerService(_testUser);

        _mockCaptureService.DeviceDiscovered += (sender, device) =>
        {
            _mockGraphService.AddDevice(device);
        };

        _mockCaptureService.ConnectionDetected += (sender, connection) =>
        {
            _mockGraphService.AddConnection(connection);
        };


        Log.Information($"UI Test Harness initialized with scenario: {scenario}");
    }

    /// <summary>
    /// Run the UI test with the specified scenario
    /// </summary>
    public void Run(TestScenario scenario = TestScenario.Simple)
    {
        try
        {
            Log.Information("Starting UI Test Harness");

            LoadTestScenario(scenario);

            _mockCaptureService.StartCapture("mock-interface");
            _mockPeerService.Start();


            Log.Warning("To fully integrate with MainApplication, consider refactoring services to use interfaces");
            Log.Information("Test harness is running. Mock services are generating data.");
            Log.Information("Press Ctrl+C to stop");

            Console.WriteLine("UI Test Harness is running...");
            Console.WriteLine($"Scenario: {scenario}");
            Console.WriteLine($"Mock services are generating network activity");
            Console.WriteLine("\nPress any key to stop...");
            Console.ReadKey();

            _mockCaptureService.StopCapture();
            _mockPeerService.Stop();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in UI test harness");
            throw;
        }
    }

    /// <summary>
    /// Load predefined test data based on scenario
    /// </summary>
    private void LoadTestScenario(TestScenario scenario)
    {
        (List<Device> devices, List<Connection> connections) = scenario switch
        {
            TestScenario.Simple => TestDataGenerator.GenerateSimpleNetwork(),
            TestScenario.Complex => TestDataGenerator.GenerateComplexNetwork(),
            TestScenario.StressTest => TestDataGenerator.GenerateStressTestNetwork(),
            _ => TestDataGenerator.GenerateSimpleNetwork()
        };

        Log.Information($"Loading test scenario: {scenario} ({devices.Count} devices, {connections.Count} connections)");

        foreach (var device in devices)
        {
            _mockCaptureService.TriggerDeviceDiscovery(device);
        }

        foreach (var connection in connections)
        {
            _mockCaptureService.TriggerConnectionDetection(connection);
        }

        var peers = TestDataGenerator.GenerateTlsPeers(5);
        foreach (var peer in peers)
        {
            _mockPeerService.TriggerPeerDiscovery(peer);
        }

        Log.Information("Test data loaded successfully");
    }

    /// <summary>
    /// Get access to mock services for manual testing
    /// </summary>
    public (MockPacketCaptureService capture, MockGraphService graph, MockTlsPeerService peers) GetMockServices()
    {
        return (_mockCaptureService, _mockGraphService, _mockPeerService);
    }
}

/// <summary>
/// Predefined test scenarios
/// </summary>
public enum TestScenario
{
    /// <summary>
    /// Simple network with 5 devices and basic connections
    /// </summary>
    Simple,

    /// <summary>
    /// Complex network with 15 devices and many connections
    /// </summary>
    Complex,

    /// <summary>
    /// Stress test with 50 devices and 120 connections
    /// </summary>
    StressTest,

    /// <summary>
    /// Empty network (only simulated activity from mock services)
    /// </summary>
    Empty
}
