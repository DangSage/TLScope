using System.Net;

namespace TLScope.Utilities;

/// <summary>
/// Utility class for validating and filtering IP addresses
/// </summary>
public static class IPAddressValidator
{
    /// <summary>
    /// Check if IP address is in the loopback range (127.0.0.0/8)
    /// </summary>
    public static bool IsLoopbackAddress(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
            return false;

        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4) return false; // Only IPv4

        return bytes[0] == 127;
    }

    /// <summary>
    /// Check if IP address is the broadcast address (255.255.255.255)
    /// </summary>
    public static bool IsBroadcastAddress(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
            return false;

        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4) return false; // Only IPv4

        return bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255;
    }

    /// <summary>
    /// Check if IP address is in the multicast range (224.0.0.0/4 = 224-239.x.x.x)
    /// </summary>
    public static bool IsMulticastAddress(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
            return false;

        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4) return false; // Only IPv4

        // Multicast: 224.0.0.0 to 239.255.255.255
        return bytes[0] >= 224 && bytes[0] <= 239;
    }

    /// <summary>
    /// Check if IP address is in the link-local range (169.254.0.0/16)
    /// </summary>
    public static bool IsLinkLocalAddress(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
            return false;

        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4) return false; // Only IPv4

        return bytes[0] == 169 && bytes[1] == 254;
    }

    /// <summary>
    /// Check if IP address is in reserved ranges (0.0.0.0/8, 240.0.0.0/4)
    /// </summary>
    public static bool IsReservedAddress(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
            return false;

        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4) return false; // Only IPv4

        // 0.0.0.0/8 - Current network (0-0.255.255.255)
        if (bytes[0] == 0)
            return true;

        // 240.0.0.0/4 - Reserved (240-255.x.x.x, except 255.255.255.255 which is handled separately)
        if (bytes[0] >= 240 && bytes[0] <= 254)
            return true;

        return false;
    }

    /// <summary>
    /// Check if IP address is a private network address (10.x.x.x, 172.16-31.x.x, 192.168.x.x)
    /// Note: These are NOT filtered by default per user configuration
    /// </summary>
    public static bool IsPrivateAddress(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
            return false;

        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4) return false; // Only IPv4

        // 10.0.0.0/8
        if (bytes[0] == 10)
            return true;

        // 172.16.0.0/12 (172.16.0.0 - 172.31.255.255)
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            return true;

        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168)
            return true;

        return false;
    }

    /// <summary>
    /// Check if IP address is a local address (private, loopback, or link-local)
    /// Used for filtering non-local (internet) traffic
    /// </summary>
    public static bool IsLocalAddress(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
            return false;

        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4) return false; // Only IPv4

        // Loopback: 127.0.0.0/8 (127.x.x.x)
        if (bytes[0] == 127)
            return true;

        // Link-local: 169.254.0.0/16 (169.254.x.x)
        if (bytes[0] == 169 && bytes[1] == 254)
            return true;

        // RFC 1918 private addresses
        // 10.0.0.0/8
        if (bytes[0] == 10)
            return true;

        // 172.16.0.0/12 (172.16.0.0 - 172.31.255.255)
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            return true;

        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168)
            return true;

        return false;
    }

    /// <summary>
    /// Check if IP address is a utility/special-use address that should be filtered
    /// </summary>
    public static bool IsUtilityAddress(string ipAddress,
        bool filterLoopback = true,
        bool filterBroadcast = true,
        bool filterMulticast = true,
        bool filterLinkLocal = true,
        bool filterReserved = true)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return true;

        if (filterLoopback && IsLoopbackAddress(ipAddress))
            return true;

        if (filterBroadcast && IsBroadcastAddress(ipAddress))
            return true;

        if (filterMulticast && IsMulticastAddress(ipAddress))
            return true;

        if (filterLinkLocal && IsLinkLocalAddress(ipAddress))
            return true;

        if (filterReserved && IsReservedAddress(ipAddress))
            return true;

        return false;
    }

    /// <summary>
    /// Check if MAC address is a utility/special-use address
    /// </summary>
    public static bool IsUtilityMAC(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
            return true;

        // Normalize MAC address (remove colons, hyphens, dots)
        var normalized = macAddress.Replace(":", "").Replace("-", "").Replace(".", "").ToUpperInvariant();

        if (normalized.Length != 12)
            return true; // Invalid MAC

        // Broadcast MAC: FF:FF:FF:FF:FF:FF
        if (normalized == "FFFFFFFFFFFF")
            return true;

        // IANA IPv4 multicast OUI: 00:00:5E (used for VRRP, HSRP, etc.)
        if (normalized.StartsWith("00005E"))
            return true;

        // Multicast MAC: least significant bit of first octet is 1
        // Parse first two hex characters
        if (int.TryParse(normalized.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int firstOctet))
        {
            if ((firstOctet & 0x01) == 1)
                return true; // Multicast
        }

        return false;
    }

    /// <summary>
    /// Get a description of why an IP address is filtered
    /// </summary>
    public static string GetFilterReason(string ipAddress)
    {
        if (IsLoopbackAddress(ipAddress))
            return "Loopback address (127.x.x.x)";

        if (IsBroadcastAddress(ipAddress))
            return "Broadcast address (255.255.255.255)";

        if (IsMulticastAddress(ipAddress))
            return "Multicast address (224-239.x.x.x)";

        if (IsLinkLocalAddress(ipAddress))
            return "Link-local address (169.254.x.x)";

        if (IsReservedAddress(ipAddress))
            return "Reserved address range";

        return "Unknown";
    }
}
