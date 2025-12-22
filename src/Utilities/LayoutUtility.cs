using TLScope.Models;

namespace TLScope.Utilities;

/// <summary>
/// Layout algorithms for network graph visualization
/// </summary>
public static class LayoutUtility
{
    /// <summary>
    /// Layout type for network graph
    /// </summary>
    public enum LayoutType
    {
        ForceDirected,  // Default physics-based layout
        Hierarchical,   // Tree-like layout with clusters
        Circular,       // Devices arranged in a circle
        Grid            // Grid-based layout
    }

    /// <summary>
    /// Calculate hierarchical layout with device clusters
    /// Places clusters in columns, with devices within each cluster stacked vertically
    /// </summary>
    public static Dictionary<Device, (double x, double y)> CalculateHierarchicalLayout(
        List<DeviceCluster> clusters,
        int width,
        int height,
        Device? userDevice = null)
    {
        var positions = new Dictionary<Device, (double x, double y)>();

        if (!clusters.Any())
            return positions;

        int clusterSpacing = width / (clusters.Count + 1);
        int currentX = clusterSpacing;

        foreach (var cluster in clusters)
        {
            int devicesInCluster = cluster.IsExpanded ? cluster.Devices.Count : 1;
            int deviceSpacing = Math.Max(3, height / (devicesInCluster + 1));
            int currentY = deviceSpacing;

            if (cluster.IsExpanded)
            {
                foreach (var device in cluster.Devices)
                {
                    positions[device] = (currentX, currentY);
                    currentY += deviceSpacing;
                }
            }
            else
            {
                var representative = ClusteringUtility.GetRepresentativeDevice(cluster);
                positions[representative] = (currentX, height / 2);
            }

            currentX += clusterSpacing;
        }

        if (userDevice != null && positions.ContainsKey(userDevice))
        {
            positions[userDevice] = (width / 2.0, height / 2.0);
        }

        return positions;
    }

    /// <summary>
    /// Calculate circular layout
    /// Arranges devices in a circle, useful for small to medium networks
    /// </summary>
    public static Dictionary<Device, (double x, double y)> CalculateCircularLayout(
        List<Device> devices,
        int width,
        int height,
        Device? userDevice = null)
    {
        var positions = new Dictionary<Device, (double x, double y)>();

        if (!devices.Any())
            return positions;

        double centerX = width / 2.0;
        double centerY = height / 2.0;

        double radius = Math.Min(width, height) / 2.0 - 5;

        var devicesToArrange = userDevice != null
            ? devices.Where(d => d != userDevice).ToList()
            : devices;

        for (int i = 0; i < devicesToArrange.Count; i++)
        {
            double angle = 2 * Math.PI * i / devicesToArrange.Count;
            double x = centerX + radius * Math.Cos(angle);
            double y = centerY + radius * Math.Sin(angle);

            positions[devicesToArrange[i]] = (x, y);
        }

        if (userDevice != null)
        {
            positions[userDevice] = (centerX, centerY);
        }

        return positions;
    }

    /// <summary>
    /// Calculate grid layout
    /// Arranges devices in a regular grid pattern
    /// </summary>
    public static Dictionary<Device, (double x, double y)> CalculateGridLayout(
        List<Device> devices,
        int width,
        int height)
    {
        var positions = new Dictionary<Device, (double x, double y)>();

        if (!devices.Any())
            return positions;

        int cols = (int)Math.Ceiling(Math.Sqrt(devices.Count));
        int rows = (int)Math.Ceiling((double)devices.Count / cols);

        double cellWidth = (double)width / (cols + 1);
        double cellHeight = (double)height / (rows + 1);

        for (int i = 0; i < devices.Count; i++)
        {
            int row = i / cols;
            int col = i % cols;

            double x = (col + 1) * cellWidth;
            double y = (row + 1) * cellHeight;

            positions[devices[i]] = (x, y);
        }

        return positions;
    }

    /// <summary>
    /// Calculate star layout (hub-and-spoke)
    /// Places central node in middle with others arranged around it
    /// </summary>
    public static Dictionary<Device, (double x, double y)> CalculateStarLayout(
        List<Device> devices,
        Device hubDevice,
        int width,
        int height)
    {
        var positions = new Dictionary<Device, (double x, double y)>();

        if (!devices.Any())
            return positions;

        double centerX = width / 2.0;
        double centerY = height / 2.0;

        positions[hubDevice] = (centerX, centerY);

        double radius = Math.Min(width, height) / 2.0 - 5;

        var spokeDevices = devices.Where(d => d != hubDevice).ToList();

        for (int i = 0; i < spokeDevices.Count; i++)
        {
            double angle = 2 * Math.PI * i / spokeDevices.Count;
            double x = centerX + radius * Math.Cos(angle);
            double y = centerY + radius * Math.Sin(angle);

            positions[spokeDevices[i]] = (x, y);
        }

        return positions;
    }

    /// <summary>
    /// Determine best layout type based on network characteristics
    /// </summary>
    public static LayoutType SuggestLayout(List<Device> devices, List<Connection> connections)
    {
        int deviceCount = devices.Count;

        if (deviceCount > 100)
            return LayoutType.Grid;

        if (deviceCount > 30)
        {
            var clusters = ClusteringUtility.ClusterDevicesBySubnet(devices);
            if (clusters.Count >= 3 && clusters.Count <= 10)
                return LayoutType.Hierarchical;
        }

        if (deviceCount <= 12)
            return LayoutType.Circular;

        return LayoutType.ForceDirected;
    }
}
