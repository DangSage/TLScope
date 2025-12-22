using Spectre.Console.Cli;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using TLScope.Views;
using TLScope.Services;
using TLScope.Models;

namespace TLScope.Commands;

/// <summary>
/// Main command to start TLScope interactive interface
/// </summary>
public class StartCommand : AsyncCommand<StartCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-u|--username")]
        [Description("Username to login with")]
        public string? Username { get; set; }

        [CommandOption("-i|--interface")]
        [Description("Network interface to capture on")]
        public string? Interface { get; set; }

        [CommandOption("--no-capture")]
        [Description("Start without packet capture")]
        public bool NoCapture { get; set; }
    }

    private readonly IServiceProvider _serviceProvider;

    public StartCommand(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        User? currentUser;
        using (var scope = _serviceProvider.CreateScope())
        {
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();

            if (!string.IsNullOrEmpty(settings.Username))
            {
                currentUser = await userService.GetUserByUsername(settings.Username);
                if (currentUser == null)
                {
                    Spectre.Console.AnsiConsole.MarkupLine($"[red]User '{settings.Username}' not found.[/]");
                    return 1;
                }
            }
            else
            {
                currentUser = null;
            }
        }

        var app = _serviceProvider.GetRequiredService<MainApplication>();
        await app.Run(settings.Interface, currentUser, !settings.NoCapture);

        return 0;
    }
}
