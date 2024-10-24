// Wrapper class for all the application logic and services.

using System;
using System.Threading.Tasks;
using TLScope.src.Controllers;
using TLScope.src.Debugging;

namespace TLScope.src {
    public class MainApplication {
        private readonly NetworkController _networkController;

        public MainApplication(NetworkController networkController) {
            _networkController = networkController ?? throw new ArgumentNullException(nameof(networkController));
            Logging.Write("MainApplication initialized.");
        }

        public void Run() {
            try {
                // Main application logic goes here
                // For example, you can call methods from _networkController and _tlsService
                // to perform network operations and TLS handshakes
                Logging.Write("Main application started.");

                // Start using networkController as an asynchronous operation
                _networkController.DiscoverLocalNetworkAsync().Wait();

            } catch (Exception ex) {
                Logging.Error("An error occurred in the main application.", ex);
            }
        }
    }
}
