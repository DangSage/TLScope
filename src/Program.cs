using TLScope.src.Models;
using TLScope.src.Services;
using Terminal.Gui;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace TLScope
{
    class Program
    {
        static void Main(string[] args)
        {
            // Call the asynchronous method and wait for it to complete
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            try
            {
                // Create a new instance of the NetworkService class
                NetworkService networkService = new();

                // ## GUI & Visualization
                // - Terminal.Gui (1.4.0+): Text-based UI for terminal applications

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
                var networkInfoLabel = new Label("Discovering network devices...")
                {
                    X = 1,
                    Y = 1,
                    Width = Dim.Fill(),
                    Height = 1
                };
                win.Add(networkInfoLabel);

                Application.Top.Add(win);

                // Add a key binding for Ctrl+Q to exit the application
                Application.Top.Add(new MenuBar(new MenuBarItem[] {
                    new MenuBarItem("_File", new MenuItem[] {
                        new MenuItem("_Quit", "", () => Application.RequestStop(), null, null, Key.CtrlMask | Key.C)
                    })
                }));

                // Start a background task for network discovery
                _ = Task.Run(async () =>
                {
                    await networkService.DiscoverLocalNetworkAsync();
                });

                // Use a Timer to periodically update the networkInfoLabel
                var timer = new Timer((state) =>
                {
                    Application.MainLoop.Invoke(() =>
                    {
                        networkInfoLabel.Text = "Discovered devices:\n";
                        foreach (var device in networkService.activeDevices)
                        {
                            networkInfoLabel.Text += $"{device.Key}: {(device.Value ? "Active" : "Inactive")}\n";
                        }
                    });
                }, null, 0, 1000); // Update every second

                Application.Run();
                // Dispose the timer when the application shuts down
                timer.Dispose();
                Application.Shutdown();

                await Task.WhenAll();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }
}