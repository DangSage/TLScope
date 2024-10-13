using TLScope.src.Models;
using TLScope.src.Services;
using Terminal.Gui;
using TLScope.src.Debugging;

namespace TLScope
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await MainAsync(args);
        }

        static async Task MainAsync(string[] args)
        {
            try
            {
                NetworkService networkService = new();

                // ## GUI & Visualization. This is temporary

                Application.Init();

                var colorS = new ColorScheme {
                    Normal = Terminal.Gui.Attribute.Make(Color.White, Color.Black),
                    Focus = Terminal.Gui.Attribute.Make(Color.Black, Color.White),
                    HotNormal = Terminal.Gui.Attribute.Make(Color.White, Color.Black),
                    HotFocus = Terminal.Gui.Attribute.Make(Color.Black, Color.White),
                };

                var win = new Window("TLScope - CTRL-C to quit")
                {
                    X = 2,
                    Y = 2,
                    Width = Dim.Fill(2),
                    Height = Dim.Fill(2),
                    ColorScheme = colorS
                };

                // Create a Label to display network information
                var networkInfoLabel = new Label("Hosting TLScope on the local network...")
                {
                    X = 1,
                    Y = 1,
                    Width = Dim.Fill(),
                    Height = 1
                };
                win.Add(networkInfoLabel);

                Application.Top.Add(win);

                // Add a key binding for Ctrl+C to exit the application
                Application.Top.Add(new MenuBar([
                    new("_File", new MenuItem[] {
                        new("_Quit", "", () => Application.RequestStop(), null, null, Key.CtrlMask | Key.C)
                    })
                ]));

                // Start network discovery in a background task
                _ = Task.Run(async () => {
                    await networkService.DiscoverLocalNetworkAsync();
                    Application.MainLoop.Invoke(() => {
                        networkInfoLabel.Text = $"Hosting From {networkService.LocalIPAddress}\nNetwork devices discovered.";
                    });
                });

                Application.Run();
                Application.Shutdown();
                // if (true) throw new Exception("This is a test exception.");
            }
            catch (Exception ex)
            {
                Logging.Error($"{ex.Message}", ex, true);
            }
            await Task.Delay(10);

            Logging.Write("Closing TLScope.");
        }
    }
}