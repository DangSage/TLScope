using QuikGraph;
using TLScope.Models;
using TLScope.Services;
using Serilog;

namespace TLScope.Services.Mock;

/// <summary>
/// Mock graph service for UI testing
/// Wraps the real GraphService but adds testing utilities
/// </summary>
public class MockGraphService : IGraphService
{
    private readonly BidirectionalGraph<Device, TaggedEdge<Device, Connection>> _graph = new();
    private readonly Dictionary<string, Device> _deviceLookup = new();
    private readonly Dictionary<string, string> _ipToMacLookup = new();

    public event EventHandler<Device>? DeviceAdded;
    public event EventHandler<Connection>? ConnectionAdded;

    public void AddDevice(Device device)
    {
        if (_deviceLookup.ContainsKey(device.MACAddress))
        {
            Log.Debug($"Device already in graph: {device}");
            return;
        }

        _graph.AddVertex(device);
        _deviceLookup[device.MACAddress] = device;

        // Track IP→MAC mapping
        if (!string.IsNullOrEmpty(device.IPAddress))
        {
            _ipToMacLookup[device.IPAddress] = device.MACAddress;
        }

        Log.Information($"Added device to graph: {device}");
        DeviceAdded?.Invoke(this, device);
    }

    public Device? GetDevice(string macAddress)
    {
        _deviceLookup.TryGetValue(macAddress, out var device);
        return device;
    }

    public Device? GetDeviceByIP(string ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
            return null;

        if (_ipToMacLookup.TryGetValue(ipAddress, out var macAddress))
        {
            return GetDevice(macAddress);
        }

        return null;
    }

    public void UpdateDevice(Device device)
    {
        if (_deviceLookup.TryGetValue(device.MACAddress, out var existing))
        {
            // If IP address changed, update IP→MAC mapping
            if (!string.IsNullOrEmpty(existing.IPAddress) && existing.IPAddress != device.IPAddress)
            {
                _ipToMacLookup.Remove(existing.IPAddress);
            }

            existing.IPAddress = device.IPAddress;
            existing.Hostname = device.Hostname;
            existing.DeviceName = device.DeviceName;
            existing.LastSeen = device.LastSeen;
            existing.PacketCount = device.PacketCount;
            existing.BytesTransferred = device.BytesTransferred;

            // Add new IP mapping
            if (!string.IsNullOrEmpty(device.IPAddress))
            {
                _ipToMacLookup[device.IPAddress] = device.MACAddress;
            }

            Log.Debug($"Updated device: {existing}");
        }
    }

    public void AddConnection(Connection connection)
    {
        // Ensure both devices are in the graph
        if (!_deviceLookup.ContainsKey(connection.SourceDevice.MACAddress))
        {
            AddDevice(connection.SourceDevice);
        }

        if (!_deviceLookup.ContainsKey(connection.DestinationDevice.MACAddress))
        {
            AddDevice(connection.DestinationDevice);
        }

        var source = _deviceLookup[connection.SourceDevice.MACAddress];
        var destination = _deviceLookup[connection.DestinationDevice.MACAddress];

        // Check if edge already exists
        if (_graph.TryGetEdge(source, destination, out var existingEdge))
        {
            existingEdge.Tag.LastSeen = connection.LastSeen;
            existingEdge.Tag.PacketCount += connection.PacketCount;
            existingEdge.Tag.BytesTransferred += connection.BytesTransferred;
            return;
        }

        var edge = new TaggedEdge<Device, Connection>(source, destination, connection);
        _graph.AddEdge(edge);

        Log.Information($"Added connection to graph: {connection}");
        ConnectionAdded?.Invoke(this, connection);
    }

    public List<Device> GetAllDevices()
    {
        return _graph.Vertices.ToList();
    }

    public List<Connection> GetAllConnections()
    {
        return _graph.Edges.Select(e => e.Tag).ToList();
    }

    public List<Connection> GetActiveConnections()
    {
        return _graph.Edges
            .Select(e => e.Tag)
            .Where(c => c.IsActive)
            .ToList();
    }

    public List<Connection> GetDeviceConnections(Device device)
    {
        var deviceInGraph = _deviceLookup.GetValueOrDefault(device.MACAddress);
        if (deviceInGraph == null)
            return new List<Connection>();

        return _graph.Edges
            .Where(e => e.Source == deviceInGraph || e.Target == deviceInGraph)
            .Select(e => e.Tag)
            .ToList();
    }

    public NetworkStatistics GetStatistics()
    {
        var devices = _graph.Vertices.ToList();
        var connections = _graph.Edges.Select(e => e.Tag).ToList();

        return new NetworkStatistics
        {
            TotalDevices = devices.Count,
            ActiveDevices = devices.Count(d => d.IsActive),
            TotalConnections = connections.Count,
            ActiveConnections = connections.Count(c => c.IsActive),
            TotalBytesTransferred = connections.Sum(c => (long)c.BytesTransferred),
            TotalPackets = connections.Sum(c => (long)c.PacketCount),
            AverageDegree = devices.Count > 0
                ? _graph.Edges.Count() / (double)devices.Count
                : 0
        };
    }

    public string ExportToDot()
    {
        var dot = "digraph NetworkGraph {\n";
        dot += "  rankdir=LR;\n";
        dot += "  node [shape=box];\n\n";

        // Add all devices as nodes
        foreach (var device in _graph.Vertices)
        {
            var label = device.DeviceName ?? device.Hostname ?? device.IPAddress;
            dot += $"  \"{device.MACAddress}\" [label=\"{label}\\n{device.IPAddress}\"];\n";
        }

        dot += "\n";

        // Add all connections as edges
        foreach (var edge in _graph.Edges)
        {
            var conn = edge.Tag;
            var label = $"{conn.Protocol}:{conn.DestinationPort}";
            dot += $"  \"{edge.Source.MACAddress}\" -> \"{edge.Target.MACAddress}\" [label=\"{label}\"];\n";
        }

        dot += "}\n";

        Log.Information("Exported graph to DOT format");
        return dot;
    }

    public void Clear()
    {
        _graph.Clear();
        _deviceLookup.Clear();
        Log.Information("Graph cleared");
    }

    public void CleanupInactiveDevices()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-2);
        var inactiveDevices = _graph.Vertices
            .Where(d => d.LastSeen < cutoff)
            .ToList();

        foreach (var device in inactiveDevices)
        {
            _graph.RemoveVertex(device);
            _deviceLookup.Remove(device.MACAddress);
        }

        if (inactiveDevices.Count > 0)
        {
            Log.Information($"Removed {inactiveDevices.Count} inactive devices");
        }
    }

    public void ResetConnectionRates()
    {
        var now = DateTime.UtcNow;
        foreach (var edge in _graph.Edges)
        {
            var connection = edge.Tag;
            var timeSinceLastReset = now - connection.LastRateUpdate;

            if (timeSinceLastReset.TotalSeconds >= 30)
            {
                connection.RecentPacketCount = 0;
                connection.LastRateUpdate = now;
            }
        }
    }

    public Dictionary<string, int> GetProtocolDistribution()
    {
        var distribution = new Dictionary<string, int>();

        foreach (var edge in _graph.Edges)
        {
            var connection = edge.Tag;
            var protocol = connection.Protocol ?? "Unknown";

            if (distribution.ContainsKey(protocol))
                distribution[protocol]++;
            else
                distribution[protocol] = 1;
        }

        return distribution;
    }

    public Dictionary<int, int> GetPortDistribution()
    {
        var distribution = new Dictionary<int, int>();

        foreach (var edge in _graph.Edges)
        {
            var connection = edge.Tag;

            if (connection.DestinationPort.HasValue)
            {
                int port = connection.DestinationPort.Value;

                if (distribution.ContainsKey(port))
                    distribution[port]++;
                else
                    distribution[port] = 1;
            }
        }

        return distribution;
    }

    public Task LoadDevicesFromDatabaseAsync()
    {
        // Mock implementation - no database to load from
        Log.Information("MockGraphService: LoadDevicesFromDatabaseAsync called (no-op)");
        return Task.CompletedTask;
    }

    // Topology analysis methods (mock implementations)
    public List<Device> GetGatewayDevices()
    {
        return GetAllDevices().Where(d => d.IsGateway).ToList();
    }

    public Device? GetDefaultGateway()
    {
        return GetAllDevices().FirstOrDefault(d => d.IsDefaultGateway);
    }

    public (List<Device> RemoteDevices, List<Device> Gateways, List<Device> LocalDevices) GetTopologyTiers()
    {
        var allDevices = GetAllDevices();
        var remoteDevices = allDevices.Where(d => d.IsVirtualDevice).ToList();
        var gateways = allDevices.Where(d => d.IsGateway && !d.IsVirtualDevice).ToList();
        var localDevices = allDevices.Where(d => !d.IsVirtualDevice && !d.IsGateway && d.IsLocal).ToList();
        return (remoteDevices, gateways, localDevices);
    }

    public List<Connection> GetConnectionsByType(ConnectionType type)
    {
        return GetAllConnections().Where(c => c.Type == type).ToList();
    }

    public List<Connection> GetDirectConnections()
    {
        return GetConnectionsByType(ConnectionType.DirectL2);
    }

    public List<Connection> GetRoutedConnections()
    {
        return GetConnectionsByType(ConnectionType.RoutedL3);
    }

    public List<Connection> GetInternetConnections()
    {
        return GetConnectionsByType(ConnectionType.Internet);
    }

    public List<Connection> GetTLSPeerConnections()
    {
        return GetConnectionsByType(ConnectionType.TLSPeer);
    }

    public List<Connection> GetConnectionsToGateway(Device device)
    {
        var gateways = GetGatewayDevices();
        var allConnections = GetAllConnections();
        return allConnections.Where(c =>
            (c.SourceDevice == device && gateways.Contains(c.DestinationDevice)) ||
            (c.DestinationDevice == device && gateways.Contains(c.SourceDevice))
        ).ToList();
    }

    public List<Connection> GetGatewayToInternetConnections()
    {
        var gateways = GetGatewayDevices();
        var allConnections = GetAllConnections();
        return allConnections.Where(c =>
            (gateways.Contains(c.SourceDevice) && c.DestinationDevice.IsVirtualDevice) ||
            (gateways.Contains(c.DestinationDevice) && c.SourceDevice.IsVirtualDevice)
        ).ToList();
    }

    public ConnectionType ClassifyConnection(Connection connection)
    {
        if (connection.IsTLSPeerConnection)
            return ConnectionType.TLSPeer;

        if (connection.DestinationDevice.IsVirtualDevice)
            return ConnectionType.Internet;

        if (connection.DestinationDevice.IsLocal)
            return ConnectionType.DirectL2;

        return ConnectionType.Internet;
    }

    public void UpdateConnectionTypes()
    {
        // Mock implementation - update connection types
        foreach (var connection in GetAllConnections())
        {
            connection.Type = ClassifyConnection(connection);
        }
    }
}
