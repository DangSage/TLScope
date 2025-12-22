using TLScope.Models;

namespace TLScope.Services;

/// <summary>
/// Interface for packet capture services
/// Allows for mock implementations during testing
/// </summary>
public interface IPacketCaptureService : IDisposable
{
    event EventHandler<Device>? DeviceDiscovered;
    event EventHandler<Connection>? ConnectionDetected;
    event EventHandler<string>? LogMessage;

    void StartCapture(string? interfaceName = null, bool promiscuousMode = true);
    void StopCapture();
    List<Device> GetDiscoveredDevices();
    List<Connection> GetActiveConnections();
    void CleanupOldConnections();
    List<string> GetAvailableInterfaces();
    string? GetCurrentInterface();
    bool IsCapturing();
    Task<List<string>> ScanNetworkAsync(string? subnet = null);
}
