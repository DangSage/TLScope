using TLScope.Models;

namespace TLScope.Services;

/// <summary>
/// Interface for graph services
/// Allows for mock implementations during testing
/// </summary>
public interface IGraphService
{
    event EventHandler<Device>? DeviceAdded;
    event EventHandler<Connection>? ConnectionAdded;

    void AddDevice(Device device);
    Device? GetDevice(string macAddress);
    Device? GetDeviceByIP(string ipAddress);
    void UpdateDevice(Device device);
    void AddConnection(Connection connection);
    List<Device> GetAllDevices();
    List<Connection> GetAllConnections();
    List<Connection> GetActiveConnections();
    List<Connection> GetDeviceConnections(Device device);
    NetworkStatistics GetStatistics();
    string ExportToDot();
    void Clear();
    void CleanupInactiveDevices();
    void ResetConnectionRates();
    Dictionary<string, int> GetProtocolDistribution();
    Dictionary<int, int> GetPortDistribution();
    Task LoadDevicesFromDatabaseAsync();

    // Topology analysis methods
    List<Device> GetGatewayDevices();
    Device? GetDefaultGateway();
    (List<Device> RemoteDevices, List<Device> Gateways, List<Device> LocalDevices) GetTopologyTiers();
    List<Connection> GetConnectionsByType(ConnectionType type);
    List<Connection> GetDirectConnections();
    List<Connection> GetRoutedConnections();
    List<Connection> GetInternetConnections();
    List<Connection> GetTLSPeerConnections();
    List<Connection> GetConnectionsToGateway(Device device);
    List<Connection> GetGatewayToInternetConnections();
    ConnectionType ClassifyConnection(Connection connection);
    void UpdateConnectionTypes();
}
