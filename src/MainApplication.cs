using System;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui;
using TLScope.src.Controllers;
using TLScope.src.Debugging;
using TLScope.src.Utilities;
using TLScope.src.Views;

namespace TLScope.src {
    public class MainApplication {
        private readonly NetworkController _networkController;
        private NetworkView? _networkView;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        public MainApplication(NetworkController networkController) {
            _networkController = networkController ?? throw new ArgumentNullException(nameof(networkController));
            Logging.Write("MainApplication initialized.");
        }

        public void Run() {
            try {
                Logging.Write("Main application started.");

                Application.Init();
                Application.UseSystemConsole = false;
                Application.Resized -= OnTerminalResized;
                var top = Application.Top;

                // Pass the existing NetworkController instance to NetworkView
                _networkView = new NetworkView(_networkController);

                var win = new Window("Welcome to TLScope (Ctrl+Q to quit)") {
                    X = 0,
                    Y = 1, // Leave one row for the menu
                    Width = Dim.Fill(),
                    Height = Dim.Fill(),
                    CanFocus = false,
                    ColorScheme = Constants.TLSColorScheme
                };
                top.Add(win);
                top.Add(new MenuBar(new MenuBarItem[] {
                    new MenuBarItem("_File", new MenuItem[] {
                        new MenuItem("_Quit", "", () => Application.RequestStop(), null, null, Key.CtrlMask | Key.Q)
                    })
                }));

                top.Add(_networkView);

                // Subscribe to the Application.Resized event
                Application.Resized += OnTerminalResized;

                // Run network discovery and UI updates in separate tasks
                Task.Run(() => UpdateNetworkView(_cancellationTokenSource.Token));
                Task.Run(() => DiscoverNetwork(_cancellationTokenSource.Token));

                Application.Run();
            } catch (Exception ex) {
                Logging.Error("An error occurred in the main application.", ex, true);
            } finally {
                // Cancel ongoing tasks
                _cancellationTokenSource.Cancel();

                // Unsubscribe event handlers
                Application.Resized -= OnTerminalResized;

                // Ensure proper shutdown
                Application.Shutdown();
                Logging.Write("Main application stopped.");
            }
        }

        private async Task DiscoverNetwork(CancellationToken cancellationToken) {
            try {
                while (!cancellationToken.IsCancellationRequested) {
                    await _networkController.DiscoverLocalNetworkAsync(cancellationToken);
                    await Task.Delay(5000, cancellationToken);
                }
            } catch (OperationCanceledException) {
                Logging.Write("Network discovery was canceled.");
            } catch (Exception ex) {
                Logging.Error("An error occurred during network discovery.", ex);
            }
        }

        private async Task UpdateNetworkView(CancellationToken cancellationToken) {
            if (_networkView == null) {
                throw new InvalidOperationException("NetworkView is null.");
            }

            try {
                while (!cancellationToken.IsCancellationRequested) {
                    await Task.Delay(1000, cancellationToken); // Throttle UI updates to once per second
                    _networkView.UpdateDeviceList();
                }
            } catch (OperationCanceledException) {
                Logging.Write("UI update was canceled.");
            } catch (IndexOutOfRangeException ex) {
                Logging.Error("An error occurred during UI update.", ex);
                // Add additional logging to capture the state
                Logging.Write($"Exception Details: {ex.Message}");
                Logging.Write($"Stack Trace: {ex.StackTrace}");
            } catch (Exception ex) {
                Logging.Error("An error occurred during UI update.", ex);
            }
        }

        private void OnTerminalResized(Application.ResizedEventArgs args) {
            // Handle the terminal resize event
            _networkView?.SetNeedsDisplay();
        }
    }
}
