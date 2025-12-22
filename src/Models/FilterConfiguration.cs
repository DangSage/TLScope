using System.Text.Json;
using TLScope.Utilities;

namespace TLScope.Models;

/// <summary>
/// Configuration for IP address filtering
/// </summary>
public class FilterConfiguration
{
    private static readonly string ConfigFilePath = ConfigurationHelper.GetConfigFilePath("filters.json");

    /// <summary>
    /// Filter loopback addresses (127.0.0.0/8)
    /// </summary>
    public bool FilterLoopback { get; set; } = true;

    /// <summary>
    /// Filter broadcast address (255.255.255.255)
    /// </summary>
    public bool FilterBroadcast { get; set; } = true;

    /// <summary>
    /// Filter multicast addresses (224.0.0.0/4)
    /// </summary>
    public bool FilterMulticast { get; set; } = true;

    /// <summary>
    /// Filter link-local addresses (169.254.0.0/16)
    /// </summary>
    public bool FilterLinkLocal { get; set; } = true;

    /// <summary>
    /// Filter reserved address ranges
    /// </summary>
    public bool FilterReserved { get; set; } = true;

    /// <summary>
    /// Block duplicate IP addresses (same IP on different MACs)
    /// Note: Disabled by default to allow multiple interfaces per IP (e.g., load balancers, routers)
    /// </summary>
    public bool BlockDuplicateIPs { get; set; } = false;

    /// <summary>
    /// Filter HTTP/HTTPS traffic (ports 80, 443, 8080, 8443)
    /// </summary>
    public bool FilterHttpTraffic { get; set; } = false;

    /// <summary>
    /// Filter non-local traffic (traffic to/from public internet addresses)
    /// Only show local network traffic (loopback, link-local, and RFC 1918 private: 10.x, 172.16-31.x, 192.168.x)
    /// </summary>
    public bool FilterNonLocalTraffic { get; set; } = true;

    /// <summary>
    /// Show remote/internet hosts in graph (default: true)
    /// When false, virtual devices (remote hosts without local MACs) are not displayed in graph
    /// </summary>
    public bool ShowRemoteHosts { get; set; } = true;

    /// <summary>
    /// Show inactive devices (default: true)
    /// When false, devices that have not been seen recently are hidden from views
    /// </summary>
    public bool ShowInactiveDevices { get; set; } = true;

    /// <summary>
    /// Automatically remove inactive devices every 30 seconds (default: true)
    /// When false, inactive devices remain in the database until manually cleared
    /// </summary>
    public bool AutoRemoveInactiveDevices { get; set; } = true;

    /// <summary>
    /// Statistics: Total addresses filtered
    /// </summary>
    public long TotalFiltered { get; set; } = 0;

    /// <summary>
    /// Statistics: Duplicate IPs blocked
    /// </summary>
    public long DuplicatesBlocked { get; set; } = 0;

    /// <summary>
    /// Statistics: HTTP/HTTPS traffic filtered
    /// </summary>
    public long HttpTrafficFiltered { get; set; } = 0;

    /// <summary>
    /// Statistics: Non-local traffic filtered
    /// </summary>
    public long NonLocalTrafficFiltered { get; set; } = 0;

    /// <summary>
    /// Load filter configuration from file
    /// </summary>
    public static FilterConfiguration Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<FilterConfiguration>(json);
                return config ?? new FilterConfiguration();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load filter configuration: {ex.Message}");
        }

        // Return defaults if file doesn't exist or fails to load
        return new FilterConfiguration();
    }

    /// <summary>
    /// Save filter configuration to file
    /// </summary>
    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to save filter configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply command-line overrides to configuration
    /// </summary>
    public void ApplyOverrides(
        bool? filterLoopback = null,
        bool? filterBroadcast = null,
        bool? filterMulticast = null,
        bool? filterLinkLocal = null,
        bool? filterReserved = null,
        bool? blockDuplicateIPs = null,
        bool? filterHttpTraffic = null,
        bool? filterNonLocalTraffic = null,
        bool? showRemoteHosts = null,
        bool? showInactiveDevices = null,
        bool? autoRemoveInactiveDevices = null)
    {
        if (filterLoopback.HasValue) FilterLoopback = filterLoopback.Value;
        if (filterBroadcast.HasValue) FilterBroadcast = filterBroadcast.Value;
        if (filterMulticast.HasValue) FilterMulticast = filterMulticast.Value;
        if (filterLinkLocal.HasValue) FilterLinkLocal = filterLinkLocal.Value;
        if (filterReserved.HasValue) FilterReserved = filterReserved.Value;
        if (blockDuplicateIPs.HasValue) BlockDuplicateIPs = blockDuplicateIPs.Value;
        if (filterHttpTraffic.HasValue) FilterHttpTraffic = filterHttpTraffic.Value;
        if (filterNonLocalTraffic.HasValue) FilterNonLocalTraffic = filterNonLocalTraffic.Value;
        if (showRemoteHosts.HasValue) ShowRemoteHosts = showRemoteHosts.Value;
        if (showInactiveDevices.HasValue) ShowInactiveDevices = showInactiveDevices.Value;
        if (autoRemoveInactiveDevices.HasValue) AutoRemoveInactiveDevices = autoRemoveInactiveDevices.Value;
    }

    /// <summary>
    /// Get a summary of current filter settings
    /// </summary>
    public string GetSummary()
    {
        var enabled = new List<string>();

        if (FilterLoopback) enabled.Add("Loopback");
        if (FilterBroadcast) enabled.Add("Broadcast");
        if (FilterMulticast) enabled.Add("Multicast");
        if (FilterLinkLocal) enabled.Add("Link-local");
        if (FilterReserved) enabled.Add("Reserved");
        if (BlockDuplicateIPs) enabled.Add("Duplicate IPs");
        if (FilterHttpTraffic) enabled.Add("HTTP/HTTPS");
        if (FilterNonLocalTraffic) enabled.Add("Non-Local");
        if (!ShowRemoteHosts) enabled.Add("Hide Remote Hosts");
        if (!ShowInactiveDevices) enabled.Add("Hide Inactive Devices");
        if (AutoRemoveInactiveDevices) enabled.Add("Auto-Remove Inactive");

        return enabled.Count > 0
            ? string.Join(", ", enabled)
            : "No filters enabled";
    }
}
