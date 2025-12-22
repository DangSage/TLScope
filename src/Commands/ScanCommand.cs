using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TLScope.Services;

namespace TLScope.Commands;

/// <summary>
/// Command to perform active ICMP network scan to discover devices
/// </summary>
public class ScanCommand : AsyncCommand<ScanCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-s|--subnet")]
        [Description("Subnet to scan (e.g., 192.168.1). Auto-detects if not specified.")]
        public string? Subnet { get; set; }

        [CommandOption("--start")]
        [Description("Starting host number (default: 1)")]
        [DefaultValue(1)]
        public int StartHost { get; set; } = 1;

        [CommandOption("--end")]
        [Description("Ending host number (default: 254)")]
        [DefaultValue(254)]
        public int EndHost { get; set; } = 254;

        [CommandOption("-t|--timeout")]
        [Description("Ping timeout in milliseconds (default: 500)")]
        [DefaultValue(500)]
        public int Timeout { get; set; } = 500;

        [CommandOption("-c|--concurrency")]
        [Description("Maximum concurrent pings (default: 50)")]
        [DefaultValue(50)]
        public int Concurrency { get; set; } = 50;
    }

    private readonly IServiceProvider _serviceProvider;

    public ScanCommand(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        AnsiConsole.MarkupLine("[bold cyan]TLScope Network Scanner[/]");
        AnsiConsole.WriteLine();

        using var scope = _serviceProvider.CreateScope();

        var scanService = new NetworkScanService(settings.Timeout, settings.Concurrency);

        string? subnet;

        if (settings.Subnet != null)
        {
            subnet = settings.Subnet;
            AnsiConsole.MarkupLine($"[dim]Scanning subnet: {subnet}.{settings.StartHost}-{settings.EndHost}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]Auto-detecting local subnet...[/]");
            subnet = scanService.GetLocalSubnet();

            if (subnet == null)
            {
                AnsiConsole.MarkupLine("[red]Error: Unable to auto-detect subnet. Please specify with --subnet[/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"[dim]Detected subnet: {subnet}.0/24[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Timeout: {settings.Timeout}ms | Concurrency: {settings.Concurrency} pings[/]");
        AnsiConsole.WriteLine();

        var discovered = new List<(string ip, long rtt)>();

        scanService.DeviceResponded += (sender, e) =>
        {
            discovered.Add((e.ipAddress, e.reply.RoundtripTime));
            AnsiConsole.MarkupLine($"[green]✓[/] {e.ipAddress,-15} responded in [cyan]{e.reply.RoundtripTime}ms[/]");
        };

        var stopwatch = Stopwatch.StartNew();
        List<string> results;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Scanning {subnet}.{settings.StartHost}-{settings.EndHost}...", async ctx =>
            {
                results = await scanService.PingSweepAsync(subnet, settings.StartHost, settings.EndHost);
            });

        stopwatch.Stop();

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold yellow]Scan Results[/]"));
        AnsiConsole.WriteLine();

        if (discovered.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No devices found[/]");
        }
        else
        {
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("#");
            table.AddColumn("IP Address");
            table.AddColumn("Response Time");

            discovered = discovered.OrderBy(d => d.ip, new IPAddressComparer()).ToList();

            for (int i = 0; i < discovered.Count; i++)
            {
                var (ip, rtt) = discovered[i];
                var rttColor = rtt < 10 ? "green" : rtt < 50 ? "cyan" : "yellow";
                table.AddRow(
                    (i + 1).ToString(),
                    ip,
                    $"[{rttColor}]{rtt}ms[/]"
                );
            }

            AnsiConsole.Write(table);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Scanned: {settings.EndHost - settings.StartHost + 1} hosts | Found: {discovered.Count} | Duration: {stopwatch.Elapsed.TotalSeconds:F2}s[/]");

        return 0;
    }
}

/// <summary>
/// IP address comparer for sorting (handles proper numeric ordering)
/// </summary>
internal class IPAddressComparer : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if (x == null || y == null)
            return string.Compare(x, y);

        if (System.Net.IPAddress.TryParse(x, out var ipX) && System.Net.IPAddress.TryParse(y, out var ipY))
        {
            var bytesX = ipX.GetAddressBytes();
            var bytesY = ipY.GetAddressBytes();

            for (int i = 0; i < Math.Min(bytesX.Length, bytesY.Length); i++)
            {
                if (bytesX[i] != bytesY[i])
                    return bytesX[i].CompareTo(bytesY[i]);
            }

            return bytesX.Length.CompareTo(bytesY.Length);
        }

        return string.Compare(x, y);
    }
}
