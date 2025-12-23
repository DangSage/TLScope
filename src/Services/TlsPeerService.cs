using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using TLScope.Models;
using TLScope.Utilities;
using Serilog;

namespace TLScope.Services;

/// <summary>
/// TLS peer-to-peer communication service for TLScope clients
///
/// PROTOCOL LAYERS:
/// 1. UDP Discovery Layer (port 8442)
///    - Broadcast announcements with peer information
///    - No encryption, used for peer discovery only
///
/// 2. TLS Transport Layer (port 8443)
///    - TCP connection establishment
///    - TLS handshake (handled automatically by SslStream)
///    - X.509 certificate authentication
///    - Encrypted communication channel
///
/// 3. Application Layer (over TLS)
///    - PEER_IDENTIFICATION: Peer authentication and identification
///    - CHALLENGE: Challenge-response for cryptographic verification
///    - GRAPH_SYNC: Network graph synchronization
///    - DEVICE_UPDATE: Device status updates
///    - PING/PONG: Connection keepalive
///
/// NOTE: The TLS handshake (ClientHello, ServerHello, certificate exchange,
/// key derivation) is handled automatically by .NET's SslStream.
/// </summary>
public class TlsPeerService : ITlsPeerService
{
    private const int DISCOVERY_PORT = 8442; // UDP broadcast port
    private const int TLS_PORT = 8443; // TLS connection port

    // Application protocol message types (sent over TLS)
    private const string MSG_TYPE_DISCOVERY = "DISCOVERY";
    private const string MSG_TYPE_PEER_IDENTIFICATION = "PEER_IDENTIFICATION";
    private const string MSG_TYPE_CHALLENGE = "CHALLENGE";
    private const string MSG_TYPE_GRAPH_SYNC = "GRAPH_SYNC";
    private const string MSG_TYPE_DEVICE_UPDATE = "DEVICE_UPDATE";
    private const string MSG_TYPE_PING = "PING";
    private const string MSG_TYPE_PONG = "PONG";

    // TLS configuration
    private const string TLS_SERVER_NAME = "tlscope";
    private const string CERT_FILE_PATH = "tlscope.pfx";
    private static readonly string CERT_PASSWORD = GetCertificatePassword();

    private readonly User _localUser;
    private readonly List<TLSPeer> _discoveredPeers = new();
    private readonly Dictionary<string, SslStream> _activePeerConnections = new();

    private UdpClient? _discoveryListener;
    private TcpListener? _tlsListener;
    private bool _isRunning;
    private Task? _discoveryTask;
    private Task? _listenerTask;
    private Timer? _announcementTimer;

    public event EventHandler<TLSPeer>? PeerDiscovered;
    public event EventHandler<TLSPeer>? PeerConnected;
    public event EventHandler<TLSPeer>? PeerDisconnected;
    public event EventHandler<(TLSPeer Peer, string Message)>? MessageReceived;

    public TlsPeerService(User localUser)
    {
        _localUser = localUser;
    }

    /// <summary>
    /// Get certificate password from environment variable or fallback to default
    /// </summary>
    private static string GetCertificatePassword()
    {
        var password = Environment.GetEnvironmentVariable("TLSCOPE_CERT_PASSWORD");

        if (string.IsNullOrEmpty(password))
        {
            Log.Warning("TLSCOPE_CERT_PASSWORD environment variable not set. Using default password. " +
                       "For production use, set the environment variable to secure your certificate.");
            return "password"; // Development fallback
        }

        return password;
    }

    /// <summary>
    /// Start peer discovery and TLS listener
    /// </summary>
    public void Start()
    {
        if (_isRunning)
            return;

        _isRunning = true;

        // Start UDP discovery listener
        _discoveryListener = new UdpClient(DISCOVERY_PORT);
        _discoveryListener.EnableBroadcast = true;
        _discoveryTask = Task.Run(ListenForDiscovery);

        // Start TLS listener
        _tlsListener = new TcpListener(IPAddress.Any, TLS_PORT);
        _tlsListener.Start();
        _listenerTask = Task.Run(ListenForConnections);

        // Announce ourselves
        AnnouncePeer();

        // Start periodic announcements every 30 seconds
        _announcementTimer = new Timer(
            callback: _ => AnnouncePeer(),
            state: null,
            dueTime: TimeSpan.FromSeconds(30),
            period: TimeSpan.FromSeconds(30)
        );

        Log.Information("TLS P2P service started");
    }

    /// <summary>
    /// Stop all peer services
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
            return;

        _isRunning = false;

        _announcementTimer?.Dispose();
        _announcementTimer = null;

        _discoveryListener?.Close();
        _tlsListener?.Stop();

        // Close all peer connections
        foreach (var stream in _activePeerConnections.Values)
        {
            stream.Close();
        }
        _activePeerConnections.Clear();

        Log.Information("TLS P2P service stopped");
    }

    /// <summary>
    /// Announce this peer on the network via UDP broadcast
    /// </summary>
    public void AnnouncePeer()
    {
        try
        {
            // Generate combined randomart + avatar art
            var avatarLines = GetLocalUserAvatar();
            var combinedArt = RandomartAvatarUtility.GenerateCombinedArt(_localUser.SSHPublicKey, avatarLines);
            var combinedArtString = RandomartAvatarUtility.CombinedArtToString(combinedArt);

            var announcement = new
            {
                Type = MSG_TYPE_DISCOVERY,
                Username = _localUser.Username,
                SSHPublicKey = _localUser.SSHPublicKey,
                AvatarType = _localUser.AvatarType,
                AvatarColor = _localUser.AvatarColor,
                CombinedRandomartAvatar = combinedArtString,
                Port = TLS_PORT,
                Version = "1.0.0"
            };

            var json = JsonSerializer.Serialize(announcement);
            var data = Encoding.UTF8.GetBytes(json);

            using var udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;
            udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT));

            Log.Debug("Peer announcement sent");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to announce peer");
        }
    }

    /// <summary>
    /// Proactively probe a specific device to check if it's a TLScope peer
    /// Sends a directed UDP discovery packet to the device's IP address
    /// </summary>
    public void ProbeDevice(IPAddress ipAddress)
    {
        try
        {
            // Generate combined randomart + avatar art
            var avatarLines = GetLocalUserAvatar();
            var combinedArt = RandomartAvatarUtility.GenerateCombinedArt(_localUser.SSHPublicKey, avatarLines);
            var combinedArtString = RandomartAvatarUtility.CombinedArtToString(combinedArt);

            var announcement = new
            {
                Type = MSG_TYPE_DISCOVERY,
                Username = _localUser.Username,
                SSHPublicKey = _localUser.SSHPublicKey,
                AvatarType = _localUser.AvatarType,
                AvatarColor = _localUser.AvatarColor,
                CombinedRandomartAvatar = combinedArtString,
                Port = TLS_PORT,
                Version = "1.0.0"
            };

            var json = JsonSerializer.Serialize(announcement);
            var data = Encoding.UTF8.GetBytes(json);

            using var udpClient = new UdpClient();
            udpClient.Send(data, data.Length, new IPEndPoint(ipAddress, DISCOVERY_PORT));

            Log.Debug($"Probing device at {ipAddress}");
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to probe device at {ipAddress}");
        }
    }

    /// <summary>
    /// Listen for peer discovery broadcasts
    /// </summary>
    private async Task ListenForDiscovery()
    {
        while (_isRunning && _discoveryListener != null)
        {
            try
            {
                var result = await _discoveryListener.ReceiveAsync();
                var json = Encoding.UTF8.GetString(result.Buffer);
                var message = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                if (message == null || !message.ContainsKey("Type"))
                    continue;

                var type = message["Type"].GetString();
                if (type != MSG_TYPE_DISCOVERY)
                    continue;

                // Parse peer info
                var username = message["Username"].GetString() ?? "Unknown";
                var sshKey = message["SSHPublicKey"].GetString() ?? "";
                var avatarType = message["AvatarType"].GetString() ?? "APPEARANCE_DEFAULT";
                var avatarColor = message["AvatarColor"].GetString() ?? "#FFFFFF";
                var combinedArt = message.ContainsKey("CombinedRandomartAvatar") ? message["CombinedRandomartAvatar"].GetString() : null;
                var port = message.ContainsKey("Port") ? message["Port"].GetInt32() : TLS_PORT;
                var version = message.ContainsKey("Version") ? message["Version"].GetString() : "1.0.0";

                // Ignore our own broadcasts
                if (username == _localUser.Username)
                    continue;

                var peer = new TLSPeer
                {
                    Username = username,
                    IPAddress = result.RemoteEndPoint.Address.ToString(),
                    Port = port,
                    SSHPublicKey = sshKey,
                    AvatarType = avatarType,
                    AvatarColor = avatarColor,
                    CombinedRandomartAvatar = combinedArt,
                    Version = version,
                    FirstSeen = DateTime.UtcNow
                };

                // Check if we already know this peer
                var existingPeer = _discoveredPeers.FirstOrDefault(p => p.Username == username);
                if (existingPeer == null)
                {
                    _discoveredPeers.Add(peer);
                    Log.Information($"Discovered new peer: {peer}");
                    PeerDiscovered?.Invoke(this, peer);
                }
                else
                {
                    existingPeer.LastConnected = DateTime.UtcNow;
                }
            }
            catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException)
            {
                // Socket closed, exit loop
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in discovery listener");
            }
        }
    }

    /// <summary>
    /// Listen for incoming TLS connections from peers
    /// </summary>
    private async Task ListenForConnections()
    {
        while (_isRunning && _tlsListener != null)
        {
            try
            {
                var client = await _tlsListener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandlePeerConnection(client));
            }
            catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException)
            {
                // Listener stopped, exit loop
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error accepting connection");
            }
        }
    }

    /// <summary>
    /// Handle incoming peer TLS connection
    /// </summary>
    private async Task HandlePeerConnection(TcpClient client)
    {
        SslStream? sslStream = null;
        try
        {
            var remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
            Log.Information($"Incoming peer connection from {remoteIp}");

            // Create SSL stream
            sslStream = new SslStream(client.GetStream(), false);

            // TLS handshake occurs here (handled by SslStream)
            var cert = LoadServerCertificate();
            await sslStream.AuthenticateAsServerAsync(cert);

            // Challenge-response authentication
            var challenge = CryptoUtility.GenerateChallenge();
            await SendMessageAsync(sslStream, new { Type = MSG_TYPE_CHALLENGE, Challenge = challenge });

            // Read peer identification message (should include signature)
            var peerIdentification = await ReadMessageAsync(sslStream);
            if (peerIdentification == null || !peerIdentification.ContainsKey("Type"))
            {
                Log.Warning("Invalid peer identification from peer");
                return;
            }

            var username = peerIdentification.ContainsKey("Username") ? peerIdentification["Username"].GetString() ?? "Unknown" : "Unknown";
            var sshKey = peerIdentification.ContainsKey("SSHPublicKey") ? peerIdentification["SSHPublicKey"].GetString() ?? "" : "";
            var signature = peerIdentification.ContainsKey("Signature") ? peerIdentification["Signature"].GetString() ?? "" : "";

            var peer = _discoveredPeers.FirstOrDefault(p => p.Username == username);

            if (peer == null)
            {
                Log.Warning($"Unknown peer attempted connection: {username}");
                return;
            }

            // Verify cryptographic signature
            var isVerified = !string.IsNullOrEmpty(signature) && CryptoUtility.VerifySignature(challenge, signature, sshKey);

            if (isVerified)
            {
                peer.IsVerified = true;
                peer.LastVerified = DateTime.UtcNow;
                Log.Information($"Peer signature verified: {peer}");
            }
            else
            {
                peer.IsVerified = false;
                Log.Warning($"Failed to verify signature for peer: {username}");
            }

            peer.IsConnected = true;
            _activePeerConnections[username] = sslStream;

            Log.Information($"Peer connected: {peer}");
            PeerConnected?.Invoke(this, peer);

            // Keep connection alive and handle messages
            await HandlePeerMessages(peer, sslStream);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling peer connection");
        }
        finally
        {
            sslStream?.Close();
            client.Close();
        }
    }

    /// <summary>
    /// Connect to a discovered peer
    /// </summary>
    public async Task<bool> ConnectToPeer(TLSPeer peer)
    {
        try
        {
            Log.Information($"Connecting to peer: {peer}");

            var client = new TcpClient();
            await client.ConnectAsync(peer.IPAddress, peer.Port);

            // Certificate verification: Ensure server's cert matches their claimed SSH public key
            var sslStream = new SslStream(client.GetStream(), false,
                (sender, certificate, chain, errors) =>
                {
                    if (certificate == null)
                    {
                        Log.Warning($"Peer {peer.Username} sent no certificate");
                        return false;
                    }

                    var cert = new X509Certificate2(certificate);
                    var isValid = CryptoUtility.VerifyCertificateMatchesSSHKey(cert, peer.SSHPublicKey);

                    if (!isValid)
                    {
                        Log.Warning($"Certificate from {peer.Username} does not match their SSH public key");
                    }

                    return isValid;
                });

            await sslStream.AuthenticateAsClientAsync(TLS_SERVER_NAME);

            // Receive challenge from server
            var challengeMsg = await ReadMessageAsync(sslStream);
            if (challengeMsg == null || challengeMsg["Type"].GetString() != MSG_TYPE_CHALLENGE)
            {
                Log.Warning($"Expected challenge from peer {peer.Username}, got: {challengeMsg?["Type"].GetString()}");
                return false;
            }

            var challenge = challengeMsg["Challenge"].GetString() ?? "";

            // Sign challenge with our private key
            string signature;
            try
            {
                if (_localUser.SSHPrivateKeyPath == null)
                {
                    Log.Error("SSH private key path is not configured");
                    return false;
                }

                signature = CryptoUtility.SignChallenge(challenge, _localUser.SSHPrivateKeyPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to sign challenge");
                return false;
            }

            // Generate combined art
            var avatarLines = GetLocalUserAvatar();
            var combinedArt = RandomartAvatarUtility.GenerateCombinedArt(_localUser.SSHPublicKey, avatarLines);
            var combinedArtString = RandomartAvatarUtility.CombinedArtToString(combinedArt);

            // Send peer identification message with signature
            var peerIdentification = new Dictionary<string, object>
            {
                ["Type"] = MSG_TYPE_PEER_IDENTIFICATION,
                ["Username"] = _localUser.Username,
                ["SSHPublicKey"] = _localUser.SSHPublicKey,
                ["AvatarType"] = _localUser.AvatarType,
                ["AvatarColor"] = _localUser.AvatarColor,
                ["CombinedRandomartAvatar"] = combinedArtString,
                ["Signature"] = signature
            };

            await SendMessageAsync(sslStream, peerIdentification);

            peer.IsConnected = true;
            _activePeerConnections[peer.Username] = sslStream;

            Log.Information($"Connected to peer: {peer}");
            PeerConnected?.Invoke(this, peer);

            // Start handling messages
            _ = Task.Run(() => HandlePeerMessages(peer, sslStream));

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to connect to peer: {peer}");
            return false;
        }
    }

    /// <summary>
    /// Handle incoming messages from a peer
    /// </summary>
    private async Task HandlePeerMessages(TLSPeer peer, SslStream sslStream)
    {
        try
        {
            while (_isRunning && peer.IsConnected)
            {
                var message = await ReadMessageAsync(sslStream);
                if (message == null)
                    break;

                var type = message.ContainsKey("Type") ? message["Type"].GetString() : null;
                Log.Debug($"Received message from {peer.Username}: {type}");

                // Handle different message types
                switch (type)
                {
                    case MSG_TYPE_GRAPH_SYNC:
                        // Handle graph synchronization
                        break;
                    case MSG_TYPE_DEVICE_UPDATE:
                        // Handle device update
                        break;
                    case MSG_TYPE_PING:
                        await SendMessageAsync(sslStream, new { Type = MSG_TYPE_PONG });
                        break;
                }

                MessageReceived?.Invoke(this, (peer, JsonSerializer.Serialize(message)));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error handling messages from {peer.Username}");
        }
        finally
        {
            peer.IsConnected = false;
            _activePeerConnections.Remove(peer.Username);
            PeerDisconnected?.Invoke(this, peer);
        }
    }

    /// <summary>
    /// Send message to a peer
    /// </summary>
    private async Task SendMessageAsync(SslStream stream, object message)
    {
        var json = JsonSerializer.Serialize(message);
        var data = Encoding.UTF8.GetBytes(json);
        var length = BitConverter.GetBytes(data.Length);

        await stream.WriteAsync(length);
        await stream.WriteAsync(data);
        await stream.FlushAsync();
    }

    /// <summary>
    /// Read message from a peer
    /// </summary>
    private async Task<Dictionary<string, JsonElement>?> ReadMessageAsync(SslStream stream)
    {
        var lengthBuffer = new byte[4];
        var bytesRead = await stream.ReadAsync(lengthBuffer.AsMemory(0, 4));
        if (bytesRead != 4)
            return null;

        var length = BitConverter.ToInt32(lengthBuffer);
        if (length <= 0 || length > 1_000_000) // Max 1MB
            return null;

        var buffer = new byte[length];
        bytesRead = await stream.ReadAsync(buffer.AsMemory(0, length));
        if (bytesRead != length)
            return null;

        var json = Encoding.UTF8.GetString(buffer);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
    }

    /// <summary>
    /// Load server certificate for TLS
    /// Generates certificate from the user's SSH private key
    /// </summary>
    private X509Certificate2 LoadServerCertificate()
    {
        try
        {
            if (_localUser.SSHPrivateKeyPath == null)
            {
                throw new InvalidOperationException("SSH private key path is not configured");
            }

            // Generate certificate from SSH key
            return CryptoUtility.GenerateCertificateFromSSHKey(_localUser.Username, _localUser.SSHPrivateKeyPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to generate certificate from SSH key, falling back to file");
            // Fallback: Load from file if SSH key generation fails
            if (File.Exists(CERT_FILE_PATH))
            {
                return X509CertificateLoader.LoadPkcs12FromFile(CERT_FILE_PATH, CERT_PASSWORD);
            }
            throw new InvalidOperationException("No valid certificate available. Please configure SSH keys or provide a certificate file.");
        }
    }

    /// <summary>
    /// Get all discovered peers
    /// </summary>
    public List<TLSPeer> GetDiscoveredPeers()
    {
        return _discoveredPeers.ToList();
    }

    /// <summary>
    /// Get connected peers
    /// </summary>
    public List<TLSPeer> GetConnectedPeers()
    {
        return _discoveredPeers.Where(p => p.IsConnected).ToList();
    }

    /// <summary>
    /// Get avatar lines for local user (custom or predefined)
    /// </summary>
    private string[] GetLocalUserAvatar()
    {
        // Check if user has custom avatar lines
        if (_localUser.CustomAvatarLines != null && _localUser.CustomAvatarLines.Length == 4)
        {
            return _localUser.CustomAvatarLines;
        }

        // Fall back to predefined avatar
        var avatar = AvatarUtility.GetAvatar(_localUser.AvatarType);
        return avatar?.Appearance ?? new[]
        {
            "   o   ",
            "./\\|/\\.",
            "( o.o )",
            " > ^ < "
        };
    }

    public void Dispose()
    {
        Stop();
        _discoveryListener?.Dispose();
        _announcementTimer?.Dispose();
    }
}
