using TLScope.Models;
using TLScope.Services;
using TLScope.Services.Mock;
using TLScope.Views;
using TLScope.Utilities;
using TLScope.Testing;
using Serilog;
using Microsoft.EntityFrameworkCore;

namespace TLScope.Testing;

/// <summary>
/// Standalone program for UI testing with mock services
/// Run this to test the UI without real network capture
/// </summary>
public class UITestProgram
{
    public static void Run(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("tlscope-uitest.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("=== TLScope UI Test Mode ===");

        var scenario = TestScenario.Simple;
        if (args.Length > 0 && Enum.TryParse<TestScenario>(args[0], true, out var parsedScenario))
        {
            scenario = parsedScenario;
        }

        Console.WriteLine($"Test Scenario: {scenario}");
        Console.WriteLine("Press Enter to start UI test...");
        Console.ReadLine();

        try
        {
            RunUITest(scenario);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in UI test");
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void RunUITest(TestScenario scenario)
    {
        var testUser = new User
        {
            Username = "ui-tester",
            Email = "tester@tlscope.local",
            SSHPublicKey = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQC... tester@tlscope"
        };

        var mockCapture = new MockPacketCaptureService();
        var mockGraph = new MockGraphService();
        var mockPeers = new MockTlsPeerService(testUser);

        mockCapture.DeviceDiscovered += (sender, device) =>
        {
            mockGraph.AddDevice(device);
        };

        mockCapture.ConnectionDetected += (sender, connection) =>
        {
            mockGraph.AddConnection(connection);
        };

        Log.Information("Loading test data...");
        LoadTestData(mockCapture, mockPeers, scenario);

        Log.Information("Starting mock services...");
        mockCapture.StartCapture("test-interface");
        mockPeers.Start();

        Log.Information("Creating UI application...");
        var userService = new UserService(new Data.ApplicationDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Data.ApplicationDbContext>()
                .UseSqlite("Data Source=tlscope_test.db")
                .Options));
        var filterConfig = Models.FilterConfiguration.Load();
        var mockGateway = new Services.Mock.MockGatewayDetectionService();
        var mainApp = new MainApplication(mockCapture, mockGraph, userService, filterConfig, mockGateway, mockPeers);

        mainApp.Run(null, testUser, startCapture: false).Wait();
    }

    private static void LoadTestData(MockPacketCaptureService capture, MockTlsPeerService peers, TestScenario scenario)
    {
        var (devices, connections) = scenario switch
        {
            TestScenario.Simple => TestDataGenerator.GenerateSimpleNetwork(),
            TestScenario.Complex => TestDataGenerator.GenerateComplexNetwork(),
            TestScenario.StressTest => TestDataGenerator.GenerateStressTestNetwork(),
            _ => (new List<Device>(), new List<Connection>())
        };

        Log.Information($"Loading {scenario} scenario: {devices.Count} devices, {connections.Count} connections");

        foreach (var device in devices)
        {
            capture.TriggerDeviceDiscovery(device);
        }

        foreach (var connection in connections)
        {
            capture.TriggerConnectionDetection(connection);
        }

        var tlsPeers = TestDataGenerator.GenerateTlsPeers(5);
        foreach (var peer in tlsPeers)
        {
            peers.TriggerPeerDiscovery(peer);
        }

        Log.Information("Test data loaded");
    }
}
