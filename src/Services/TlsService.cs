using System;
using System.Threading.Tasks;
using TLScope.src.Debugging;

namespace TLScope.src.Services {
    public class TlsService {
        private readonly NetworkService _networkService;

        public TlsService(NetworkService networkService) {
            _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
        }

        public async Task MonitorNetworkAsync() {
            try {
                await _networkService.DiscoverLocalNetworkAsync();
                Logging.Write("Network discovery started.");
            } catch (Exception ex) {
                Logging.Error("Error in MonitorNetworkAsync", ex);
            }
        }

        // Additional methods to work with NetworkService
    }
}