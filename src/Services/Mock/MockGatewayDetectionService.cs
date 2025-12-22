using System.Net;
using TLScope.Models;
using Serilog;

namespace TLScope.Services.Mock;

/// <summary>
/// Mock implementation of gateway detection service for testing
/// </summary>
public class MockGatewayDetectionService : IGatewayDetectionService
{
#pragma warning disable CS0067 // Event is never used - mock implementation doesn't need to raise events
    public event EventHandler<Device>? GatewayDetected;
    public event EventHandler<string>? LogMessage;
#pragma warning restore CS0067

    public IPAddress? DetectDefaultGateway()
    {
        // Mock implementation - return a fake gateway
        Log.Information("[MOCK] DetectDefaultGateway called");
        return IPAddress.Parse("192.168.1.1");
    }

    public List<(IPAddress Gateway, IPAddress Network, IPAddress Netmask)> GetRoutingTable()
    {
        // Mock implementation - return empty list
        Log.Information("[MOCK] GetRoutingTable called");
        return new List<(IPAddress Gateway, IPAddress Network, IPAddress Netmask)>();
    }

    public List<Device> IdentifyGatewayDevices(List<Device> devices)
    {
        // Mock implementation - don't mark any devices as gateways
        Log.Information("[MOCK] IdentifyGatewayDevices called");
        return new List<Device>();
    }

    public Device? InferGatewayFromARPPatterns(List<Device> devices)
    {
        // Mock implementation - return null
        Log.Information("[MOCK] InferGatewayFromARPPatterns called");
        return null;
    }

    public void RefreshGateways(List<Device> devices)
    {
        // Mock implementation - no-op
        Log.Information("[MOCK] RefreshGateways called");
    }
}
