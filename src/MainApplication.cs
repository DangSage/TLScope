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
        private NetworkController _networkController;
        private readonly NetView _networkView;
        private readonly UserView _userView;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public MainApplication(NetworkController networkController) {
            _networkController = networkController ?? throw new ArgumentNullException(nameof(networkController));
            _networkView = new NetView(ref _networkController);
            _userView = new UserView(
                _networkController._networkInterface ??
                    throw new ArgumentNullException(nameof(_networkController._networkInterface))
                );
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Run() {
            try {
                Application.Init();
                var top = Application.Top;

                top.Add(new MenuBar(new MenuBarItem[] {
                    new("_File", new MenuItem[] {
                        new("_GitHub", "", () => ConsoleHelper.OpenGitHubRepository(), null, null, Key.CtrlMask | Key.G),
                        new("_Quit", "", () => Application.RequestStop(), null, null, Key.CtrlMask | Key.Q),
                    })
                }));

                top.Add(_networkView);
                top.Add(_userView);

                Application.Resized += OnTerminalResized;
                Application.MainLoop.AddIdle(() => {
                    if (_networkView.Visible) {
                        _networkView.UpdateView(ref _networkController);
                    }
                    Application.Refresh();
                    return true;
                });

                Task.Run(() => _networkController.DiscoverLocalNetworkAsync(_cancellationTokenSource.Token));
                Application.Run();
            } catch (Exception ex) {
                Logging.Error("An error occurred in the main application.", ex, true);
            } finally {
                _cancellationTokenSource.Cancel();
                Application.Resized -= OnTerminalResized;
                Application.Shutdown();
                Logging.Write("Main application stopped.");
            }
        }

        private void OnTerminalResized(Application.ResizedEventArgs args) {
            try {
                _networkView.SetNeedsDisplay();
                Logging.Write("Terminal resized.");
            } catch (Exception ex) {
                Logging.Error("An error occurred during terminal resize.", ex);
            }
        }
    }
}
