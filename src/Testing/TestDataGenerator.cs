using TLScope.Models;

namespace TLScope.Testing;

/// <summary>
/// Generates test data for UI testing
/// Provides predefined scenarios and random data generation
/// </summary>
public static class TestDataGenerator
{
    /// <summary>
    /// Generate a simple network scenario with 5 devices and basic connections
    /// </summary>
    public static (List<Device> devices, List<Connection> connections) GenerateSimpleNetwork()
    {
        var devices = new List<Device>
        {
            CreateDevice("00:1A:2B:3C:4D:01", "192.168.1.1", "router-1", "TP-Link", new[] { 80, 443, 22 }),
            CreateDevice("00:1A:2B:3C:4D:02", "192.168.1.100", "desktop-1", "Dell", new[] { 22, 3389 }),
            CreateDevice("00:1A:2B:3C:4D:03", "192.168.1.101", "laptop-1", "Apple", new[] { 22, 5900 }),
            CreateDevice("00:1A:2B:3C:4D:04", "192.168.1.102", "phone-1", "Samsung", new[] { 8443 }),
            CreateDevice("00:1A:2B:3C:4D:05", "192.168.1.50", "server-1", "HP", new[] { 80, 443, 22, 3306 })
        };

        var connections = new List<Connection>
        {
            CreateConnection(devices[1], devices[0], "TCP", 55123, 443),  // desktop -> router (HTTPS)
            CreateConnection(devices[2], devices[0], "TCP", 55124, 443),  // laptop -> router (HTTPS)
            CreateConnection(devices[3], devices[0], "TCP", 55125, 80),   // phone -> router (HTTP)
            CreateConnection(devices[1], devices[4], "TCP", 55126, 3306), // desktop -> server (MySQL)
            CreateConnection(devices[2], devices[4], "TCP", 55127, 443),  // laptop -> server (HTTPS)
        };

        return (devices, connections);
    }

    /// <summary>
    /// Generate a complex network scenario with 15 devices and many connections
    /// </summary>
    public static (List<Device> devices, List<Connection> connections) GenerateComplexNetwork()
    {
        var devices = new List<Device>
        {
            CreateDevice("00:1A:2B:3C:4D:01", "192.168.1.1", "router-main", "Cisco", new[] { 80, 443, 22, 23 }),
            CreateDevice("00:1A:2B:3C:4D:02", "192.168.1.2", "switch-1", "Cisco", new[] { 22, 23 }),
            CreateDevice("00:1A:2B:3C:4D:03", "192.168.1.3", "firewall-1", "Palo Alto", new[] { 443, 22 }),

            CreateDevice("00:1A:2B:3C:4D:10", "192.168.1.10", "web-server", "Dell", new[] { 80, 443, 22 }),
            CreateDevice("00:1A:2B:3C:4D:11", "192.168.1.11", "db-server", "HP", new[] { 3306, 5432, 22 }),
            CreateDevice("00:1A:2B:3C:4D:12", "192.168.1.12", "file-server", "Synology", new[] { 445, 139, 22 }),

            CreateDevice("00:1A:2B:3C:4D:20", "192.168.1.100", "desktop-alice", "Dell", new[] { 22, 3389 }),
            CreateDevice("00:1A:2B:3C:4D:21", "192.168.1.101", "desktop-bob", "HP", new[] { 22, 3389 }),
            CreateDevice("00:1A:2B:3C:4D:22", "192.168.1.102", "laptop-charlie", "Apple", new[] { 22, 5900 }),

            CreateDevice("00:1A:2B:3C:4D:30", "192.168.1.150", "phone-alice", "Samsung", new[] { 8443 }),
            CreateDevice("00:1A:2B:3C:4D:31", "192.168.1.151", "tablet-bob", "Apple", new[] { 8443 }),

            CreateDevice("00:1A:2B:3C:4D:40", "192.168.1.200", "camera-front", "Hikvision", new[] { 554, 80 }),
            CreateDevice("00:1A:2B:3C:4D:41", "192.168.1.201", "camera-back", "Hikvision", new[] { 554, 80 }),
            CreateDevice("00:1A:2B:3C:4D:42", "192.168.1.202", "smart-tv", "Samsung", new[] { 8080 }),
            CreateDevice("00:1A:2B:3C:4D:43", "192.168.1.203", "printer", "HP", new[] { 9100, 631 })
        };

        var connections = new List<Connection>
        {
            CreateConnection(devices[1], devices[0], "TCP", 60001, 22),
            CreateConnection(devices[2], devices[0], "TCP", 60002, 22),

            CreateConnection(devices[6], devices[3], "TCP", 55100, 443),  // alice-desktop -> web-server
            CreateConnection(devices[7], devices[3], "TCP", 55101, 443),  // bob-desktop -> web-server
            CreateConnection(devices[8], devices[3], "TCP", 55102, 443),  // charlie-laptop -> web-server
            CreateConnection(devices[6], devices[4], "TCP", 55103, 3306), // alice-desktop -> db-server
            CreateConnection(devices[7], devices[5], "TCP", 55104, 445),  // bob-desktop -> file-server
            CreateConnection(devices[8], devices[5], "TCP", 55105, 445),  // charlie-laptop -> file-server

            CreateConnection(devices[9], devices[3], "TCP", 55200, 443),   // alice-phone -> web-server
            CreateConnection(devices[10], devices[3], "TCP", 55201, 443),  // bob-tablet -> web-server

            CreateConnection(devices[11], devices[3], "TCP", 55300, 80),   // camera-front -> web-server
            CreateConnection(devices[12], devices[3], "TCP", 55301, 80),   // camera-back -> web-server
            CreateConnection(devices[13], devices[3], "TCP", 55302, 8080), // smart-tv -> web-server
            CreateConnection(devices[14], devices[5], "TCP", 55303, 9100), // printer -> file-server

            CreateConnection(devices[6], devices[7], "TCP", 55400, 8443, isTlsPeer: true),
            CreateConnection(devices[6], devices[8], "TCP", 55401, 8443, isTlsPeer: true),
            CreateConnection(devices[9], devices[10], "TCP", 55402, 8443, isTlsPeer: true),
        };

        return (devices, connections);
    }

    /// <summary>
    /// Generate a stress test scenario with many devices and connections
    /// </summary>
    public static (List<Device> devices, List<Connection> connections) GenerateStressTestNetwork()
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var devices = new List<Device>();
        var connections = new List<Connection>();

        for (int i = 0; i < 50; i++)
        {
            var macBytes = new byte[6];
            macBytes[0] = 0x00;
            macBytes[1] = 0x1A;
            macBytes[2] = (byte)(i / 256);
            macBytes[3] = (byte)(i % 256);
            macBytes[4] = (byte)random.Next(256);
            macBytes[5] = (byte)random.Next(256);

            var mac = string.Join(":", macBytes.Select(b => b.ToString("X2")));
            var ip = $"192.168.{i / 254}.{(i % 254) + 1}";
            var hostname = $"device-{i:D3}";
            var vendors = new[] { "Dell", "HP", "Apple", "Cisco", "Samsung", "TP-Link", "Netgear", null };

            var device = CreateDevice(
                mac,
                ip,
                hostname,
                vendors[random.Next(vendors.Length)],
                Enumerable.Range(0, random.Next(1, 5))
                    .Select(_ => new[] { 22, 80, 443, 3306, 3389, 5432, 8080, 8443 }[random.Next(8)])
                    .ToArray()
            );

            devices.Add(device);
        }

        for (int i = 0; i < 120; i++)
        {
            var src = devices[random.Next(devices.Count)];
            var dst = devices[random.Next(devices.Count)];

            if (src != dst)
            {
                var protocols = new[] { "TCP", "UDP" };
                var protocol = protocols[random.Next(protocols.Length)];
                var commonPorts = new[] { 22, 80, 443, 3306, 3389, 5432, 8080, 8443 };

                var connection = CreateConnection(
                    src,
                    dst,
                    protocol,
                    random.Next(1024, 65535),
                    commonPorts[random.Next(commonPorts.Length)]
                );

                connections.Add(connection);
            }
        }

        return (devices, connections);
    }

    /// <summary>
    /// Generate TLS peer network scenario
    /// </summary>
    public static List<TLSPeer> GenerateTlsPeers(int count = 5)
    {
        var usernames = new[] { "alice", "bob", "charlie", "diana", "eve", "frank", "grace", "hank" };
        var avatarTypes = new[] { "CAT", "DOG", "BIRD", "FISH", "ROBOT", "ALIEN", "WIZARD", "NINJA" };
        var colors = new[] { "#FF6B6B", "#4ECDC4", "#45B7D1", "#FFA07A", "#98D8C8", "#F7DC6F", "#BB8FCE", "#85C1E2" };

        var peers = new List<TLSPeer>();

        for (int i = 0; i < Math.Min(count, usernames.Length); i++)
        {
            peers.Add(new TLSPeer
            {
                Username = usernames[i],
                IPAddress = $"192.168.1.{100 + i}",
                Port = 8443,
                SSHPublicKey = $"ssh-rsa AAAA{Convert.ToBase64String(new byte[32])} {usernames[i]}@tlscope",
                AvatarType = avatarTypes[i % avatarTypes.Length],
                AvatarColor = colors[i % colors.Length],
                Version = "1.0.0",
                FirstSeen = DateTime.UtcNow.AddMinutes(-i * 5),
                IsConnected = i % 2 == 0 // Every other peer is connected
            });
        }

        return peers;
    }

    /// <summary>
    /// Helper to create a device with specified parameters
    /// </summary>
    private static Device CreateDevice(string mac, string ip, string? hostname, string? vendor, int[] ports)
    {
        var device = new Device
        {
            MACAddress = mac,
            IPAddress = ip,
            Hostname = hostname,
            Vendor = vendor,
            FirstSeen = DateTime.UtcNow.AddMinutes(-30),
            LastSeen = DateTime.UtcNow,
            PacketCount = new Random().Next(100, 10000),
            BytesTransferred = new Random().Next(1024, 1024 * 1024)
        };

        foreach (var port in ports.Distinct())
        {
            device.OpenPorts.Add(port);
        }

        return device;
    }

    /// <summary>
    /// Helper to create a connection between devices
    /// </summary>
    private static Connection CreateConnection(Device src, Device dst, string protocol,
        int srcPort, int dstPort, bool isTlsPeer = false)
    {
        return new Connection
        {
            SourceDevice = src,
            DestinationDevice = dst,
            Protocol = protocol,
            SourcePort = srcPort,
            DestinationPort = dstPort,
            FirstSeen = DateTime.UtcNow.AddMinutes(-20),
            LastSeen = DateTime.UtcNow,
            PacketCount = new Random().Next(10, 1000),
            BytesTransferred = new Random().Next(1024, 1024 * 1024),
            IsTLSPeerConnection = isTlsPeer
        };
    }
}
