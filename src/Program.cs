using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using TLScope.Data;
using TLScope.Models;
using TLScope.Services;
using TLScope.Views;
using TLScope.Utilities;
using TLScope.Testing;
using TLScope.Commands;

namespace TLScope;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Check for legacy UI test mode (for backwards compatibility)
        if (args.Length > 0 && args[0].Equals("uitest", StringComparison.OrdinalIgnoreCase))
        {
            RunUITests(args.Skip(1).ToArray());
            return 0;
        }

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("tlscope.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("TLScope starting...");

        try
        {
            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            // Initialize database
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await dbContext.Database.EnsureCreatedAsync();
                Log.Information("Database initialized");
            }

            // Load avatars
            AvatarUtility.LoadAvatars();
            Log.Information($"Loaded {AvatarUtility.GetAvatarNames().Count} avatars");

            // If no arguments provided, run the default interactive mode
            if (args.Length == 0)
            {
                return await RunInteractiveMode(serviceProvider);
            }

            // Otherwise, use Spectre.Console.Cli for command parsing
            var app = new CommandApp(new TypeRegistrar(serviceProvider));
            app.Configure(config =>
            {
                config.SetApplicationName("tlscope");

                config.AddCommand<StartCommand>("start")
                    .WithDescription("Start TLScope interactive interface")
                    .WithExample(new[] { "start" })
                    .WithExample(new[] { "start", "--username", "alice" })
                    .WithExample(new[] { "start", "--interface", "eth0" });

                config.AddCommand<UITestCommand>("uitest")
                    .WithDescription("Run UI tests")
                    .WithExample(new[] { "uitest", "Simple" })
                    .WithExample(new[] { "uitest", "Complex", "--text" });

                config.AddCommand<ScanCommand>("scan")
                    .WithDescription("Scan network using ICMP ping sweep")
                    .WithExample(new[] { "scan" })
                    .WithExample(new[] { "scan", "--subnet", "192.168.1" })
                    .WithExample(new[] { "scan", "--start", "1", "--end", "50" });
            });

            return await app.RunAsync(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    static async Task<int> RunInteractiveMode(IServiceProvider serviceProvider)
    {
        // Initialize console state manager to handle alternate screen buffer
        // Note: We don't enter the buffer yet - that happens after login
        using var consoleState = new ConsoleStateManager();

        try
        {
            using var app = serviceProvider.GetRequiredService<MainApplication>();
            await app.Run(consoleState: consoleState);
            Log.Information("Application exited normally");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Interactive mode failed");
            AnsiConsole.WriteException(ex);
            return 1;
        }
        // ConsoleStateManager.Dispose() will be called automatically here,
        // ensuring the alternate screen buffer is exited and cursor is restored
    }

    static void ConfigureServices(IServiceCollection services)
    {
        // Database
        var dbPath = ConfigurationHelper.GetConfigFilePath("tlscope.db");
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Database Context Factory for async operations
        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Load FilterConfiguration
        var filterConfig = FilterConfiguration.Load();
        services.AddSingleton(filterConfig);

        // Services - register as interfaces and implementations
        services.AddScoped<UserService>();

        // GraphService must be registered before PacketCaptureService
        services.AddSingleton<GraphService>();
        services.AddSingleton<IGraphService>(sp => sp.GetRequiredService<GraphService>());

        // NetworkScanService for ICMP scanning
        services.AddSingleton<NetworkScanService>();
        services.AddSingleton<INetworkScanService>(sp => sp.GetRequiredService<NetworkScanService>());

        // GatewayDetectionService for topology mapping
        services.AddSingleton<GatewayDetectionService>();
        services.AddSingleton<IGatewayDetectionService>(sp => sp.GetRequiredService<GatewayDetectionService>());

        // PacketCaptureService with dependencies
        services.AddSingleton<PacketCaptureService>(sp =>
        {
            var config = sp.GetRequiredService<FilterConfiguration>();
            var graphService = sp.GetRequiredService<IGraphService>();
            var networkScanService = sp.GetRequiredService<INetworkScanService>();
            var gatewayDetectionService = sp.GetRequiredService<IGatewayDetectionService>();
            return new PacketCaptureService(config, graphService, networkScanService, gatewayDetectionService);
        });
        services.AddSingleton<IPacketCaptureService>(sp => sp.GetRequiredService<PacketCaptureService>());

        services.AddTransient<TlsPeerService>();

        // Application
        services.AddSingleton<MainApplication>(sp =>
        {
            var captureService = sp.GetRequiredService<IPacketCaptureService>();
            var graphService = sp.GetRequiredService<IGraphService>();
            var userService = sp.GetRequiredService<UserService>();
            var filterConfig = sp.GetRequiredService<FilterConfiguration>();
            var gatewayDetectionService = sp.GetRequiredService<IGatewayDetectionService>();
            return new MainApplication(captureService, graphService, userService, filterConfig, gatewayDetectionService);
        });

        // Commands
        services.AddSingleton<StartCommand>();
        services.AddSingleton<UITestCommand>();
        services.AddSingleton<ScanCommand>();
    }

    static void RunUITests(string[] args)
    {
        var useGui = args.Any(a => a.Equals("--gui", StringComparison.OrdinalIgnoreCase));
        var useText = args.Any(a => a.Equals("--text", StringComparison.OrdinalIgnoreCase));

        if (!useGui && !useText)
        {
            useText = true;
        }

        var scenarioArg = args.FirstOrDefault(a => !a.StartsWith("--"));
        var scenario = TestScenario.Simple;
        if (!string.IsNullOrEmpty(scenarioArg) && Enum.TryParse<TestScenario>(scenarioArg, true, out var parsed))
        {
            scenario = parsed;
        }

        if (useText)
        {
            AnsiConsole.MarkupLine("[cyan1]TLScope UI Test - Console Mode[/]");
            AnsiConsole.WriteLine();
            var runner = new ConsoleTestRunner(scenario);
            runner.Run();
        }
        else if (useGui)
        {
            AnsiConsole.MarkupLine("[yellow]GUI mode is no longer supported. Use console mode instead.[/]");
            AnsiConsole.WriteLine();
            var runner = new ConsoleTestRunner(scenario);
            runner.Run();
        }
    }
}

/// <summary>
/// Type registrar for Spectre.Console.Cli dependency injection
/// </summary>
public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceProvider _provider;

    public TypeRegistrar(IServiceProvider provider)
    {
        _provider = provider;
    }

    public ITypeResolver Build()
    {
        return new TypeResolver(_provider);
    }

    public void Register(Type service, Type implementation)
    {
        // Not needed - we use Microsoft.Extensions.DependencyInjection
    }

    public void RegisterInstance(Type service, object implementation)
    {
        // Not needed - we use Microsoft.Extensions.DependencyInjection
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        // Not needed - we use Microsoft.Extensions.DependencyInjection
    }
}

/// <summary>
/// Type resolver for Spectre.Console.Cli dependency injection
/// </summary>
public sealed class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider)
    {
        _provider = provider;
    }

    public object? Resolve(Type? type)
    {
        return type == null ? null : _provider.GetService(type);
    }
}
