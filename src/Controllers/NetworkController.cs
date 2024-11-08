// Network Controller to handle all network operations in the application at a high-level
// For specific functionality, see NetData.cs in Utilities

using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using TLScope.src.Services;
using TLScope.src.Models;
using TLScope.src.Debugging;
using TLScope.src.Utilities;

namespace TLScope.src.Controllers {
    public class NetworkController {
        private ConcurrentDictionary<string, Device> _activeDevices = new();

        public readonly NetworkInterface? _networkInterface;

        public NetworkController() {
            // Initialize the network interface (example: get the first network interface)
            _networkInterface = NetworkInterface.GetAllNetworkInterfaces().LastOrDefault();
            if (_networkInterface == null) {
                throw new InvalidOperationException("Network interface is null");
            }
            if (_networkInterface.OperationalStatus.ToString() == "Down") {
                throw new InvalidOperationException(
                    $"Network Interface ({_networkInterface.Name}) is offline. "+
                    $"Make sure you are connected to the Internet.");
            }
            Logging.Write("NetworkController initialized.");
        }

        public async Task DiscoverLocalNetworkAsync(CancellationToken cancTok) {
            try {
                if (_networkInterface == null) {
                    throw new InvalidOperationException("Network interface is null");
                }
                Logging.Write(
                    $"Hosting from Interface {_networkInterface.Name}\n" +
                    $"\tIP Address: {NetData.GetLocalIPAddress(_networkInterface)}\n" +
                    $"\tMAC Address: {NetData.GetLocalMacAddress(_networkInterface)}"
                );

                var scanTask = NetworkService.ScanNetworkAsync(_activeDevices, cancTok);
                var pingTask = NetworkService.PingDevicesAsync(_activeDevices, cancTok);

                await Task.WhenAll(scanTask, pingTask);
                Logging.Write("Network discovery completed.");
            } catch (OperationCanceledException) {
                Logging.Write("Network discovery was canceled.");
            } catch (Exception ex) {
                Logging.Error("An error occurred during network discovery.", ex);
            }
        }

        public ref ConcurrentDictionary<string, Device> GetActiveDevices() {
            return ref _activeDevices;
        }
    }
}
