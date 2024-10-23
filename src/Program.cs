using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TLScope.src.Data;
using TLScope.src.Services;
using TLScope.src.Utilities;
using TLScope.src.Controllers;
using System;
using TLScope.src.Debugging;

namespace TLScope {
    class Program {
        static void Main(string[] args) {

            if (args.Length > 0) {
                if (args[0] == "--version") {
                    VersionInfo.TLScopeVersionCheck();
                    return;
                    }
                }

            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            var dbContext = serviceProvider.GetService<ApplicationDbContext>();
            // if dbContext is null, create a new instance of ApplicationDbContext
            if (dbContext == null) {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlite("Data Source=tlscope.db")
                    .Options;
                dbContext = new ApplicationDbContext(options);
                }

            try {
                // Run the CLIController first
                var cliController = new CLIController(dbContext);
                cliController.RunCLI();

                // MainApplication(networkService, tlsService);
                } catch (Exception ex) {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Logging.Error("An error occurred in the main application. (NOT Caught)", ex, true);
                }
            }

        private static void ConfigureServices(IServiceCollection services) {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite("Data Source=tlscope.db"));

            services.AddTransient<NetworkService>();
            services.AddTransient<TlsService>();
            }
        }
    }
