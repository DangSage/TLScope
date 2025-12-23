using SharpPcap;
using SharpPcap.LibPcap;
using PacketDotNet;
using TLScope.Models;
using TLScope.Utilities;
using Serilog;
using System.Collections.Concurrent;
using System.Net;

namespace TLScope.Services;

/// <summary>
/// Real-time packet capture service using SharpPcap
/// Passively discovers devices and monitors network traffic
/// </summary>
public class PacketCaptureService : IPacketCaptureService
{
    private ILiveDevice? _captureDevice;
    private bool _isCapturing;
    private bool _captureReady; // Only fire device discovery events after capture is confirmed ready
    private string? _currentInterfaceName;
    private readonly FilterConfiguration _filterConfig;
    private readonly IGraphService _graphService;
    private readonly INetworkScanService? _networkScanService;
    private readonly IGatewayDetectionService? _gatewayDetectionService;
    private long _packetCounter = 0;
    private const int PACKET_LOG_INTERVAL = 50; // Log every 50th packet

    public event EventHandler<Device>? DeviceDiscovered;
    public event EventHandler<Connection>? ConnectionDetected;
    public event EventHandler<string>? LogMessage;

    /// <summary>
    /// Constructor with filter configuration and graph service dependencies
    /// </summary>
    public PacketCaptureService(FilterConfiguration filterConfig, IGraphService graphService, INetworkScanService? networkScanService = null, IGatewayDetectionService? gatewayDetectionService = null)
    {
        _filterConfig = filterConfig;
        _graphService = graphService;
        _networkScanService = networkScanService;
        _gatewayDetectionService = gatewayDetectionService;

        // Subscribe to network scan events if service is provided
        if (_networkScanService != null)
        {
            _networkScanService.DeviceResponded += OnNetworkScanDeviceResponded;
            _networkScanService.ScanCompleted += OnNetworkScanCompleted;
        }
    }

    /// <summary>
    /// Get all available network interfaces
    /// </summary>
    public List<string> GetAvailableInterfaces()
    {
        try
        {
            var devices = LibPcapLiveDeviceList.Instance;
            return devices.Select(d => $"{d.Name} - {d.Description}").ToList();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get network interfaces - may need elevated privileges");
            return new List<string> { "No interfaces available (run with sudo)" };
        }
    }

    /// <summary>
    /// Get the currently active interface name
    /// </summary>
    public string? GetCurrentInterface()
    {
        return _currentInterfaceName;
    }

    /// <summary>
    /// Check if currently capturing
    /// </summary>
    public bool IsCapturing()
    {
        return _isCapturing;
    }

    /// <summary>
    /// Start capturing packets on specified interface
    /// </summary>
    /// <param name="interfaceName">Interface name (e.g., "eth0", "wlan0")</param>
    /// <param name="promiscuousMode">Enable promiscuous mode to capture all traffic</param>
    public void StartCapture(string? interfaceName = null, bool promiscuousMode = true)
    {
        if (_isCapturing)
        {
            Log.Warning("Packet capture already running");
            return;
        }

        var devices = LibPcapLiveDeviceList.Instance;

        if (devices.Count == 0)
        {
            throw new InvalidOperationException("No network interfaces found. Run with sudo/admin privileges.");
        }

        // Select interface
        if (string.IsNullOrEmpty(interfaceName))
        {
            // Use first non-loopback interface
            _captureDevice = devices.FirstOrDefault(d => !d.Name.Contains("lo"))
                ?? devices[0];
        }
        else
        {
            _captureDevice = devices.FirstOrDefault(d => d.Name.Contains(interfaceName));
            if (_captureDevice == null)
            {
                throw new ArgumentException($"Interface '{interfaceName}' not found");
            }
        }

        _currentInterfaceName = $"{_captureDevice.Name} - {_captureDevice.Description}";
        Log.Information($"[CAPTURE-START] Starting capture on interface: {_captureDevice.Name} ({_captureDevice.Description}), Promiscuous mode: {promiscuousMode}");
        LogMessage?.Invoke(this, $"Starting capture on {_captureDevice.Name}");

        // Configure device
        _captureDevice.OnPacketArrival += OnPacketArrival;
        _captureDevice.Open(new DeviceConfiguration
        {
            Mode = promiscuousMode ? DeviceModes.Promiscuous : DeviceModes.None,
            ReadTimeout = 1000
        });

        // Optional: Set BPF filter (e.g., "tcp or udp" to ignore other protocols)
        // _captureDevice.Filter = "tcp or udp or arp";

        _captureDevice.StartCapture();
        _isCapturing = true;

        // Mark capture as ready - device discovery can now begin
        _captureReady = true;

        Log.Information($"[CAPTURE-READY] Packet capture started successfully on {_captureDevice.Name}");
    }

    /// <summary>
    /// Stop packet capture
    /// </summary>
    public void StopCapture()
    {
        if (!_isCapturing || _captureDevice == null)
            return;

        var deviceName = _captureDevice.Name;
        var deviceCount = _graphService.GetAllDevices().Count;
        var connectionCount = _graphService.GetAllConnections().Count;

        _captureReady = false;
        _captureDevice.StopCapture();
        _captureDevice.Close();
        _isCapturing = false;
        _currentInterfaceName = null;

        Log.Information($"[CAPTURE-STOP] Packet capture stopped on {deviceName}. Discovered: {deviceCount} devices, {connectionCount} connections");
        LogMessage?.Invoke(this, "Packet capture stopped");
    }

    /// <summary>
    /// Packet arrival callback
    /// </summary>
    private void OnPacketArrival(object sender, PacketCapture e)
    {
        try
        {
            var rawPacket = e.GetPacket();
            var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);

            // Extract Ethernet layer
            var ethernetPacket = packet.Extract<EthernetPacket>();
            if (ethernetPacket == null)
            {
                Log.Debug("Received non-Ethernet packet, skipping");
                return;
            }

            // Log packet capture
            Log.Debug($"[PACKET] Captured packet: {ethernetPacket.SourceHardwareAddress} → {ethernetPacket.DestinationHardwareAddress}, Type: {ethernetPacket.Type}, Length: {rawPacket.Data.Length} bytes");

            // Process different packet types
            var ipPacket = packet.Extract<IPPacket>();
            if (ipPacket != null)
            {
                ProcessIPPacket(ethernetPacket, ipPacket, rawPacket.Data.Length);
            }

            var arpPacket = packet.Extract<ArpPacket>();
            if (arpPacket != null)
            {
                ProcessARPPacket(arpPacket);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing packet");
        }
    }

    /// <summary>
    /// Process IP packet (TCP/UDP)
    /// </summary>
    private void ProcessIPPacket(EthernetPacket ethernetPacket, IPPacket ipPacket, int packetLength)
    {
        // Extract transport layer packets
        var tcpPacket = ipPacket.Extract<TcpPacket>();
        var udpPacket = ipPacket.Extract<UdpPacket>();

        // Extract TTL for topology analysis
        var ttl = ipPacket.TimeToLive;

        // Increment packet counter
        _packetCounter++;

        // === APPLY ALL FILTERS FIRST (before logging or device discovery) ===

        // Filter non-local traffic if enabled
        if (_filterConfig.FilterNonLocalTraffic)
        {
            var srcIsLocal = IPAddressValidator.IsLocalAddress(ipPacket.SourceAddress.ToString());
            var dstIsLocal = IPAddressValidator.IsLocalAddress(ipPacket.DestinationAddress.ToString());

            if (!srcIsLocal || !dstIsLocal)
            {
                Log.Debug($"[FILTER-NONLOCAL] Filtered non-local traffic: {ipPacket.SourceAddress} → {ipPacket.DestinationAddress}");
                _filterConfig.NonLocalTrafficFiltered++;
                return;
            }
        }

        // Filter HTTP/HTTPS traffic if enabled
        if (_filterConfig.FilterHttpTraffic && (tcpPacket != null || udpPacket != null))
        {
            var port = tcpPacket?.DestinationPort ?? udpPacket?.DestinationPort ?? 0;
            var srcPortCheck = tcpPacket?.SourcePort ?? udpPacket?.SourcePort ?? 0;

            if (IsHttpPort(port) || IsHttpPort(srcPortCheck))
            {
                Log.Debug($"[FILTER-HTTP] Filtered HTTP/HTTPS traffic: {ipPacket.SourceAddress}:{srcPortCheck} → {ipPacket.DestinationAddress}:{port}");
                _filterConfig.HttpTrafficFiltered++;
                return;
            }
        }

        // === FILTERS PASSED - Now proceed with logging and processing ===

        // Determine protocol info for logging
        bool shouldLog = _packetCounter % PACKET_LOG_INTERVAL == 0;
        string? protocolName = null;
        int? srcPort = null;
        int? dstPort = null;

        if (tcpPacket != null)
        {
            Log.Debug($"[IP-TCP] {ipPacket.SourceAddress}:{tcpPacket.SourcePort} → {ipPacket.DestinationAddress}:{tcpPacket.DestinationPort}, Flags: {tcpPacket.Flags}, Seq: {tcpPacket.SequenceNumber}, PayloadSize: {ipPacket.PayloadLength} bytes");

            protocolName = GetProtocolName("TCP", tcpPacket.SourcePort, tcpPacket.DestinationPort);
            srcPort = tcpPacket.SourcePort;
            dstPort = tcpPacket.DestinationPort;

            // Always log important protocols
            if (protocolName != "TCP") shouldLog = true;
        }
        else if (udpPacket != null)
        {
            Log.Debug($"[IP-UDP] {ipPacket.SourceAddress}:{udpPacket.SourcePort} → {ipPacket.DestinationAddress}:{udpPacket.DestinationPort}, PayloadSize: {ipPacket.PayloadLength} bytes");

            protocolName = GetProtocolName("UDP", udpPacket.SourcePort, udpPacket.DestinationPort);
            srcPort = udpPacket.SourcePort;
            dstPort = udpPacket.DestinationPort;

            // Always log important protocols
            if (protocolName != "UDP") shouldLog = true;
        }
        else
        {
            Log.Debug($"[IP-OTHER] {ipPacket.SourceAddress} → {ipPacket.DestinationAddress}, Protocol: {ipPacket.Protocol}, PayloadSize: {ipPacket.PayloadLength} bytes");
            protocolName = ipPacket.Protocol.ToString();
        }

        // Emit log message for sampled or interesting packets (after filters!)
        if (shouldLog && _captureReady)
        {
            string portInfo = srcPort.HasValue ? $":{srcPort} → :{dstPort}" : "";
            LogMessage?.Invoke(this, $"📦 {protocolName} packet: {ipPacket.SourceAddress}{portInfo} → {ipPacket.DestinationAddress} ({FormatBytes(packetLength)})");
        }

        // Discover source device
        var srcDevice = DiscoverDevice(
            ethernetPacket.SourceHardwareAddress.ToString(),
            ipPacket.SourceAddress.ToString(),
            packetLength
        );

        // Discover destination device
        var dstDevice = DiscoverDevice(
            ethernetPacket.DestinationHardwareAddress.ToString(),
            ipPacket.DestinationAddress.ToString(),
            packetLength
        );

        // If either device was filtered at MAC layer, try to create virtual device based on IP
        // This handles cases where remote hosts are accessed through a gateway with filtered MAC
        if (srcDevice == null && !string.IsNullOrEmpty(ipPacket.SourceAddress.ToString()))
        {
            srcDevice = DiscoverVirtualDevice(ipPacket.SourceAddress.ToString(), packetLength);
        }

        if (dstDevice == null && !string.IsNullOrEmpty(ipPacket.DestinationAddress.ToString()))
        {
            dstDevice = DiscoverVirtualDevice(ipPacket.DestinationAddress.ToString(), packetLength);
        }

        // If both devices are still null after virtual device creation, skip connection tracking
        if (srcDevice == null || dstDevice == null)
        {
            Log.Debug($"[FILTER] Packet dropped - unable to discover devices: {ipPacket.SourceAddress} → {ipPacket.DestinationAddress}");
            return;
        }

        // Note: Filter checks have been moved to the beginning of this method (lines 211-237)
        // to prevent filtered traffic from appearing in logs

        // Track connection
        if (tcpPacket != null)
        {
            TrackConnection(srcDevice, dstDevice, "TCP",
                tcpPacket.SourcePort, tcpPacket.DestinationPort,
                ipPacket.PayloadLength, ttl);
        }
        else if (udpPacket != null)
        {
            TrackConnection(srcDevice, dstDevice, "UDP",
                udpPacket.SourcePort, udpPacket.DestinationPort,
                ipPacket.PayloadLength, ttl);

            // Check if this is TLScope peer discovery (UDP broadcast on port 8442)
            if (udpPacket.DestinationPort == 8442)
            {
                // Handle TLScope peer discovery
                Log.Information($"[TLSCOPE-DISCOVERY] TLScope peer discovery packet from {srcDevice.IPAddress}");
            }

            // DHCP passive discovery - monitor DHCP traffic for device discovery
            // DHCP uses UDP ports 67 (server) and 68 (client)
            if (udpPacket.SourcePort == 68 || udpPacket.DestinationPort == 68 ||
                udpPacket.SourcePort == 67 || udpPacket.DestinationPort == 67)
            {
                ProcessDHCPPacket(udpPacket, ethernetPacket, ipPacket);
            }
        }
    }

    /// <summary>
    /// Process ARP packet for device discovery
    /// </summary>
    private void ProcessARPPacket(ArpPacket arpPacket)
    {
        // ARP packets reveal MAC-IP mappings
        var mac = arpPacket.SenderHardwareAddress.ToString();
        var ip = arpPacket.SenderProtocolAddress.ToString();

        Log.Debug($"[ARP] Operation: {arpPacket.Operation}, Sender: {ip} ({mac}), Target: {arpPacket.TargetProtocolAddress} ({arpPacket.TargetHardwareAddress})");

        DiscoverDevice(mac, ip);
    }

    /// <summary>
    /// Process DHCP packet for passive device discovery
    /// DHCP packets reveal device MAC addresses and requested/assigned IPs
    /// </summary>
    private void ProcessDHCPPacket(UdpPacket udpPacket, EthernetPacket ethernetPacket, IPPacket ipPacket)
    {
        try
        {
            // DHCP client MAC is always in the Ethernet source address
            var clientMac = ethernetPacket.SourceHardwareAddress.ToString();
            var clientIP = ipPacket.SourceAddress.ToString();

            // Determine DHCP message type based on ports
            bool isClientToServer = udpPacket.SourcePort == 68 && udpPacket.DestinationPort == 67;
            bool isServerToClient = udpPacket.SourcePort == 67 && udpPacket.DestinationPort == 68;

            if (isClientToServer)
            {
                // DHCP Discover, Request, Inform, etc. from client
                // Client MAC is in Ethernet header, IP might be 0.0.0.0
                Log.Debug($"[DHCP-CLIENT] DHCP request from {clientMac} (IP: {clientIP})");

                // Discover device by MAC (IP might be 0.0.0.0 for DISCOVER messages)
                if (clientIP != "0.0.0.0")
                {
                    DiscoverDevice(clientMac, clientIP);
                }
                else
                {
                    // Try to parse DHCP payload for requested IP
                    var payload = udpPacket.PayloadData;
                    if (payload != null && payload.Length > 240)
                    {
                        // DHCP packet structure:
                        // 0-3: Message type, HW type, HW addr len, hops
                        // 4-7: Transaction ID
                        // 8-11: Seconds, Flags
                        // 12-15: Client IP (ciaddr)
                        // 16-19: Your IP (yiaddr) - offered by server
                        // 20-23: Server IP (siaddr)
                        // 24-27: Gateway IP (giaddr)
                        // 28-43: Client HW address (chaddr) - 16 bytes

                        // Extract client IP from ciaddr field (bytes 12-15)
                        var ciaddrBytes = new byte[] { payload[12], payload[13], payload[14], payload[15] };
                        var ciaddr = new System.Net.IPAddress(ciaddrBytes).ToString();

                        if (ciaddr != "0.0.0.0")
                        {
                            Log.Debug($"[DHCP-CLIENT] Extracted client IP from DHCP: {ciaddr}");
                            DiscoverDevice(clientMac, ciaddr);
                        }
                        else
                        {
                            // No IP yet, just register the MAC
                            // We'll discover the IP when we see the DHCP ACK or later traffic
                            Log.Debug($"[DHCP-CLIENT] Device {clientMac} requesting IP (no IP assigned yet)");
                        }
                    }
                }
            }
            else if (isServerToClient)
            {
                // DHCP Offer, Ack, Nak from server
                // Destination MAC is the client MAC
                var serverIP = ipPacket.SourceAddress.ToString();
                Log.Debug($"[DHCP-SERVER] DHCP response from server {serverIP}");

                // Parse DHCP payload to extract offered/assigned IP
                var payload = udpPacket.PayloadData;
                if (payload != null && payload.Length > 240)
                {
                    // Extract "your IP" (yiaddr) field (bytes 16-19) - this is the offered/assigned IP
                    var yiaddrBytes = new byte[] { payload[16], payload[17], payload[18], payload[19] };
                    var yiaddr = new System.Net.IPAddress(yiaddrBytes).ToString();

                    // Extract client MAC from chaddr field (bytes 28-43)
                    var chaddr = string.Join(":", payload.Skip(28).Take(6).Select(b => b.ToString("X2")));

                    if (yiaddr != "0.0.0.0" && !string.IsNullOrEmpty(chaddr))
                    {
                        Log.Information($"[DHCP-DISCOVERY] Device discovered via DHCP: {chaddr} assigned IP {yiaddr}");
                        DiscoverDevice(chaddr, yiaddr);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Don't crash on DHCP parsing errors - just log and continue
            Log.Debug($"[DHCP] Failed to parse DHCP packet: {ex.Message}");
        }
    }

    /// <summary>
    /// Discover or update device
    /// </summary>
    private Device? DiscoverDevice(string macAddress, string ipAddress, int bytes = 0)
    {
        var key = macAddress.ToLower();

        Log.Debug($"[DEVICE-DISCOVERY] Attempting to discover device: MAC={macAddress}, IP={ipAddress}");

        // Filter utility MAC addresses
        if (IPAddressValidator.IsUtilityMAC(macAddress))
        {
            Log.Debug($"[FILTER-MAC] Filtered utility MAC address: {macAddress}");
            return null;
        }

        // Filter utility IP addresses
        if (IPAddressValidator.IsUtilityAddress(ipAddress,
            _filterConfig.FilterLoopback,
            _filterConfig.FilterBroadcast,
            _filterConfig.FilterMulticast,
            _filterConfig.FilterLinkLocal,
            _filterConfig.FilterReserved))
        {
            var reason = IPAddressValidator.GetFilterReason(ipAddress);
            Log.Debug($"[FILTER-IP] Filtered utility IP address: {ipAddress} ({reason})");
            _filterConfig.TotalFiltered++;
            return null;
        }

        // Filter non-local IP addresses if enabled
        if (_filterConfig.FilterNonLocalTraffic && !IPAddressValidator.IsLocalAddress(ipAddress))
        {
            Log.Debug($"[FILTER-NONLOCAL] Filtered non-local IP address: {ipAddress}");
            _filterConfig.NonLocalTrafficFiltered++;
            return null;
        }

        // Check for duplicate IP addresses (different MAC, same IP)
        if (_filterConfig.BlockDuplicateIPs && !string.IsNullOrEmpty(ipAddress))
        {
            var existingDevice = _graphService.GetDeviceByIP(ipAddress);
            if (existingDevice != null && existingDevice.MACAddress.ToLower() != key)
            {
                Log.Warning($"[FILTER-DUPLICATE] Blocked duplicate IP: {ipAddress} (existing MAC: {existingDevice.MACAddress}, new MAC: {macAddress})");
                _filterConfig.DuplicatesBlocked++;
                return null;
            }
        }

        // Check if device already exists in GraphService
        var device = _graphService.GetDevice(key);

        if (device != null)
        {
            // Update existing device
            device.IPAddress = ipAddress;
            device.LastSeen = DateTime.UtcNow;
            device.PacketCount++;
            device.BytesTransferred += bytes;

            // Update device in graph service
            _graphService.UpdateDevice(device);

            Log.Debug($"[DEVICE-UPDATE] Updated existing device: MAC={macAddress}, IP={ipAddress}, PacketCount={device.PacketCount}, Bytes={device.BytesTransferred}");
            return device;
        }

        // New device discovered - create and add to GraphService
        device = new Device
        {
            MACAddress = macAddress,
            IPAddress = ipAddress,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            PacketCount = 1,
            BytesTransferred = bytes
        };

        // Try to lookup vendor from MAC OUI
        device.Vendor = LookupVendor(macAddress);

        // Perform DNS hostname resolution asynchronously (fire-and-forget)
        _ = ResolveHostnameAsync(device);

        // Only fire device discovery event if capture is ready
        if (_captureReady)
        {
            Log.Information($"[DEVICE-NEW] New device discovered: MAC={device.MACAddress}, IP={device.IPAddress}, Vendor={device.Vendor ?? "Unknown"}");
            DeviceDiscovered?.Invoke(this, device);
        }
        else
        {
            Log.Debug($"[DEVICE-NEW] Device discovered (capture not ready yet): MAC={device.MACAddress}, IP={device.IPAddress}");
        }

        return device;
    }

    /// <summary>
    /// Discover or update virtual device (IP-only, no MAC)
    /// Used for remote devices accessed through a gateway with filtered MAC
    /// </summary>
    private Device? DiscoverVirtualDevice(string ipAddress, int bytes = 0)
    {
        // Use IP address as key for virtual devices (prefixed with "virtual-")
        var key = $"virtual-{ipAddress}".ToLower();

        Log.Debug($"[VIRTUAL-DEVICE] Attempting to discover virtual device: IP={ipAddress}");

        // Filter utility IP addresses
        if (IPAddressValidator.IsUtilityAddress(ipAddress,
            _filterConfig.FilterLoopback,
            _filterConfig.FilterBroadcast,
            _filterConfig.FilterMulticast,
            _filterConfig.FilterLinkLocal,
            _filterConfig.FilterReserved))
        {
            var reason = IPAddressValidator.GetFilterReason(ipAddress);
            Log.Debug($"[FILTER-IP] Filtered utility IP address: {ipAddress} ({reason})");
            _filterConfig.TotalFiltered++;
            return null;
        }

        // Filter non-local IP addresses if enabled
        if (_filterConfig.FilterNonLocalTraffic && !IPAddressValidator.IsLocalAddress(ipAddress))
        {
            Log.Debug($"[FILTER-NONLOCAL] Filtered non-local virtual device: {ipAddress}");
            _filterConfig.NonLocalTrafficFiltered++;
            return null;
        }

        // Check if device already exists in GraphService
        var device = _graphService.GetDevice(key);

        if (device != null)
        {
            // Update existing virtual device
            device.LastSeen = DateTime.UtcNow;
            device.PacketCount++;
            device.BytesTransferred += bytes;

            // Update in graph service
            _graphService.UpdateDevice(device);

            Log.Debug($"[VIRTUAL-DEVICE-UPDATE] Updated virtual device: IP={ipAddress}, PacketCount={device.PacketCount}");
            return device;
        }

        // New virtual device discovered - create and fire event
        device = new Device
        {
            MACAddress = key, // Use virtual key as MAC for unique identification
            IPAddress = ipAddress,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            PacketCount = 1,
            BytesTransferred = bytes,
            Vendor = "Remote Host" // Mark as remote/virtual device
        };

        // Perform DNS hostname resolution asynchronously
        _ = ResolveHostnameAsync(device);

        // Fire device discovery event
        if (_captureReady)
        {
            Log.Information($"[VIRTUAL-DEVICE-NEW] New virtual device discovered: IP={device.IPAddress}");
            DeviceDiscovered?.Invoke(this, device);
        }

        return device;
    }

    /// <summary>
    /// Track connection between devices (delegates to GraphService)
    /// </summary>
    private void TrackConnection(Device src, Device dst, string protocol,
        int srcPort, int dstPort, int bytes, int ttl)
    {
        // Create connection object
        var connection = new Connection
        {
            SourceDevice = src,
            DestinationDevice = dst,
            Protocol = protocol,
            SourcePort = srcPort,
            DestinationPort = dstPort,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            PacketCount = 1,
            BytesTransferred = bytes,
            MinTTL = ttl,
            MaxTTL = ttl,
            AverageTTL = ttl,
            PacketCountForTTL = 1
        };

        // Check if this is a TLS peer connection (port 8443)
        if (dstPort == 8443 || srcPort == 8443)
        {
            connection.IsTLSPeerConnection = true;
            connection.Type = ConnectionType.TLSPeer;
            Log.Information($"[TLSCOPE-PEER] TLS peer connection detected: {src.IPAddress}:{srcPort} → {dst.IPAddress}:{dstPort}");
        }
        else
        {
            // Classify connection type based on TTL and destination
            connection.Type = ClassifyConnectionType(dst, ttl);
        }

        Log.Debug($"[CONNECTION-TRACK] {protocol} connection: {src.IPAddress}:{srcPort} → {dst.IPAddress}:{dstPort}, Type={connection.Type}, TTL={ttl}, Bytes={bytes}");

        // Fire connection event if capture is ready (GraphService will be updated via MainApplication event handler)
        if (_captureReady)
        {
            ConnectionDetected?.Invoke(this, connection);
        }
        else
        {
            Log.Debug($"[CONNECTION-NEW] Connection detected (capture not ready yet): {src.IPAddress}:{srcPort} → {dst.IPAddress}:{dstPort}");
        }

        // Track open ports
        if (!dst.OpenPorts.Contains(dstPort))
        {
            dst.OpenPorts.Add(dstPort);
            Log.Debug($"[PORT-DISCOVERY] New open port detected on {dst.IPAddress}: {dstPort} ({protocol})");
        }
    }

    /// <summary>
    /// Classify connection type based on destination device and TTL
    /// TTL analysis:
    /// - >= 62: DirectL2 (initial TTL 64 - 1-2 hops, same L2 segment)
    /// - 50-61: RoutedL3 (routed through local gateway)
    /// - < 50: Internet (multiple hops, likely remote)
    /// </summary>
    private ConnectionType ClassifyConnectionType(Device destinationDevice, int ttl)
    {
        // If destination is a virtual device (remote host), it's Internet
        if (destinationDevice.IsVirtualDevice)
        {
            return ConnectionType.Internet;
        }

        // If destination is local, analyze TTL
        if (destinationDevice.IsLocal)
        {
            if (ttl >= 62)
            {
                // High TTL suggests direct L2 connection
                // Common initial TTLs: 64 (Linux), 128 (Windows), 255 (network devices)
                // After 1-2 hops: 62-63, 126-127, 253-254
                return ConnectionType.DirectL2;
            }
            else if (ttl >= 50)
            {
                // Medium TTL suggests routing through local gateway
                return ConnectionType.RoutedL3;
            }
            else
            {
                // Low TTL even for local device - unusual, treat as Internet
                return ConnectionType.Internet;
            }
        }

        // Destination is not local (public IP), it's Internet
        return ConnectionType.Internet;
    }

    /// <summary>
    /// Lookup vendor from MAC address OUI (first 3 bytes)
    /// </summary>
    private string? LookupVendor(string macAddress)
    {
        // Simple vendor mapping - can be expanded with full OUI database
        var oui = macAddress.ToUpper().Substring(0, 8);

        return oui switch
        {
            "00:50:56" => "VMware",
            "08:00:27" => "VirtualBox",
            "52:54:00" => "QEMU",
            "00:1A:A0" => "Dell",
            "00:0C:29" => "VMware",
            _ => null
        };
    }

    /// <summary>
    /// Get protocol name from transport protocol and ports
    /// </summary>
    private string GetProtocolName(string transport, int srcPort, int dstPort)
    {
        // Check well-known ports
        var port = Math.Min(srcPort, dstPort); // Use the lower port (typically the service port)

        return port switch
        {
            80 => "HTTP",
            443 => "HTTPS",
            22 => "SSH",
            21 => "FTP",
            23 => "Telnet",
            25 => "SMTP",
            53 => "DNS",
            67 or 68 => "DHCP",
            110 => "POP3",
            143 => "IMAP",
            3306 => "MySQL",
            5432 => "PostgreSQL",
            6379 => "Redis",
            27017 => "MongoDB",
            3389 => "RDP",
            8080 => "HTTP-Alt",
            8443 => "HTTPS-Alt",
            _ => transport
        };
    }

    /// <summary>
    /// Format bytes into human-readable string
    /// </summary>
    /// <summary>
    /// Check if port is HTTP/HTTPS related
    /// </summary>
    private bool IsHttpPort(int port)
    {
        return port == 80 || port == 443 || port == 8080 || port == 8443;
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Get all discovered devices (delegates to GraphService)
    /// </summary>
    public List<Device> GetDiscoveredDevices()
    {
        return _graphService.GetAllDevices();
    }

    /// <summary>
    /// Get all active connections (delegates to GraphService)
    /// </summary>
    public List<Connection> GetActiveConnections()
    {
        return _graphService.GetAllConnections()
            .Where(c => c.IsActive)
            .ToList();
    }

    /// <summary>
    /// Clear old inactive connections (delegates to GraphService for device cleanup)
    /// Connections are now managed entirely by GraphService
    /// </summary>
    public void CleanupOldConnections()
    {
        // Device cleanup handles removing old connections since connections
        // are stored as edges in the graph
        _graphService.CleanupInactiveDevices();
    }

    /// <summary>
    /// Resolve hostname via reverse DNS lookup (PTR query)
    /// </summary>
    private async Task ResolveHostnameAsync(Device device)
    {
        if (string.IsNullOrEmpty(device.IPAddress))
            return;

        try
        {
            // Skip hostname resolution for utility addresses
            if (IPAddressValidator.IsUtilityAddress(device.IPAddress, true, true, true, true, true))
            {
                Log.Debug($"[DNS] Skipping hostname resolution for utility address: {device.IPAddress}");
                return;
            }

            // Parse IP address
            if (!IPAddress.TryParse(device.IPAddress, out var ipAddress))
            {
                Log.Debug($"[DNS] Invalid IP address format: {device.IPAddress}");
                return;
            }

            // Perform reverse DNS lookup with timeout
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var task = Dns.GetHostEntryAsync(ipAddress);
            var hostEntry = await task.WaitAsync(cancellationTokenSource.Token);

            if (!string.IsNullOrEmpty(hostEntry.HostName) && hostEntry.HostName != device.IPAddress)
            {
                device.Hostname = hostEntry.HostName;
                Log.Information($"[DNS] Resolved hostname for {device.IPAddress}: {device.Hostname}");

                // Update device in graph service to save hostname
                _graphService.UpdateDevice(device);
            }
            else
            {
                Log.Debug($"[DNS] No hostname found for {device.IPAddress}");
            }
        }
        catch (OperationCanceledException)
        {
            Log.Debug($"[DNS] Hostname resolution timeout for {device.IPAddress}");
        }
        catch (Exception ex)
        {
            Log.Debug($"[DNS] Failed to resolve hostname for {device.IPAddress}: {ex.Message}");
        }
    }

    /// <summary>
    /// Trigger an active ICMP network scan on the local subnet
    /// Discovered devices are automatically fed into the device discovery pipeline
    /// </summary>
    /// <param name="subnet">Optional subnet to scan (e.g., "192.168.1"). If null, auto-detects from current interface</param>
    /// <returns>List of discovered IP addresses</returns>
    public async Task<List<string>> ScanNetworkAsync(string? subnet = null)
    {
        if (_networkScanService == null)
        {
            Log.Warning("[NETWORK-SCAN] Network scan service not available");
            return new List<string>();
        }

        Log.Information("[NETWORK-SCAN] Triggering active network scan");
        LogMessage?.Invoke(this, "Starting network scan...");

        List<string> results;
        if (subnet != null)
        {
            results = await _networkScanService.PingSweepAsync(subnet);
        }
        else
        {
            // Auto-detect subnet from current interface
            var detectedSubnet = _networkScanService.GetLocalSubnet(_captureDevice?.Name);
            if (detectedSubnet == null)
            {
                Log.Warning("[NETWORK-SCAN] Unable to auto-detect subnet");
                LogMessage?.Invoke(this, "Network scan failed: unable to detect subnet");
                return new List<string>();
            }
            results = await _networkScanService.PingSweepAsync(detectedSubnet);
        }

        LogMessage?.Invoke(this, $"Network scan completed: {results.Count} devices found");
        return results;
    }

    /// <summary>
    /// Event handler for network scan device responses
    /// Feeds discovered IPs into the passive device discovery pipeline
    /// </summary>
    private void OnNetworkScanDeviceResponded(object? sender, (string ipAddress, System.Net.NetworkInformation.PingReply reply) e)
    {
        if (!_captureReady)
            return;

        // Create a virtual device for the scanned IP
        // MAC address will be discovered through passive ARP capture later
        Log.Debug($"[NETWORK-SCAN] Processing discovered IP: {e.ipAddress}");

        // Check if we already have this device by IP
        var existingDevice = _graphService.GetDeviceByIP(e.ipAddress);
        if (existingDevice != null)
        {
            // Update last seen time
            existingDevice.LastSeen = DateTime.UtcNow;
            _graphService.UpdateDevice(existingDevice);
            Log.Debug($"[NETWORK-SCAN] Updated existing device: {e.ipAddress}");
        }
        else
        {
            // Create placeholder device - will be enriched when we see actual traffic
            var device = new Device
            {
                MACAddress = $"scan-pending-{e.ipAddress}",  // Temporary placeholder
                IPAddress = e.ipAddress,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                Vendor = "Scan Discovered (MAC pending)"
            };

            // Try to resolve hostname
            _ = Task.Run(() => ResolveHostnameAsync(device));

            // GraphService is updated via DeviceDiscovered event handler in MainApplication
            Log.Information($"[NETWORK-SCAN] Discovered new device via scan: {e.ipAddress}");
            DeviceDiscovered?.Invoke(this, device);
        }
    }

    /// <summary>
    /// Event handler for network scan completion
    /// </summary>
    private void OnNetworkScanCompleted(object? sender, NetworkScanResult e)
    {
        Log.Information($"[NETWORK-SCAN] Scan completed: {e.ResponsiveHosts.Count}/{e.TotalScanned} hosts in {e.Duration.TotalSeconds:F2}s");
    }

    public void Dispose()
    {
        // Unsubscribe from network scan events
        if (_networkScanService != null)
        {
            _networkScanService.DeviceResponded -= OnNetworkScanDeviceResponded;
            _networkScanService.ScanCompleted -= OnNetworkScanCompleted;
        }

        StopCapture();
        _captureDevice?.Dispose();
    }
}
