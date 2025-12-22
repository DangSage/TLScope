using System.Net;
using TLScope.Models;

namespace TLScope.Services;

/// <summary>
/// Interface for TLS peer services
/// Allows for mock implementations during testing
/// </summary>
public interface ITlsPeerService : IDisposable
{
    event EventHandler<TLSPeer>? PeerDiscovered;
    event EventHandler<TLSPeer>? PeerConnected;
    event EventHandler<TLSPeer>? PeerDisconnected;
    event EventHandler<(TLSPeer Peer, string Message)>? MessageReceived;

    void Start();
    void Stop();
    void AnnouncePeer();
    void ProbeDevice(IPAddress ipAddress);
    Task<bool> ConnectToPeer(TLSPeer peer);
    List<TLSPeer> GetDiscoveredPeers();
    List<TLSPeer> GetConnectedPeers();
}
