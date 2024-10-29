using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TLScope.src.Services;
using TLScope.src.Models;
using TLScope.src.Debugging;

namespace TLScope.src.Controllers {
    public class NetworkController {
        private readonly NetworkService _networkService;
        private ConcurrentDictionary<string, Device> _activeDevices = new();

        public event Action<ConcurrentDictionary<string, Device>>? DevicesUpdated;

        public NetworkController(NetworkService networkService) {
            _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
            Logging.Write("NetworkController initialized.");
        }

        public async Task<ConcurrentDictionary<string, Device>> DiscoverLocalNetworkAsync(CancellationToken cancellationToken) {
            try {
                var localIPAddress = GetLocalIPAddress();
                if (localIPAddress == null) {
                    Logging.Error("Local IP Address not found. Network discovery aborted.");
                    return _activeDevices;
                }

                var scanTask = _networkService.ScanNetworkAsync(localIPAddress, _activeDevices, cancellationToken);
                var pingTask = _networkService.PingDevicesAsync(_activeDevices, cancellationToken);

                await Task.WhenAll(scanTask, pingTask);
                Logging.Write("Network discovery completed.");
                OnDevicesUpdated();
            } catch (OperationCanceledException) {
                Logging.Write("Network discovery was canceled.");
            } catch (Exception ex) {
                Logging.Error("An error occurred during network discovery.", ex);
            }

            return _activeDevices;
        }

        private void OnDevicesUpdated() {
            DevicesUpdated?.Invoke(_activeDevices);
        }

        public ref ConcurrentDictionary<string, Device> GetActiveDevices() {
            return ref _activeDevices;
        }

        private static string GetLocalIPAddress() {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces()) {
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                     networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)) {
                    foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses) {
                        if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork) {
                            Logging.Write($"Hosting from Local IP Address: {unicastAddress.Address}");
                            return unicastAddress.Address.ToString();
                        }
                    }
                }
            }
            throw new Exception("Local IP Address Not Found! Are you connected to WiFi?");
        }
    }
}
