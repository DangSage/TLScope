using System.Net;

namespace TLScope.Models;

/// <summary>
/// Represents a cluster of related devices (grouped by subnet)
/// Used to reduce visual complexity in large network graphs
/// </summary>
public class DeviceCluster
{
    /// <summary>
    /// Unique identifier for the cluster
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the cluster (e.g., "192.168.1.x")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Subnet prefix that defines this cluster (e.g., "192.168.1")
    /// </summary>
    public string SubnetPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Devices in this cluster
    /// </summary>
    public List<Device> Devices { get; set; } = new();

    /// <summary>
    /// Whether this cluster is currently expanded (showing individual devices)
    /// </summary>
    public bool IsExpanded { get; set; } = false;

    /// <summary>
    /// Total packet count for all devices in cluster
    /// </summary>
    public long TotalPacketCount => Devices.Sum(d => d.PacketCount);

    /// <summary>
    /// Total bytes transferred for all devices in cluster
    /// </summary>
    public long TotalBytesTransferred => Devices.Sum(d => d.BytesTransferred);

    /// <summary>
    /// Number of active devices in cluster
    /// </summary>
    public int ActiveDeviceCount => Devices.Count(d => d.IsActive);

    /// <summary>
    /// Get a short label for the cluster to display in graph
    /// </summary>
    public string GetLabel()
    {
        if (IsExpanded)
            return Name;

        var activeCount = ActiveDeviceCount;
        var totalCount = Devices.Count;

        if (activeCount == totalCount)
            return $"{Name} ({totalCount})";
        else
            return $"{Name} ({activeCount}/{totalCount})";
    }

    /// <summary>
    /// Get a detailed description of the cluster
    /// </summary>
    public string GetDescription()
    {
        return $"{Name}: {ActiveDeviceCount} active, {Devices.Count} total devices";
    }
}
