namespace TLScope.Models;

/// <summary>
/// Represents an open port on a network device
/// </summary>
public class DevicePort
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Associated device
    /// </summary>
    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    /// <summary>
    /// Port number (1-65535)
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Protocol (TCP, UDP, etc.)
    /// </summary>
    public string Protocol { get; set; } = "TCP";

    /// <summary>
    /// When this port was first observed as open
    /// </summary>
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this port was last observed as open
    /// </summary>
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    public override string ToString()
    {
        return $"{Port}/{Protocol}";
    }
}
