using Terminal.Gui;
using TLScope.src.Controllers;
using TLScope.src.Debugging;
using TLScope.src.Utilities;
using TLScope.src.Views;

namespace TLScope.src {
    public class MainApplication {
        private readonly NetworkController _networkController;
        private readonly NetworkView _networkView;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        public MainApplication() {
            _networkController = new NetworkController();
            _networkView = new NetworkView(ref _networkController);
            Logging.Write("MainApplication initialized.");
        }

        public void Run() {
            try {
                Logging.Write("Main application started.");

                Application.Init();
                Application.UseSystemConsole = false;
                var top = Application.Top;

                top.Add(new MenuBar(new MenuBarItem[] {
                    new MenuBarItem("_File", new MenuItem[] {
                        new MenuItem("_GitHub", "", () => ConsoleHelper.OpenGitHubRepository(), null, null, Key.CtrlMask | Key.G),
                        new MenuItem("_Quit", "", () => Application.RequestStop(), null, null, Key.CtrlMask | Key.Q),
                    })
                }));

                top.Add(_networkView);

                // Subscribe to the Application.Resized event
                Application.Resized += OnTerminalResized;

                // Run network discovery and UI updates in a single task
                Task.Run(() => DiscoverAndUpdateNetwork(_cancellationTokenSource.Token));

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

        private async Task DiscoverAndUpdateNetwork(CancellationToken cancellationToken) {
            try {
                while (!cancellationToken.IsCancellationRequested) {
                    await _networkController.DiscoverLocalNetworkAsync(cancellationToken);
                    Application.MainLoop.Invoke(() => _networkView.UpdateView());
                    await Task.Delay(5000, cancellationToken);
                }
            } catch (OperationCanceledException) {
                Logging.Write("Network discovery was canceled.");
            } catch (Exception ex) {
                Logging.Error("An error occurred during network discovery.", ex);
            }
        }

        private void OnTerminalResized(Application.ResizedEventArgs args) {
            try {
                // Handle the terminal resize event
                _networkView.SetNeedsDisplay();
                Logging.Write("Terminal resized.");
            } catch (Exception ex) {
                Logging.Error("An error occurred during terminal resize.", ex);
            }
        }
    }
}
