// Wrapper class for all the application logic and services.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TLScope.src.Services;
using TLScope.src.Utilities;
using TLScope.src.Debugging;

namespace TLScope.src {
    public class MainApplication {
        private readonly NetworkService _networkService;
        private readonly TlsService _tlsService;

        public MainApplication(NetworkService networkService, TlsService tlsService) {
            _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
            _tlsService = tlsService ?? throw new ArgumentNullException(nameof(tlsService));
            Logging.Write("MainApplication initialized.");
            }

        public void Run() {
            try {
                // Main application logic goes here
                // For example, you can call methods from _networkService and _tlsService
                // to perform network operations and TLS handshakes
                } catch (Exception ex) {
                Logging.Error("An error occurred in the main application.", ex);
                }
            }
        }
    }
