// NetworkController should be a controller that handles all network related operations.
// It should have a method called DiscoverLocalNetwork that returns a list of devices on the local network.
// DiscoverLocalNetwork should call NetworkService.DiscoverLocalNetworkAsync and return the result in bursts of time.
// NetworkController should have a method called MonitorNetwork that calls DiscoverLocalNetwork and logs the result.
// NetworkController has a dependency on NetworkService. Inject NetworkService into NetworkController.
// NetworkController should be used in Program.cs to monitor the network and display the results in the GUI.
// NetworkController needs to be able to control, resume, stop, and restart the network monitoring process.
// NetworkController should be able to handle exceptions and log them.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using TLScope.src.Services;

namespace TLScope.src.Controllers {
    public class NetworkController {
        private readonly NetworkService _networkService;

        public NetworkController(NetworkService networkService) {
            _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
        }

        public async Task<IEnumerable<string>> DiscoverLocalNetworkAsync() {
            try {
                await _networkService.DiscoverLocalNetworkAsync();
                return _networkService.activeDevices.Keys;
            } catch (Exception ex) {
                throw new Exception($"Error discovering local network: {ex.Message}");
            }
        }

        public async Task MonitorNetworkAsync() {
            try {
                var devices = await DiscoverLocalNetworkAsync();
                foreach (var device in devices) {
                    Console.WriteLine(device);
                }
            } catch (Exception ex) {
                Console.WriteLine($"Error monitoring network: {ex.Message}");
            }
        }
    }
}
