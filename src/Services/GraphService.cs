using QuikGraph;
using QuikGraph.Algorithms;
using TLScope.Models;
using TLScope.Data;
using TLScope.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace TLScope.Services;

/// <summary>
/// Network graph management service using QuikGraph
/// Manages devices as vertices and connections as edges
/// </summary>
public class GraphService : IGraphService
{
    private readonly BidirectionalGraph<Device, TaggedEdge<Device, Connection>> _graph = new();
    private readonly Dictionary<string, Device> _deviceLookup = new(); // MAC -> Device
    private readonly Dictionary<string, string> _ipToMacLookup = new(); // IP -> MAC (for duplicate detection)
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public event EventHandler<Device>? DeviceAdded;
    public event EventHandler<Connection>? ConnectionAdded;

    public GraphService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Add device to the graph
    /// </summary>
    public void AddDevice(Device device)
    {
        // Normalize MAC address to lowercase for consistent lookups
        var normalizedMac = device.MACAddress.ToLower();

        if (_deviceLookup.ContainsKey(normalizedMac))
        {
            Log.Debug($"Device already in graph, updating: {device}");
            UpdateDevice(device);
            return;
        }

        _graph.AddVertex(device);
        _deviceLookup[normalizedMac] = device;

        // Track IP→MAC mapping for duplicate detection
        if (!string.IsNullOrEmpty(device.IPAddress))
        {
            _ipToMacLookup[device.IPAddress] = normalizedMac;
        }

        // Save to database
        _ = SaveDeviceToDatabaseAsync(device);

        Log.Information($"Added device to graph: {device}");
        DeviceAdded?.Invoke(this, device);
    }

    /// <summary>
    /// Get device by MAC address
    /// </summary>
    public Device? GetDevice(string macAddress)
    {
        var normalizedMac = macAddress.ToLower();
        _deviceLookup.TryGetValue(normalizedMac, out var device);
        return device;
    }

    /// <summary>
    /// Get device by IP address
    /// </summary>
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

    /// <summary>
    /// Update existing device
    /// </summary>
    public void UpdateDevice(Device device)
    {
        var normalizedMac = device.MACAddress.ToLower();

        if (_deviceLookup.TryGetValue(normalizedMac, out var existing))
        {
            // If IP address changed, update IP→MAC mapping
            if (!string.IsNullOrEmpty(existing.IPAddress) && existing.IPAddress != device.IPAddress)
            {
                // Remove old IP mapping (check if exists first)
                if (_ipToMacLookup.ContainsKey(existing.IPAddress))
                {
                    _ipToMacLookup.Remove(existing.IPAddress);
                }
            }

            // Update properties
            existing.IPAddress = device.IPAddress;
            existing.Hostname = device.Hostname;
            existing.DeviceName = device.DeviceName;
            existing.LastSeen = device.LastSeen;
            existing.PacketCount = device.PacketCount;
            existing.BytesTransferred = device.BytesTransferred;

            // Add new IP mapping
            if (!string.IsNullOrEmpty(device.IPAddress))
            {
                _ipToMacLookup[device.IPAddress] = normalizedMac;
            }

            // Save to database
            _ = SaveDeviceToDatabaseAsync(existing);

            Log.Debug($"Updated device: {existing}");
        }
    }

    /// <summary>
    /// Add connection between devices
    /// </summary>
    public void AddConnection(Connection connection)
    {
        // Normalize MAC addresses for lookups
        var sourceMac = connection.SourceDevice.MACAddress.ToLower();
        var destMac = connection.DestinationDevice.MACAddress.ToLower();

        // Ensure both devices are in the graph
        if (!_deviceLookup.ContainsKey(sourceMac))
        {
            AddDevice(connection.SourceDevice);
        }

        if (!_deviceLookup.ContainsKey(destMac))
        {
            AddDevice(connection.DestinationDevice);
        }

        var source = _deviceLookup[sourceMac];
        var destination = _deviceLookup[destMac];

        // Check if edge already exists
        if (_graph.TryGetEdge(source, destination, out var existingEdge))
        {
            // Update existing connection
            existingEdge.Tag.LastSeen = connection.LastSeen;
            existingEdge.Tag.PacketCount += connection.PacketCount;
            existingEdge.Tag.BytesTransferred += connection.BytesTransferred;
            return;
        }

        // Add new edge
        var edge = new TaggedEdge<Device, Connection>(source, destination, connection);
        _graph.AddEdge(edge);

        Log.Information($"Added connection to graph: {connection}");
        ConnectionAdded?.Invoke(this, connection);
    }

    /// <summary>
    /// Get all devices in the graph
    /// </summary>
    public List<Device> GetAllDevices()
    {
        return _graph.Vertices.ToList();
    }

    /// <summary>
    /// Get all connections in the graph
    /// </summary>
    public List<Connection> GetAllConnections()
    {
        return _graph.Edges.Select(e => e.Tag).ToList();
    }

    /// <summary>
    /// Get active connections (seen within last 30 seconds)
    /// </summary>
    public List<Connection> GetActiveConnections()
    {
        return _graph.Edges
            .Select(e => e.Tag)
            .Where(c => c.IsActive)
            .ToList();
    }

    /// <summary>
    /// Get connections for a specific device
    /// </summary>
    public List<Connection> GetDeviceConnections(Device device)
    {
        var outgoing = _graph.OutEdges(device).Select(e => e.Tag);
        var incoming = _graph.InEdges(device).Select(e => e.Tag);

        return outgoing.Concat(incoming).ToList();
    }

    /// <summary>
    /// Get directly connected devices (neighbors)
    /// </summary>
    public List<Device> GetConnectedDevices(Device device)
    {
        if (!_graph.ContainsVertex(device))
            return new List<Device>();

        var outgoing = _graph.OutEdges(device).Select(e => e.Target);
        var incoming = _graph.InEdges(device).Select(e => e.Source);

        return outgoing.Concat(incoming).Distinct().ToList();
    }

    /// <summary>
    /// Find shortest path between two devices
    /// </summary>
    public List<Device>? FindPath(Device source, Device destination)
    {
        try
        {
            var tryGetPath = _graph.ShortestPathsDijkstra(
                edge => 1.0, // All edges have weight 1
                source
            );

            if (tryGetPath(destination, out var path))
            {
                return path.SelectMany(e => new[] { e.Source, e.Target }).Distinct().ToList();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Calculate network statistics
    /// </summary>
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
            TotalBytesTransferred = connections.Sum(c => c.BytesTransferred),
            TotalPackets = connections.Sum(c => c.PacketCount),
            AverageDegree = devices.Count > 0
                ? _graph.Edges.Count() / (double)devices.Count
                : 0
        };
    }

    /// <summary>
    /// Merge graph from another TLScope peer
    /// </summary>
    public void MergeGraph(List<Device> peerDevices, List<Connection> peerConnections)
    {
        Log.Information($"Merging peer graph: {peerDevices.Count} devices, {peerConnections.Count} connections");

        foreach (var device in peerDevices)
        {
            if (!_deviceLookup.ContainsKey(device.MACAddress))
            {
                AddDevice(device);
            }
            else
            {
                UpdateDevice(device);
            }
        }

        foreach (var connection in peerConnections)
        {
            AddConnection(connection);
        }

        Log.Information("Graph merge completed");
    }

    /// <summary>
    /// Cleanup inactive devices (removes devices with no activity for 2+ minutes)
    /// </summary>
    public void CleanupInactiveDevices()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-2);
        var inactiveDevices = _graph.Vertices
            .Where(d => d.LastSeen < cutoff)
            .ToList();

        if (inactiveDevices.Count > 0)
        {
            Log.Information($"[CLEANUP] Removing {inactiveDevices.Count} inactive devices (no activity for 2+ minutes)");

            foreach (var device in inactiveDevices)
            {
                var normalizedMac = device.MACAddress.ToLower();

                // Remove from graph (check if vertex exists first to avoid KeyNotFoundException)
                if (_graph.ContainsVertex(device))
                {
                    _graph.RemoveVertex(device);
                }

                // Remove from device lookup
                if (_deviceLookup.ContainsKey(normalizedMac))
                {
                    _deviceLookup.Remove(normalizedMac);
                }

                // Remove IP mapping
                if (!string.IsNullOrEmpty(device.IPAddress) && _ipToMacLookup.ContainsKey(device.IPAddress))
                {
                    _ipToMacLookup.Remove(device.IPAddress);
                }

                // Remove from database
                _ = DeleteDeviceFromDatabaseAsync(device);

                Log.Debug($"[CLEANUP] Removed device: {device.IPAddress} ({device.MACAddress}), last seen {device.LastSeen:yyyy-MM-dd HH:mm:ss}");
            }

            Log.Information($"[CLEANUP] Cleanup complete. Remaining devices: {_graph.VertexCount}");
        }
    }

    /// <summary>
    /// Reset connection packet rates (RecentPacketCount) every 30 seconds
    /// </summary>
    public void ResetConnectionRates()
    {
        var now = DateTime.UtcNow;
        foreach (var edge in _graph.Edges)
        {
            var connection = edge.Tag;
            var timeSinceLastReset = now - connection.LastRateUpdate;

            // Reset if 30 seconds have passed
            if (timeSinceLastReset.TotalSeconds >= 30)
            {
                connection.RecentPacketCount = 0;
                connection.LastRateUpdate = now;
                Log.Debug($"[RATE-RESET] Connection rate reset: {connection.SourceDevice.IPAddress} → {connection.DestinationDevice.IPAddress}");
            }
        }
    }

    /// <summary>
    /// Get graph as DOT format (for external visualization)
    /// </summary>
    public string ExportToDot()
    {
        var dot = new System.Text.StringBuilder();
        dot.AppendLine("digraph TLScope {");

        // Add vertices
        foreach (var device in _graph.Vertices)
        {
            var label = device.DeviceName ?? device.Hostname ?? device.IPAddress;
            dot.AppendLine($"  \"{device.MACAddress}\" [label=\"{label}\"];");
        }

        // Add edges
        foreach (var edge in _graph.Edges)
        {
            var label = $"{edge.Tag.Protocol}";
            if (edge.Tag.DestinationPort.HasValue)
                label += $":{edge.Tag.DestinationPort}";

            dot.AppendLine($"  \"{edge.Source.MACAddress}\" -> \"{edge.Target.MACAddress}\" [label=\"{label}\"];");
        }

        dot.AppendLine("}");
        return dot.ToString();
    }

    /// <summary>
    /// Get protocol distribution across all connections
    /// </summary>
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

    /// <summary>
    /// Get port distribution (destination ports) across all connections
    /// </summary>
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

    /// <summary>
    /// Clear the entire graph
    /// </summary>
    public void Clear()
    {
        _graph.Clear();
        _deviceLookup.Clear();
        _ipToMacLookup.Clear();
        Log.Information("Graph cleared");
    }

    /// <summary>
    /// Load devices from database on startup
    /// </summary>
    public async Task LoadDevicesFromDatabaseAsync()
    {
        try
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var devices = await context.Devices.ToListAsync();

            Log.Information($"Loading {devices.Count} devices from database");

            foreach (var device in devices)
            {
                // Normalize MAC address to lowercase for consistent lookups
                var normalizedMac = device.MACAddress.ToLower();

                // Add to graph without saving back to database
                if (!_deviceLookup.ContainsKey(normalizedMac))
                {
                    _graph.AddVertex(device);
                    _deviceLookup[normalizedMac] = device;

                    if (!string.IsNullOrEmpty(device.IPAddress))
                    {
                        _ipToMacLookup[device.IPAddress] = normalizedMac;
                    }
                }
            }

            Log.Information($"Loaded {devices.Count} devices from database");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load devices from database");
        }
    }

    /// <summary>
    /// Save device to database (async, fire-and-forget)
    /// </summary>
    private async Task SaveDeviceToDatabaseAsync(Device device)
    {
        try
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            // Check if device exists in database
            var existingDevice = await context.Devices
                .FirstOrDefaultAsync(d => d.MACAddress == device.MACAddress);

            if (existingDevice != null)
            {
                // Update existing device
                existingDevice.IPAddress = device.IPAddress;
                existingDevice.Hostname = device.Hostname;
                existingDevice.DeviceName = device.DeviceName;
                existingDevice.Vendor = device.Vendor;
                existingDevice.OperatingSystem = device.OperatingSystem;
                existingDevice.LastSeen = device.LastSeen;
                existingDevice.PacketCount = device.PacketCount;
                existingDevice.BytesTransferred = device.BytesTransferred;
                existingDevice.OpenPorts = device.OpenPorts;
                existingDevice.IsTLScopePeer = device.IsTLScopePeer;
                existingDevice.TLSPeerId = device.TLSPeerId;

                context.Devices.Update(existingDevice);
            }
            else
            {
                // Add new device
                context.Devices.Add(device);
            }

            await context.SaveChangesAsync();
            Log.Debug($"Saved device to database: {device.MACAddress}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to save device to database: {device.MACAddress}");
        }
    }

    /// <summary>
    /// Delete device from database (async, fire-and-forget)
    /// </summary>
    private async Task DeleteDeviceFromDatabaseAsync(Device device)
    {
        try
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            var existingDevice = await context.Devices
                .FirstOrDefaultAsync(d => d.MACAddress == device.MACAddress);

            if (existingDevice != null)
            {
                context.Devices.Remove(existingDevice);
                await context.SaveChangesAsync();
                Log.Debug($"Deleted device from database: {device.MACAddress}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to delete device from database: {device.MACAddress}");
        }
    }

    // ============================================================================
    // TOPOLOGY ANALYSIS METHODS
    // ============================================================================

    /// <summary>
    /// Get all devices that are identified as gateways/routers
    /// </summary>
    public List<Device> GetGatewayDevices()
    {
        return GetAllDevices().Where(d => d.IsGateway).ToList();
    }

    /// <summary>
    /// Get the default gateway device
    /// </summary>
    public Device? GetDefaultGateway()
    {
        return GetAllDevices().FirstOrDefault(d => d.IsDefaultGateway);
    }

    /// <summary>
    /// Get devices organized by topology tier
    /// Tier 1: Remote/Internet devices (virtual devices)
    /// Tier 2: Gateways/Routers
    /// Tier 3: Local devices
    /// </summary>
    public (List<Device> RemoteDevices, List<Device> Gateways, List<Device> LocalDevices) GetTopologyTiers()
    {
        var allDevices = GetAllDevices();

        var remoteDevices = allDevices.Where(d => d.IsVirtualDevice).ToList();
        var gateways = allDevices.Where(d => d.IsGateway && !d.IsVirtualDevice).ToList();
        var localDevices = allDevices.Where(d => !d.IsVirtualDevice && !d.IsGateway && d.IsLocal).ToList();

        Log.Debug($"[TOPOLOGY] Tiers: {remoteDevices.Count} remote, {gateways.Count} gateways, {localDevices.Count} local");

        return (remoteDevices, gateways, localDevices);
    }

    /// <summary>
    /// Get connections of a specific type
    /// </summary>
    public List<Connection> GetConnectionsByType(ConnectionType type)
    {
        return GetAllConnections().Where(c => c.Type == type).ToList();
    }

    /// <summary>
    /// Get direct L2 connections (same network segment)
    /// </summary>
    public List<Connection> GetDirectConnections()
    {
        return GetConnectionsByType(ConnectionType.DirectL2);
    }

    /// <summary>
    /// Get routed L3 connections (through gateway)
    /// </summary>
    public List<Connection> GetRoutedConnections()
    {
        return GetConnectionsByType(ConnectionType.RoutedL3);
    }

    /// <summary>
    /// Get Internet connections (to remote hosts)
    /// </summary>
    public List<Connection> GetInternetConnections()
    {
        return GetConnectionsByType(ConnectionType.Internet);
    }

    /// <summary>
    /// Get TLS peer connections
    /// </summary>
    public List<Connection> GetTLSPeerConnections()
    {
        return GetConnectionsByType(ConnectionType.TLSPeer);
    }

    /// <summary>
    /// Get connections from a device to the gateway
    /// </summary>
    public List<Connection> GetConnectionsToGateway(Device device)
    {
        var gateways = GetGatewayDevices();
        var allConnections = GetAllConnections();

        return allConnections.Where(c =>
            (c.SourceDevice == device && gateways.Contains(c.DestinationDevice)) ||
            (c.DestinationDevice == device && gateways.Contains(c.SourceDevice))
        ).ToList();
    }

    /// <summary>
    /// Get connections from gateway to internet
    /// </summary>
    public List<Connection> GetGatewayToInternetConnections()
    {
        var gateways = GetGatewayDevices();
        var allConnections = GetAllConnections();

        return allConnections.Where(c =>
            (gateways.Contains(c.SourceDevice) && c.DestinationDevice.IsVirtualDevice) ||
            (gateways.Contains(c.DestinationDevice) && c.SourceDevice.IsVirtualDevice)
        ).ToList();
    }

    /// <summary>
    /// Classify connection type for an existing connection
    /// Used to update connection types after topology changes
    /// </summary>
    public ConnectionType ClassifyConnection(Connection connection)
    {
        // TLS peer connections always stay as TLSPeer
        if (connection.IsTLSPeerConnection)
            return ConnectionType.TLSPeer;

        // If we have TTL data, use it
        if (connection.AverageTTL.HasValue)
        {
            var dest = connection.DestinationDevice;

            if (dest.IsVirtualDevice)
                return ConnectionType.Internet;

            if (dest.IsLocal)
            {
                if (connection.AverageTTL >= 62)
                    return ConnectionType.DirectL2;
                else if (connection.AverageTTL >= 50)
                    return ConnectionType.RoutedL3;
                else
                    return ConnectionType.Internet;
            }

            return ConnectionType.Internet;
        }

        // Fallback: classify based on device properties only
        if (connection.DestinationDevice.IsVirtualDevice)
            return ConnectionType.Internet;

        if (connection.DestinationDevice.IsLocal)
            return ConnectionType.DirectL2; // Assume direct if no TTL data

        return ConnectionType.Internet;
    }

    /// <summary>
    /// Update connection types for all connections
    /// Call this after gateway detection or topology changes
    /// </summary>
    public void UpdateConnectionTypes()
    {
        var connections = GetAllConnections();
        int updated = 0;

        foreach (var connection in connections)
        {
            var newType = ClassifyConnection(connection);
            if (newType != connection.Type)
            {
                connection.Type = newType;
                updated++;
            }
        }

        if (updated > 0)
        {
            Log.Information($"[TOPOLOGY] Updated {updated} connection types");
        }
    }
}

/// <summary>
/// Network statistics
/// </summary>
public class NetworkStatistics
{
    public int TotalDevices { get; set; }
    public int ActiveDevices { get; set; }
    public int TotalConnections { get; set; }
    public int ActiveConnections { get; set; }
    public long TotalBytesTransferred { get; set; }
    public long TotalPackets { get; set; }
    public double AverageDegree { get; set; }

    public override string ToString()
    {
        return $"Devices: {ActiveDevices}/{TotalDevices} | " +
               $"Connections: {ActiveConnections}/{TotalConnections} | " +
               $"Bytes: {LatexHelpers.FormatBytes(TotalBytesTransferred)}";
    }
}
