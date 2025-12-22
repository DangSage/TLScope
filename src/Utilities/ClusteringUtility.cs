using System.Net;
using TLScope.Models;

namespace TLScope.Utilities;

/// <summary>
/// Utility class for clustering devices to reduce visual complexity in large networks
/// </summary>
public static class ClusteringUtility
{
    /// <summary>
    /// Group devices by subnet (first 3 octets of IP address)
    /// Returns a list of clusters, where each cluster represents a /24 subnet
    /// </summary>
    public static List<DeviceCluster> ClusterDevicesBySubnet(List<Device> devices)
    {
        var clusters = new Dictionary<string, DeviceCluster>();

        foreach (var device in devices)
        {
            var subnetPrefix = GetSubnetPrefix(device.IPAddress);
            if (string.IsNullOrEmpty(subnetPrefix))
                continue; // Skip devices with invalid IPs

            if (!clusters.ContainsKey(subnetPrefix))
            {
                clusters[subnetPrefix] = new DeviceCluster
                {
                    Id = $"cluster_{subnetPrefix.Replace(".", "_")}",
                    Name = $"{subnetPrefix}.x",
                    SubnetPrefix = subnetPrefix,
                    Devices = new List<Device>()
                };
            }

            clusters[subnetPrefix].Devices.Add(device);
        }

        return clusters.Values.OrderByDescending(c => c.TotalPacketCount).ToList();
    }

    /// <summary>
    /// Get subnet prefix (first 3 octets) from IP address
    /// Returns empty string if IP is invalid
    /// </summary>
    private static string GetSubnetPrefix(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
            return string.Empty;

        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4)
            return string.Empty; // Only IPv4

        return $"{bytes[0]}.{bytes[1]}.{bytes[2]}";
    }

    /// <summary>
    /// Determine if clustering should be used based on device count
    /// </summary>
    public static bool ShouldUseClustering(int deviceCount)
    {
        // Use clustering for 50+ devices
        return deviceCount >= 50;
    }

    /// <summary>
    /// Intelligently decide which clusters should be collapsed vs expanded
    /// Strategy: Expand small clusters (1-3 devices), collapse large ones (4+)
    /// </summary>
    public static void AutoConfigureClusters(List<DeviceCluster> clusters)
    {
        foreach (var cluster in clusters)
        {
            // Expand very small clusters (easier to see individual devices)
            if (cluster.Devices.Count <= 3)
            {
                cluster.IsExpanded = true;
            }
            // Collapse larger clusters
            else
            {
                cluster.IsExpanded = false;
            }
        }
    }

    /// <summary>
    /// Get the most important device in a cluster to represent it
    /// (highest packet count, or first active device)
    /// </summary>
    public static Device GetRepresentativeDevice(DeviceCluster cluster)
    {
        // Prefer active devices
        var activeDevices = cluster.Devices.Where(d => d.IsActive).ToList();
        if (activeDevices.Any())
        {
            return activeDevices.OrderByDescending(d => d.PacketCount).First();
        }

        // Fallback to any device with highest packet count
        return cluster.Devices.OrderByDescending(d => d.PacketCount).First();
    }

    /// <summary>
    /// Merge very small clusters (1-2 devices) into an "Other" cluster
    /// to reduce clutter when there are many tiny subnets
    /// </summary>
    public static List<DeviceCluster> MergeSmallClusters(List<DeviceCluster> clusters, int minClusterSize = 3)
    {
        var largeClusters = clusters.Where(c => c.Devices.Count >= minClusterSize).ToList();
        var smallClusters = clusters.Where(c => c.Devices.Count < minClusterSize).ToList();

        // If we have small clusters, merge them into an "Other" cluster
        if (smallClusters.Any())
        {
            var otherCluster = new DeviceCluster
            {
                Id = "cluster_other",
                Name = "Other",
                SubnetPrefix = "mixed",
                Devices = smallClusters.SelectMany(c => c.Devices).ToList(),
                IsExpanded = false
            };

            largeClusters.Add(otherCluster);
        }

        return largeClusters;
    }

    /// <summary>
    /// Calculate aggregated connections between clusters
    /// Returns dictionary of (cluster1, cluster2) -> total packet count
    /// </summary>
    public static Dictionary<(string, string), int> AggregateClusterConnections(
        List<DeviceCluster> clusters,
        Dictionary<(Device, Device), int> deviceConnections)
    {
        var clusterConnections = new Dictionary<(string, string), int>();

        // Build device to cluster mapping
        var deviceToCluster = new Dictionary<Device, DeviceCluster>();
        foreach (var cluster in clusters)
        {
            foreach (var device in cluster.Devices)
            {
                deviceToCluster[device] = cluster;
            }
        }

        // Aggregate device connections to cluster connections
        foreach (var ((dev1, dev2), packetCount) in deviceConnections)
        {
            if (!deviceToCluster.ContainsKey(dev1) || !deviceToCluster.ContainsKey(dev2))
                continue;

            var cluster1 = deviceToCluster[dev1];
            var cluster2 = deviceToCluster[dev2];

            // Skip self-connections within same cluster
            if (cluster1.Id == cluster2.Id)
                continue;

            // Create normalized key (always store smaller ID first for consistency)
            var key = string.CompareOrdinal(cluster1.Id, cluster2.Id) < 0
                ? (cluster1.Id, cluster2.Id)
                : (cluster2.Id, cluster1.Id);

            if (!clusterConnections.ContainsKey(key))
                clusterConnections[key] = 0;

            clusterConnections[key] += packetCount;
        }

        return clusterConnections;
    }
}
