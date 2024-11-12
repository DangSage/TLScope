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
        private readonly NetView _networkView;
        private readonly UserView _userView;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public MainApplication(NetworkController networkController) {
            _networkController = networkController ?? throw new ArgumentNullException(nameof(networkController));
            _networkView = new NetView(ref _networkController);
            _userView = new UserView(ref _networkController);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Run() {
            try {
                Application.Init();
                var top = Application.Top;

                var menu = new MenuBar(new MenuBarItem[] {
                    new("_File", new MenuItem[] {
                        new("_GitHub", "", () => ConsoleHelper.OpenGitHubRepository(), null, null, Key.CtrlMask | Key.G),
                        new("_About", "", () => MessageBox.Query(60, 10, "About TLScope", Constants.AboutMessage, "OK"), null, null, Key.CtrlMask | Key.A),
                        new("_Quit", "", () => Application.RequestStop(), null, null, Key.CtrlMask | Key.Q),
                    })
                });

                top.Add(menu);

                // Set dynamic dimensions and positions
                _networkView.X = 0;
                _networkView.Y = 1; // Below the menu
                _networkView.Width = Dim.Percent(50);
                _networkView.Height = Dim.Fill();

                _userView.X = Pos.Right(_networkView);
                _userView.Y = 1; // Below the menu
                _userView.Width = Dim.Fill();
                _userView.Height = Dim.Fill();

                top.Add(_networkView);
                top.Add(_userView);

                // Handle terminal resizing
                Application.Resized += (args) => {
                    Logging.Write("Terminal resized.");
                    _networkView.Width = Dim.Percent(50);
                    _userView.X = Pos.Right(_networkView);
                    _userView.Width = Dim.Fill();
                };

                Task.Run(() => _networkController.DiscoverLocalNetworkAsync(_cancellationTokenSource.Token));
                Application.Run();
            } catch (Exception ex) {
                Logging.Error("An error occurred in the main application.", ex, true);
            } finally {
                _cancellationTokenSource.Cancel();
                Application.Shutdown();
                Logging.Write("Main application stopped.");
            }
        }
    }
}
