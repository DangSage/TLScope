using TLScope.Models;

namespace TLScope.Utilities;

/// <summary>
/// Utility for edge bundling to reduce visual clutter in dense networks
/// </summary>
public static class EdgeBundlingUtility
{
    /// <summary>
    /// Represents a bundled edge group
    /// </summary>
    public class EdgeBundle
    {
        public List<(Device source, Device dest, int strength)> Edges { get; set; } = new();
        public Device? HubDevice { get; set; }
        public int TotalStrength => Edges.Sum(e => e.strength);
    }

    /// <summary>
    /// Identify hub devices (devices with many connections)
    /// </summary>
    public static List<Device> IdentifyHubs(
        List<Device> devices,
        Dictionary<(string, string), int> connections,
        int minConnectionsForHub = 5)
    {
        var connectionCounts = new Dictionary<Device, int>();

        foreach (var device in devices)
        {
            int count = connections.Count(c =>
                c.Key.Item1 == device.MACAddress ||
                c.Key.Item2 == device.MACAddress);

            if (count >= minConnectionsForHub)
                connectionCounts[device] = count;
        }

        return connectionCounts
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>
    /// Bundle edges that connect to the same hub device
    /// Returns a list of edge bundles grouped by hub
    /// </summary>
    public static List<EdgeBundle> BundleEdgesByHub(
        List<Device> devices,
        Dictionary<(string, string), int> connections)
    {
        var bundles = new List<EdgeBundle>();

        // Identify hub devices (devices with 5+ connections)
        var hubs = IdentifyHubs(devices, connections, minConnectionsForHub: 5);

        if (!hubs.Any())
            return bundles;

        // Create device lookup by MAC
        var deviceLookup = devices.ToDictionary(d => d.MACAddress);

        // Group edges by hub
        foreach (var hub in hubs)
        {
            var bundle = new EdgeBundle { HubDevice = hub };

            foreach (var ((mac1, mac2), strength) in connections)
            {
                Device? dev1 = deviceLookup.GetValueOrDefault(mac1);
                Device? dev2 = deviceLookup.GetValueOrDefault(mac2);

                if (dev1 == null || dev2 == null)
                    continue;

                // Check if this edge connects to the hub
                if (dev1 == hub && dev2 != hub)
                {
                    bundle.Edges.Add((hub, dev2, strength));
                }
                else if (dev2 == hub && dev1 != hub)
                {
                    bundle.Edges.Add((dev1, hub, strength));
                }
            }

            // Only add bundle if it has edges
            if (bundle.Edges.Any())
                bundles.Add(bundle);
        }

        return bundles;
    }

    /// <summary>
    /// Calculate control points for bundled edges using force-directed bundling
    /// Returns a list of waypoints for each edge to create smooth curves
    /// </summary>
    public static Dictionary<(Device, Device), List<(double x, double y)>> CalculateBundlePaths(
        List<EdgeBundle> bundles,
        Dictionary<Device, (double x, double y)> devicePositions)
    {
        var bundlePaths = new Dictionary<(Device, Device), List<(double x, double y)>>();

        foreach (var bundle in bundles)
        {
            if (bundle.HubDevice == null || !devicePositions.ContainsKey(bundle.HubDevice))
                continue;

            var hubPos = devicePositions[bundle.HubDevice];

            foreach (var (source, dest, _) in bundle.Edges)
            {
                if (!devicePositions.ContainsKey(source) || !devicePositions.ContainsKey(dest))
                    continue;

                var srcPos = devicePositions[source];
                var dstPos = devicePositions[dest];

                // Create a smooth curve through the hub
                // Use quadratic Bezier curve: source -> hub -> destination
                var waypoints = new List<(double x, double y)>
                {
                    srcPos,
                    hubPos,
                    dstPos
                };

                bundlePaths[(source, dest)] = waypoints;
            }
        }

        return bundlePaths;
    }

    /// <summary>
    /// Determine if edge bundling should be used based on network characteristics
    /// </summary>
    public static bool ShouldUseBundling(
        List<Device> devices,
        Dictionary<(string, string), int> connections)
    {
        // Use bundling for networks with 30+ devices and 50+ connections
        if (devices.Count < 30 || connections.Count < 50)
            return false;

        // Check if there are hub devices that would benefit from bundling
        var hubs = IdentifyHubs(devices, connections, minConnectionsForHub: 5);
        return hubs.Count >= 2;
    }

    /// <summary>
    /// Simplify edge list by bundling parallel edges (same source and destination)
    /// Returns deduplicated edges with aggregated strength
    /// </summary>
    public static Dictionary<(Device, Device), int> SimplifyParallelEdges(
        Dictionary<(string, string), int> connections,
        Dictionary<string, Device> deviceLookup)
    {
        var simplifiedEdges = new Dictionary<(Device, Device), int>();

        foreach (var ((mac1, mac2), strength) in connections)
        {
            if (!deviceLookup.TryGetValue(mac1, out var dev1) ||
                !deviceLookup.TryGetValue(mac2, out var dev2))
                continue;

            // Create normalized key (always same device order)
            var key = dev1.Id < dev2.Id ? (dev1, dev2) : (dev2, dev1);

            if (!simplifiedEdges.ContainsKey(key))
                simplifiedEdges[key] = 0;

            simplifiedEdges[key] += strength;
        }

        return simplifiedEdges;
    }

    /// <summary>
    /// Calculate visual thickness for edge based on bundled strength
    /// Returns number of parallel lines to draw (1-3)
    /// </summary>
    public static int GetEdgeThickness(int bundledStrength, int maxStrength)
    {
        if (maxStrength == 0)
            return 1;

        double ratio = (double)bundledStrength / maxStrength;

        if (ratio > 0.66)
            return 3;  // Thick edge (triple line)
        else if (ratio > 0.33)
            return 2;  // Medium edge (double line)
        else
            return 1;  // Thin edge (single line)
    }
}
