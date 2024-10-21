using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TLScope.src.Data;
using TLScope.src.Services;
using Terminal.Gui;

namespace TLScope
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var networkService = serviceProvider.GetRequiredService<NetworkService>();
            var tlsService = serviceProvider.GetRequiredService<TlsService>();

            await tlsService.MonitorNetworkAsync();

            // GUI & Visualization
            Application.Init();
            var top = Application.Top;

            var win = new Window("TLScope - CTRL-C to quit")
            {
                X = 0,
                Y = 1, // Leave one row for the menu
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var networkInfoLabel = new Label("Hosting TLScope on the local network...")
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(),
                Height = 1
            };
            win.Add(networkInfoLabel);

            top.Add(win);

            top.Add(new MenuBar(new MenuBarItem[]
            {
                new MenuBarItem("_File", new MenuItem[]
                {
                    new MenuItem("_Quit", "", () => Application.RequestStop(), null, null, Key.CtrlMask | Key.C)
                })
            }));

            _ = Task.Run(async () =>
            {
                try
                {
                    await tlsService.MonitorNetworkAsync();
                    Application.MainLoop.Invoke(() =>
                    {
                        networkInfoLabel.Text = $"Hosting From {networkService.LocalIPAddress}\nNetwork devices discovered.";
                    });
                }
                catch (Exception ex)
                {
                    Application.MainLoop.Invoke(() =>
                    {
                        networkInfoLabel.Text = $"Error: {ex.Message}";
                    });
                }
            });

            Application.Run();
            Application.Shutdown();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite("Data Source=tlscope.db"));

            services.AddTransient<NetworkService>();
            services.AddTransient<TlsService>();
        }
    }
}