using System;
using System.Threading.Tasks;
using TLScope.src.Services;
using TLScope.src.Debugging;

namespace TLScope.src.Controllers {
    public class NetworkController {
        private readonly NetworkService _networkService;

        public NetworkController(NetworkService networkService) {
            _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
            Logging.Write("NetworkController initialized.");
        }

        public async Task DiscoverLocalNetworkAsync() {
            try {
                await _networkService.DiscoverLocalNetworkAsync();
                Logging.Write("Network discovery completed.");
            } catch (Exception ex) {
                Logging.Error("An error occurred during network discovery.", ex);
            }
        }
    }
}
