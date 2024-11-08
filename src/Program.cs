using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TLScope.src.Data;
using TLScope.src.Services;
using TLScope.src.Controllers;
using TLScope.src.Utilities;
using TLScope.src.Debugging;

namespace TLScope.src {
    class Program {
        static void Main(string[] args) {
            Utilities.Environment.SetEnvironmentVariables();

            if (args.Length > 0) {
                var cliController = new CLIController(args, null);
                cliController.RunCLI(); // should exit the environment on its own
            }

            var services = new ServiceCollection();
            ConfigureServices(services, args);
            var serviceProvider = services.BuildServiceProvider();

            try {
                var cliController = serviceProvider.GetService<CLIController>()
                    ?? throw new InvalidOperationException("CLIController service is null.");
                cliController.RunCLI();

                var mainApp = new MainApplication(serviceProvider.GetService<NetworkController>()
                    ?? throw new InvalidOperationException("NetworkController service is null."));
                mainApp.Run();
                // clean up
                serviceProvider.Dispose();

            } catch (InvalidOperationException ex) {
                Logging.Write(ex.Message);
                Console.WriteLine($"{ex.Message}");
            } catch (Exception ex) {
                Logging.Error("Fatal Error caught in main application", ex, true);
                Console.WriteLine($"An error occurred: {ex.Message}");
            } finally {
                Logging.Write("Exiting TLScope...");
                Console.WriteLine(Constants.GoodbyeMessage);
            }
        }

        private static void ConfigureServices(IServiceCollection services, string[] args) {
            services.AddSingleton(args); // Register string[] args as a singleton service
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite("Data Source=tlscope.db"));

            services.AddTransient<NetworkService>();
            services.AddTransient<NetworkController>();
            services.AddTransient<CLIController>();
        }
    }
}
