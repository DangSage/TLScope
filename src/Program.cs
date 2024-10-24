using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TLScope.src.Data;
using TLScope.src.Services;
using TLScope.src.Utilities;
using TLScope.src.Controllers;
using System;
using TLScope.src.Debugging;

namespace TLScope.src {
    class Program {
        static void Main(string[] args) {
            Utilities.Environment.SetEnvironmentVariables();

            if (args.Length > 0) {
                var cliController = new CLIController(args, null);
                cliController.RunCLI();
                return;
            }

            var services = new ServiceCollection();
            ConfigureServices(services, args);
            var serviceProvider = services.BuildServiceProvider();

            try {
                var cliController = serviceProvider.GetService<CLIController>()
                    ?? throw new InvalidOperationException("CLIController service is null.");
                cliController.RunCLI();

                // Login is successful, start the main application
                var networkService = serviceProvider.GetService<NetworkService>();
                var tlsService = serviceProvider.GetService<TlsService>();

                // var mainApp = new MainApplication(networkService, tlsService);
                // mainApp.Run();
            } catch (Exception ex) {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Logging.Error("An error occurred in the main application. (NOT Caught)", ex, true);
            }
        }

        private static void ConfigureServices(IServiceCollection services, string[] args) {
            services.AddSingleton(args); // Register string[] args as a singleton service
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite("Data Source=tlscope.db"));

            services.AddTransient<NetworkService>();
            services.AddTransient<TlsService>();
            services.AddTransient<CLIController>();
        }
    }
}
