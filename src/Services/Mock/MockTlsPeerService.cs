using System.Net;
using TLScope.Models;
using TLScope.Services;
using TLScope.Utilities;
using Serilog;

namespace TLScope.Services.Mock;

/// <summary>
/// Mock TLS peer service for UI testing
/// Simulates peer discovery and connections without real network activity
/// </summary>
public class MockTlsPeerService : ITlsPeerService
{
    private readonly User _localUser;
    private readonly List<TLSPeer> _discoveredPeers = new();
    private bool _isRunning;
    private CancellationTokenSource? _simulationCancellation;

    public event EventHandler<TLSPeer>? PeerDiscovered;
    public event EventHandler<TLSPeer>? PeerConnected;
    public event EventHandler<TLSPeer>? PeerDisconnected;
#pragma warning disable CS0067 // Event is never used - required by ITlsPeerService interface
    public event EventHandler<(TLSPeer Peer, string Message)>? MessageReceived;
#pragma warning restore CS0067

    public MockTlsPeerService(User localUser)
    {
        _localUser = localUser;
    }

    /// <summary>
    /// Start mock peer discovery
    /// </summary>
    public void Start()
    {
        if (_isRunning)
            return;

        _isRunning = true;
        _simulationCancellation = new CancellationTokenSource();

        Log.Information("Mock TLS P2P service started");

        // Start simulation in background
        Task.Run(() => SimulatePeerActivity(_simulationCancellation.Token));
    }

    /// <summary>
    /// Stop mock peer service
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
            return;

        _isRunning = false;
        _simulationCancellation?.Cancel();

        Log.Information("Mock TLS P2P service stopped");
    }

    /// <summary>
    /// Announce presence to network (mock implementation - does nothing)
    /// </summary>
    public void AnnouncePeer()
    {
        Log.Information($"Mock announcing peer: {_localUser.Username}");
        // In mock mode, do nothing - peers are simulated
    }

    /// <summary>
    /// Probe a device for TLScope peer (mock implementation - does nothing)
    /// </summary>
    public void ProbeDevice(IPAddress ipAddress)
    {
        Log.Debug($"Mock probing device: {ipAddress}");
        // In mock mode, do nothing - peers are simulated
    }

    /// <summary>
    /// Simulate peer discovery and connections
    /// </summary>
    private async Task SimulatePeerActivity(CancellationToken cancellationToken)
    {
        var random = new Random();
        await Task.Delay(1000, cancellationToken); // Initial delay

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Randomly discover new peers
                if (random.Next(100) < 20 && _discoveredPeers.Count < 10)
                {
                    var peer = GenerateRandomPeer();
                    if (!_discoveredPeers.Any(p => p.Username == peer.Username))
                    {
                        _discoveredPeers.Add(peer);
                        Log.Information($"Mock discovered peer: {peer}");
                        PeerDiscovered?.Invoke(this, peer);
                    }
                }

                // Randomly connect to peers
                if (_discoveredPeers.Count > 0 && random.Next(100) < 15)
                {
                    var disconnectedPeers = _discoveredPeers.Where(p => !p.IsConnected).ToList();
                    if (disconnectedPeers.Any())
                    {
                        var peer = disconnectedPeers[random.Next(disconnectedPeers.Count)];
                        peer.IsConnected = true;
                        peer.LastConnected = DateTime.UtcNow;
                        Log.Information($"Mock connected to peer: {peer}");
                        PeerConnected?.Invoke(this, peer);
                    }
                }

                // Randomly disconnect from peers
                if (_discoveredPeers.Count > 0 && random.Next(100) < 5)
                {
                    var connectedPeers = _discoveredPeers.Where(p => p.IsConnected).ToList();
                    if (connectedPeers.Any())
                    {
                        var peer = connectedPeers[random.Next(connectedPeers.Count)];
                        peer.IsConnected = false;
                        Log.Information($"Mock disconnected from peer: {peer}");
                        PeerDisconnected?.Invoke(this, peer);
                    }
                }

                await Task.Delay(random.Next(2000, 5000), cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    /// <summary>
    /// Generate a random peer for testing
    /// </summary>
    private TLSPeer GenerateRandomPeer()
    {
        var random = new Random();
        var usernames = new[] { "alice", "bob", "charlie", "diana", "eve", "frank", "grace", "hank", "ivy", "jack" };
        var avatarTypes = new[] { "APPEARANCE_DEFAULT", "APPEARANCE_HAPPY", "APPEARANCE_SLEEPY", "APPEARANCE_WINKING" };
        var colors = new[] { "#FF6B6B", "#4ECDC4", "#45B7D1", "#FFA07A", "#98D8C8", "#F7DC6F", "#BB8FCE", "#85C1E2" };

        var username = usernames[random.Next(usernames.Length)] + random.Next(1, 999);
        var sshKey = $"ssh-rsa AAAA{Convert.ToBase64String(new byte[32])} {username}@tlscope";

        // Generate mock combined randomart + avatar art
        var avatarType = avatarTypes[random.Next(avatarTypes.Length)];
        var avatar = AvatarUtility.GetAvatar(avatarType);
        var avatarLines = avatar?.Appearance ?? new[] { "   o   ", "./\\|/\\.", "( o.o )", " > ^ < " };
        var combinedArt = RandomartAvatarUtility.GenerateCombinedArt(sshKey, avatarLines);
        var combinedArtString = RandomartAvatarUtility.CombinedArtToString(combinedArt);

        // 70% chance of being verified
        var isVerified = random.Next(100) < 70;

        return new TLSPeer
        {
            Username = username,
            IPAddress = $"192.168.1.{random.Next(2, 254)}",
            Port = 8443,
            SSHPublicKey = sshKey,
            AvatarType = avatarType,
            AvatarColor = colors[random.Next(colors.Length)],
            CombinedRandomartAvatar = combinedArtString,
            IsVerified = isVerified,
            LastVerified = isVerified ? DateTime.UtcNow.AddMinutes(-random.Next(1, 60)) : null,
            Version = "1.0.0",
            FirstSeen = DateTime.UtcNow,
            IsConnected = false
        };
    }

    /// <summary>
    /// Manually trigger peer discovery (for testing)
    /// </summary>
    public void TriggerPeerDiscovery(TLSPeer peer)
    {
        if (!_discoveredPeers.Any(p => p.Username == peer.Username))
        {
            _discoveredPeers.Add(peer);
            PeerDiscovered?.Invoke(this, peer);
        }
    }

    /// <summary>
    /// Manually connect to a peer (for testing)
    /// </summary>
    public Task<bool> ConnectToPeer(TLSPeer peer)
    {
        peer.IsConnected = true;
        peer.LastConnected = DateTime.UtcNow;
        PeerConnected?.Invoke(this, peer);
        return Task.FromResult(true);
    }

    public List<TLSPeer> GetDiscoveredPeers()
    {
        return _discoveredPeers.ToList();
    }

    public List<TLSPeer> GetConnectedPeers()
    {
        return _discoveredPeers.Where(p => p.IsConnected).ToList();
    }

    public void Dispose()
    {
        Stop();
        _simulationCancellation?.Dispose();
    }
}
