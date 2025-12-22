using System.Net;
using Spectre.Console;
using Spectre.Console.Rendering;
using TLScope.Models;
using TLScope.Services;
using TLScope.Utilities;
using Serilog;

namespace TLScope.Views;

/// <summary>
/// Main Spectre.Console-based interactive application
/// </summary>
public class MainApplication : IDisposable
{
    private bool _disposed = false;
    private readonly IPacketCaptureService _captureService;
    private readonly IGraphService _graphService;
    private readonly UserService _userService;
    private readonly FilterConfiguration _filterConfig;
    private readonly DisplayConfiguration _displayConfig;
    private readonly IGatewayDetectionService _gatewayDetectionService;
    private ITlsPeerService? _peerService;

    private User? _currentUser;
    private bool _isRunning;
    private readonly List<string> _logMessages = new();
    private readonly object _logLock = new();
    private DateTime _sessionStartTime;
    private DateTime _lastCleanupTime = DateTime.UtcNow;
    private bool _dashboardNeedsRefresh = false;
    private readonly object _refreshLock = new();

    private int _lastWindowWidth = 0;
    private int _lastWindowHeight = 0;

    private const int WIDE_TERMINAL_THRESHOLD = 120;
    private enum LayoutMode { SingleColumn, TwoColumn }

    private ConsoleStateManager? _consoleState;

    private HashSet<string> _excludedIPs = new();
    private HashSet<string> _excludedHostnames = new();
    private HashSet<string> _excludedMACs = new();
    private static readonly string ExclusionsConfigFile = ConfigurationHelper.GetConfigFilePath("exclusions.json");

    private Device? _filteredDevice = null;
    private HashSet<string> _highlightedDeviceMACs = new();

    public MainApplication(
        IPacketCaptureService captureService,
        IGraphService graphService,
        UserService userService,
        FilterConfiguration filterConfig,
        IGatewayDetectionService gatewayDetectionService,
        ITlsPeerService? peerService = null)
    {
        _captureService = captureService;
        _graphService = graphService;
        _userService = userService;
        _filterConfig = filterConfig;
        _gatewayDetectionService = gatewayDetectionService;
        _displayConfig = DisplayConfiguration.Load();
        _peerService = peerService;

        _captureService.DeviceDiscovered += OnDeviceDiscovered;
        _captureService.ConnectionDetected += OnConnectionDetected;
        _captureService.LogMessage += OnLogMessage;

        _graphService.DeviceAdded += OnDeviceAdded;
        _graphService.ConnectionAdded += OnConnectionAdded;

        _gatewayDetectionService.GatewayDetected += OnGatewayDetected;
        _gatewayDetectionService.LogMessage += OnLogMessage;

        LoadExclusionsConfig();
    }

    public async Task Run(string? networkInterface = null, User? user = null, bool startCapture = true, ConsoleStateManager? consoleState = null)
    {
        _isRunning = true;
        _sessionStartTime = DateTime.Now;
        _consoleState = consoleState;

        try
        {
            if (user == null)
            {
                _currentUser = await PerformLogin();
                if (_currentUser == null)
                {
                    Console.WriteLine(AnsiColors.Colorize("Login failed or cancelled.", AnsiColors.Red));
                    return;
                }
            }
            else
            {
                _currentUser = user;
            }

            Console.WriteLine();
            Console.WriteLine($"Welcome back, {_currentUser.Username}!");
            Console.WriteLine(AnsiColors.Colorize($"Last login: {_currentUser.LastLogin:g}", AnsiColors.Dim));
            Console.WriteLine();

            if (_peerService == null)
            {
                _peerService = new TlsPeerService(_currentUser);
            }
            _peerService.PeerDiscovered += OnPeerDiscovered;
            _peerService.PeerConnected += OnPeerConnected;

            if (startCapture)
            {
                await StartServices(networkInterface);

                Console.WriteLine(AnsiColors.Colorize("✓ TLScope is ready!", AnsiColors.BrightGreen));
                Console.Write(AnsiColors.Colorize("Press Enter to continue...", AnsiColors.Dim));
                Console.ReadLine();
            }

            consoleState?.EnterAlternateBuffer();

            InitializeResizeDetection();

            await ShowMainMenu();
        }
        finally
        {
            Cleanup();
        }
    }

    private void ShowBanner()
    {
        AnsiConsole.MarkupLine("[bold]TLScope[/][dim] Network Security Visualization Tool[/]");
    }

    /// <summary>
    /// Initialize terminal resize detection
    private void InitializeResizeDetection()
    {
        try
        {
            _lastWindowWidth = Console.WindowWidth;
            _lastWindowHeight = Console.WindowHeight;
            Log.Debug($"Initialized resize detection: {_lastWindowWidth}x{_lastWindowHeight}");
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to initialize resize detection");
        }
    }

    /// <summary>
    /// Check if terminal was resized
    /// </summary>
    /// <returns>True if a resize was detected, false otherwise</returns>
    private bool CheckForResize()
    {
        try
        {
            var currentWidth = Console.WindowWidth;
            var currentHeight = Console.WindowHeight;

            if (currentWidth != _lastWindowWidth || currentHeight != _lastWindowHeight)
            {
                Log.Debug($"Terminal resized: {_lastWindowWidth}x{_lastWindowHeight} -> {currentWidth}x{currentHeight}");
                _lastWindowWidth = currentWidth;
                _lastWindowHeight = currentHeight;

                lock (_refreshLock)
                {
                    _dashboardNeedsRefresh = true;
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to check for terminal resize");
        }

        return false;
    }

    /// <summary>
    /// Determine layout mode based on terminal width
    /// </summary>
    /// <returns>LayoutMode.SingleColumn for narrow terminals, LayoutMode.TwoColumn for wide ones</returns>
    private LayoutMode GetLayoutMode()
    {
        try
        {
            return _lastWindowWidth >= WIDE_TERMINAL_THRESHOLD
                ? LayoutMode.TwoColumn
                : LayoutMode.SingleColumn;
        }
        catch
        {
            return LayoutMode.SingleColumn;
        }
    }

    /// <summary>
    /// Custom selection prompt with Escape support
    /// </summary>
    private string? PromptSelection(string title, List<string> choices, int pageSize = 20, int startIndex = 0, bool isRawAnsi = false, string[]? headerLines = null, string[]? mainHeaderLines = null)
    {
        if (choices.Count == 0)
            return null;

        int selectedIndex = startIndex;
        int scrollOffset = 0;
        bool done = false;
        string? result = null;

        if (selectedIndex >= choices.Count)
            selectedIndex = choices.Count - 1;

        while (!done)
        {
            AnsiConsole.Clear();

            if (mainHeaderLines != null && mainHeaderLines.Length > 0)
            {
                foreach (var mainHeaderLine in mainHeaderLines)
                {
                    Console.WriteLine(mainHeaderLine);
                }
                AnsiConsole.WriteLine();
            }

            if (!string.IsNullOrEmpty(title))
            {
                if (isRawAnsi)
                {
                    Console.WriteLine(title);
                }
                else
                {
                    AnsiConsole.MarkupLine($"[bold]{title}[/]");
                }
            }

            AnsiConsole.MarkupLine($"[dim]Use ↑↓ arrows to navigate, Enter to select, ESC or Q to go back[/]");
            AnsiConsole.WriteLine();

            if (headerLines != null && headerLines.Length > 0)
            {
                foreach (var headerLine in headerLines)
                {
                    Console.WriteLine(headerLine);
                }
                AnsiConsole.WriteLine();
            }

            int visibleCount = Math.Min(pageSize, choices.Count);
            if (selectedIndex < scrollOffset)
                scrollOffset = selectedIndex;
            if (selectedIndex >= scrollOffset + visibleCount)
                scrollOffset = selectedIndex - visibleCount + 1;

            int endIndex = Math.Min(scrollOffset + visibleCount, choices.Count);

            for (int i = scrollOffset; i < endIndex; i++)
            {
                if (i == selectedIndex)
                {
                    if (isRawAnsi)
                    {
                        Console.WriteLine($"\x1b[36m> {choices[i]}\x1b[0m");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[cyan]> {choices[i]}[/]");
                    }
                }
                else
                {
                    if (isRawAnsi)
                    {
                        Console.WriteLine($"  {choices[i]}");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"  {choices[i]}");
                    }
                }
            }

            if (choices.Count > visibleCount)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]Showing {scrollOffset + 1}-{endIndex} of {choices.Count}[/]");
            }

            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = selectedIndex > 0 ? selectedIndex - 1 : choices.Count - 1;
                    break;

                case ConsoleKey.DownArrow:
                    selectedIndex = selectedIndex < choices.Count - 1 ? selectedIndex + 1 : 0;
                    break;

                case ConsoleKey.PageUp:
                    selectedIndex = Math.Max(0, selectedIndex - pageSize);
                    break;

                case ConsoleKey.PageDown:
                    selectedIndex = Math.Min(choices.Count - 1, selectedIndex + pageSize);
                    break;

                case ConsoleKey.Home:
                    selectedIndex = 0;
                    break;

                case ConsoleKey.End:
                    selectedIndex = choices.Count - 1;
                    break;

                case ConsoleKey.Enter:
                    result = choices[selectedIndex];
                    done = true;
                    break;

                case ConsoleKey.Escape:
                case ConsoleKey.Q:
                    result = null;
                    done = true;
                    break;

                case ConsoleKey.D1:
                case ConsoleKey.D2:
                case ConsoleKey.D3:
                case ConsoleKey.D4:
                case ConsoleKey.D5:
                case ConsoleKey.D6:
                case ConsoleKey.D7:
                case ConsoleKey.D8:
                case ConsoleKey.D9:
                    int numIndex = (int)key.Key - (int)ConsoleKey.D1;
                    if (numIndex < choices.Count)
                    {
                        selectedIndex = numIndex;
                    }
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Wait for user to press Q, Escape, or any key to exit
    /// </summary>
    private void WaitForExit(string? message = null)
    {
        message ??= "[dim]Press any key to return..[/]";
        AnsiConsole.Markup(message);
        Console.ReadKey(true);
    }

    /// <summary>
    /// Loop until user presses Q or Escape
    /// </summary>
    private void WaitForExitLoop()
    {
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Q)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Async loop until user presses Q or Escape
    /// </summary>
    private async Task WaitForExitLoopAsync()
    {
        while (!Console.KeyAvailable || Console.ReadKey(true).Key != ConsoleKey.Q)
        {
            await Task.Delay(100);
        }
    }

    private async Task<User?> PerformLogin()
    {
        var allUsers = await _userService.GetAllUsers();

        if (allUsers.Count == 0)
        {
            Console.WriteLine(AnsiColors.Colorize("No users found. Let's create your first account.", AnsiColors.Yellow));
            Console.WriteLine();
            return await PerformRegistration(isFirstUser: true);
        }

        Console.WriteLine(AnsiColors.Colorize("User Login:", AnsiColors.Dim));

        for (int i = 0; i < allUsers.Count; i++)
        {
            Console.WriteLine($"  {AnsiColors.Colorize((i + 1).ToString(), AnsiColors.Dim)}. {allUsers[i].Username}");
        }
        Console.WriteLine($"  {AnsiColors.Colorize((allUsers.Count + 1).ToString(), AnsiColors.Dim)}. {AnsiColors.Colorize("Create New Account", AnsiColors.Dim)}");

        int choice = -1;
        while (choice < 1 || choice > allUsers.Count + 1)
        {
            Console.Write("Select option> ");
            var input = Console.ReadLine();
            if (!int.TryParse(input, out choice) || choice < 1 || choice > allUsers.Count + 1)
            {
                Console.WriteLine(AnsiColors.Colorize("Invalid choice. Please try again.", AnsiColors.Red));
                choice = -1;
            }
        }

        if (choice == allUsers.Count + 1)
        {
            Console.WriteLine();
            return await PerformRegistration(isFirstUser: false);
        }

        var selectedUser = allUsers[choice - 1];
        var existingUser = await _userService.GetUserByUsername(selectedUser.Username);

        return existingUser;
    }

    private async Task<User?> PerformRegistration(bool isFirstUser, string? username = null)
    {
        if (isFirstUser)
        {
            Console.WriteLine(AnsiColors.Colorize("First Time Setup", AnsiColors.Green));
            Console.WriteLine(AnsiColors.Colorize("----------------------", AnsiColors.Green));
        }
        else
        {
            Console.WriteLine(AnsiColors.Colorize("Create New Account", AnsiColors.Green));
            Console.WriteLine(AnsiColors.Colorize("----------------------", AnsiColors.Green));
        }

        if (string.IsNullOrEmpty(username))
        {
            while (string.IsNullOrEmpty(username))
            {
                Console.Write(AnsiColors.Colorize("Enter username", AnsiColors.Cyan) + ": ");
                username = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(username))
                {
                    Console.WriteLine(AnsiColors.Colorize("Username cannot be empty.", AnsiColors.Red));
                    continue;
                }

                var existing = await _userService.GetUserByUsername(username);
                if (existing != null)
                {
                    Console.WriteLine(AnsiColors.Colorize($"Username '{username}' is already taken.", AnsiColors.Red));
                    username = null;
                }
            }
        }

        Console.Write(AnsiColors.Colorize("Enter email", AnsiColors.Cyan) + AnsiColors.Colorize(" (optional, press Enter to skip)", AnsiColors.Dim) + ": ");
        var email = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            email = null;
        }

        Console.WriteLine();
        Console.WriteLine(AnsiColors.Colorize("SSH Key Setup (optional):", AnsiColors.Cyan));
        Console.WriteLine(AnsiColors.Colorize("  • Used for TLS peer authentication", AnsiColors.Dim));
        Console.WriteLine(AnsiColors.Colorize("  • Generates unique avatar color", AnsiColors.Dim));
        Console.WriteLine(AnsiColors.Colorize("  • Example: ~/.ssh/id_rsa", AnsiColors.Dim));
        Console.WriteLine();
        Console.Write(AnsiColors.Colorize("SSH private key path", AnsiColors.Cyan) + AnsiColors.Colorize(" (press Enter to skip)", AnsiColors.Dim) + ": ");
        var sshKeyPath = Console.ReadLine()?.Trim();

        if (!string.IsNullOrEmpty(sshKeyPath))
        {
            if (sshKeyPath.StartsWith("~/"))
            {
                sshKeyPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    sshKeyPath.Substring(2)
                );
            }

            if (!File.Exists(sshKeyPath))
            {
                Console.WriteLine(AnsiColors.Colorize($"Warning: SSH key file not found at '{sshKeyPath}'", AnsiColors.Yellow));
                Console.WriteLine(AnsiColors.Colorize("Continuing without SSH key authentication...", AnsiColors.Yellow));
                sshKeyPath = null;
            }
        }
        else
        {
            sshKeyPath = null;
        }

        Console.WriteLine();
        Console.WriteLine(AnsiColors.Colorize("Creating account...", AnsiColors.Dim));

        var newUser = await _userService.AuthenticateOrCreateUser(username, email, sshKeyPath);

        if (newUser != null)
        {
            Console.WriteLine();
            Console.WriteLine(AnsiColors.Colorize("Registration Successful:", AnsiColors.Green));
            Console.WriteLine(AnsiColors.Colorize("✓ Account created successfully!", AnsiColors.Green));
            Console.WriteLine();
            Console.WriteLine($"{AnsiColors.Colorize("Username:", AnsiColors.Cyan)} {newUser.Username}");
            Console.WriteLine($"{AnsiColors.Colorize("Email:", AnsiColors.Cyan)} {newUser.Email ?? AnsiColors.Colorize("Not provided", AnsiColors.Dim)}");
            Console.WriteLine($"{AnsiColors.Colorize("Avatar Color:", AnsiColors.Cyan)} {newUser.AvatarColor}");
            var sshStatus = !string.IsNullOrEmpty(newUser.SSHPublicKey) && !newUser.SSHPublicKey.StartsWith("default-")
                ? AnsiColors.Colorize("Loaded", AnsiColors.Green)
                : AnsiColors.Colorize("Not configured", AnsiColors.Dim);
            Console.WriteLine($"{AnsiColors.Colorize("SSH Key:", AnsiColors.Cyan)} {sshStatus}");
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine(AnsiColors.Colorize("Failed to create account.", AnsiColors.Red));
        }

        return newUser;
    }

    private async Task StartServices(string? networkInterface)
    {
        Console.WriteLine(AnsiColors.Colorize("Starting services...", AnsiColors.Dim));

        try
        {
            await _graphService.LoadDevicesFromDatabaseAsync();
            var deviceCount = _graphService.GetAllDevices().Count;
            if (deviceCount > 0)
            {
                Console.WriteLine(AnsiColors.Colorize($"✓ Loaded {deviceCount} devices from database", AnsiColors.Green));
                AddLogMessage($"✓ Loaded {deviceCount} devices from database");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load devices from database");
            Console.WriteLine(AnsiColors.Colorize("⚠ Failed to load devices from database", AnsiColors.Yellow));
        }

        try
        {
            if (!string.IsNullOrEmpty(networkInterface))
            {
                _captureService.StartCapture(networkInterface);
            }
            else
            {
                _captureService.StartCapture();
            }
            Console.WriteLine(AnsiColors.Colorize("✓ Packet capture started successfully", AnsiColors.Green));
            AddLogMessage("✓ Packet capture started successfully");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to start packet capture");
            Console.WriteLine(AnsiColors.Colorize("⚠ Packet capture failed - run with sudo for full functionality", AnsiColors.Yellow));
            AddLogMessage("⚠ Packet capture failed - run with sudo for full functionality");
        }

        await Task.Delay(500);

        try
        {
            var gateway = _gatewayDetectionService.DetectDefaultGateway();
            if (gateway != null)
            {
                Console.WriteLine(AnsiColors.Colorize($"✓ Detected default gateway IP: {gateway}", AnsiColors.Green));
                AddLogMessage($"✓ Default gateway IP: {gateway}");

                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000);

                    for (int attempt = 0; attempt < 12; attempt++)  // Try for up to 1 minute
                    {
                        var devices = _graphService.GetAllDevices();
                        var gatewayDevices = _gatewayDetectionService.IdentifyGatewayDevices(devices);

                        if (gatewayDevices.Any())
                        {
                            var defaultGw = gatewayDevices.FirstOrDefault(g => g.IsDefaultGateway);
                            if (defaultGw != null)
                            {
                                Log.Information($"[GATEWAY] Gateway device identified: {defaultGw.IPAddress} ({defaultGw.MACAddress})");
                                AddLogMessage($"✓ Gateway device found: {defaultGw.IPAddress}");
                                break;
                            }
                        }

                        await Task.Delay(5000);  // Check every 5 seconds
                    }
                });
            }
            else
            {
                Console.WriteLine(AnsiColors.Colorize("ℹ️  Could not detect default gateway", AnsiColors.Cyan));
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to detect gateway");
            Console.WriteLine(AnsiColors.Colorize("⚠ Gateway detection failed", AnsiColors.Yellow));
        }

        try
        {
            _peerService?.Start();
            Console.WriteLine(AnsiColors.Colorize("✓ TLS peer service started successfully", AnsiColors.Green));
            AddLogMessage("✓ TLS peer service started successfully");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to start TLS peer service");
            Console.WriteLine(AnsiColors.Colorize("⚠ TLS peer service failed to start", AnsiColors.Yellow));
            AddLogMessage("⚠ TLS peer service failed to start");
        }

        await Task.Delay(500);
        Console.WriteLine(AnsiColors.Colorize("Services started!", AnsiColors.Green));
    }

    private async Task ShowMainMenu()
    {
        while (_isRunning)
        {
            RenderDashboard();

            var input = await ReadInputWithRefresh();

            if (!string.IsNullOrEmpty(input))
            {
                var (shouldContinue, waitForKey) = await ExecuteCommand(input);

                if (!shouldContinue)
                {
                    break;
                }

                if (_isRunning && waitForKey)
                {
                    Console.WriteLine();
                    WaitForExit("[dim]Press any key to return to menu..[/]");
                }
            }
        }
    }

    private void RenderDashboard()
    {
        Console.Clear();

        AnsiConsole.MarkupLine("[bold]TLScope[/][dim] Network Security Visualization Tool[/]"); 
        AnsiConsole.MarkupLine("[dim]Version 1.0.0 Created by Ethan Dang[/]");
        AnsiConsole.MarkupLine("");
        var combinedTable = RenderCombinedInfoTable();
        AnsiConsole.Write(combinedTable);

        var devices = _graphService.GetAllDevices();
        var connections = _graphService.GetAllConnections();
        var activeDevices = devices.Count(d => d.IsActive);
        var isCapturing = _captureService.IsCapturing();

        var statusMarkup = isCapturing ? "[green]Capturing[/]" : "[yellow on black]Stopped[/]";
        AnsiConsole.MarkupLine($"[dim]Status: [/]{statusMarkup}[dim] | Devices: [/][cyan]{activeDevices}/{devices.Count}[/][dim] | Connections: [/][cyan on black]{connections.Count}[/]");

        AnsiConsole.MarkupLine("[dim][cyan]d[/]-Devices  [cyan]c[/]-Connections  [cyan]g[/]-Graph  [cyan]n[/]-Scan  [cyan]s[/]-Statistics  [cyan]h[/]-Help  [cyan]q[/]-Quit[/]");

        if (_filteredDevice != null || _highlightedDeviceMACs.Any())
        {
            var filterStatus = "";
            if (_filteredDevice != null)
            {
                filterStatus += $"[yellow]⚠ Filtered to: {_filteredDevice.IPAddress}[/]";
            }
            if (_highlightedDeviceMACs.Any())
            {
                if (filterStatus.Length > 0) filterStatus += " | ";
                filterStatus += $"[yellow]★ {_highlightedDeviceMACs.Count} highlighted[/]";
            }
            filterStatus += " [dim](Type 'clear')[/]";
            AnsiConsole.MarkupLine(filterStatus);
        }

        AnsiConsole.Markup("> ");
    }

    private async Task<string> ReadInputWithRefresh()
    {
        var inputBuffer = new System.Text.StringBuilder();

        while (true)
        {
            CheckForResize();

            if ((DateTime.UtcNow - _lastCleanupTime).TotalSeconds >= 30)
            {
                if (_filterConfig.AutoRemoveInactiveDevices)
                {
                    _graphService.CleanupInactiveDevices();
                }

                _graphService.ResetConnectionRates();

                try
                {
                    var devices = _graphService.GetAllDevices();
                    _gatewayDetectionService.RefreshGateways(devices);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Gateway refresh failed during cleanup cycle");
                }

                _lastCleanupTime = DateTime.UtcNow;
            }

            bool needsRefresh = false;
            lock (_refreshLock)
            {
                needsRefresh = _dashboardNeedsRefresh;
                if (needsRefresh)
                {
                    _dashboardNeedsRefresh = false;
                }
            }

            if (needsRefresh && inputBuffer.Length == 0)
            {
                RenderDashboard();
            }

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine(); // Move to next line
                    return inputBuffer.ToString().ToLower().Trim();
                }
                else if (key.Key == ConsoleKey.Backspace && inputBuffer.Length > 0)
                {
                    inputBuffer.Length--;
                    Console.Write("\b \b"); // Erase character
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    inputBuffer.Append(key.KeyChar);
                    Console.Write(key.KeyChar);
                }
            }
            else
            {
                await Task.Delay(100);
            }
        }
    }

    private async Task<(bool shouldContinue, bool waitForKey)> ExecuteCommand(string input)
    {
        switch (input)
        {
            case "d":
            case "devices":
                ShowDevicesTable();
                return (true, true);
            case "p":
            case "peers":
                ShowPeersTable();
                return (true, true);
            case "c":
            case "connections":
                ShowConnectionsTable();
                return (true, true);
            case "a":
            case "avatar":
                await CustomizeAvatar();
                return (true, true);
            case "i":
            case "interface":
                await ManageNetworkInterface();
                return (true, true);
            case "f":
            case "filters":
            case "filter":
                ShowIPFilterSettings();
                return (true, true);
            case "x":
            case "exclusions":
            case "exclude":
                ManageExclusions();
                return (true, true);
            case "e":
            case "export":
                await ExportGraph();
                return (true, true);
            case "l":
            case "logs":
                await ShowLogs();
                return (true, true);
            case "g":
            case "graph":
            case "network":
                ShowFullscreenNetworkGraph();
                return (true, true);
            case "n":
            case "scan":
                await TriggerNetworkScan();
                return (true, true);
            case "s":
            case "stats":
            case "statistics":
                ShowStatistics();
                return (true, true);
            case "h":
            case "help":
            case "?":
                ShowHelp();
                return (true, true);
            case "q":
            case "quit":
            case "exit":
                ExitApplication();
                return (false, false); // Exit immediately without waiting
            case "clear":
                ClearFilters();
                return (true, true);
            case "":
                return (true, false); // Don't wait, just refresh
            default:
                AnsiConsole.MarkupLine($"[red]Unknown command: '{input}'. Press 'h' for help.[/]");
                return (true, true); // Don't wait for key on error
        }
    }

    private void ShowHelp()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]TLScope[/][dim] Network Security Visualization Tool[/]");
        AnsiConsole.Write(new Rule($"[white]TLScope Command Reference[/]").RuleStyle(Style.Parse("grey37")));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold cyan]Data Views[/]");
        var viewsTable = new Table()
            .Border(TableBorder.Markdown)
            .BorderColor(Color.Cyan1)
            .HideHeaders()
            .AddColumn($"[cyan]Shortcut[/]")
            .AddColumn($"[cyan]Full Command[/]")
            .AddColumn($"[cyan]Description[/]");

        viewsTable.AddRow($"[cyan]d[/]", "devices", "View all discovered network devices");
        viewsTable.AddRow($"[cyan]p[/]", "peers", "View TLS peers (other TLScope clients)");
        viewsTable.AddRow($"[cyan]c[/]", "connections", "View network connections between devices");
        viewsTable.AddRow($"[cyan]g[/]", "graph, network", "View fullscreen network topology graph");
        viewsTable.AddRow($"[cyan]s[/]", "stats, statistics", "Show network statistics and topology analysis");
        viewsTable.AddRow($"[cyan]l[/]", "logs", "View activity logs and packet events");

        AnsiConsole.Write(viewsTable);

        AnsiConsole.MarkupLine("[bold yellow]Network Management[/]");
        var mgmtTable = new Table()
            .Border(TableBorder.Markdown)
            .BorderColor(Color.Yellow)
            .HideHeaders()
            .AddColumn($"[yellow]Shortcut[/]")
            .AddColumn($"[yellow]Full Command[/]")
            .AddColumn($"[yellow]Description[/]");

        mgmtTable.AddRow($"[yellow]i[/]", "interface", "Manage network capture interface");
        mgmtTable.AddRow($"[yellow]n[/]", "scan", "Run active ICMP network scan");
        mgmtTable.AddRow($"[yellow]f[/]", "filters, filter", "Manage IP filter settings");
        mgmtTable.AddRow($"[yellow]x[/]", "exclusions, exclude", "Manage excluded IPs/hostnames/MACs");

        AnsiConsole.Write(mgmtTable);

        AnsiConsole.MarkupLine("[bold white]Customization & Export[/]");
        var customTable = new Table()
            .Border(TableBorder.Markdown)
            .BorderColor(Color.Grey37)
            .HideHeaders()
            .AddColumn($"[default]Shortcut[/]")
            .AddColumn($"[default]Full Command[/]")
            .AddColumn($"[default]Description[/]");

        customTable.AddRow($"[default]a[/]", "avatar", "Customize your avatar appearance");
        customTable.AddRow($"[default]e[/]", "export", "Export network graph to DOT/LaTeX format");

        AnsiConsole.Write(customTable);

        AnsiConsole.MarkupLine("[bold white]System[/]");
        var systemTable = new Table()
            .Border(TableBorder.Markdown)
            .BorderColor(Color.Grey37)
            .HideHeaders()
            .AddColumn($"[default]Shortcut[/]")
            .AddColumn($"[default]Full Command[/]")
            .AddColumn($"[default]Description[/]");

        systemTable.AddRow($"[default]h[/]", "help, ?", "Show this help message");
        systemTable.AddRow($"[default]q[/]", "quit, exit", "Exit TLScope");

        AnsiConsole.Write(systemTable);
        AnsiConsole.WriteLine();

        Console.WriteLine();
        AnsiConsole.Write(new Rule("[white]Quick Tips[/]").RuleStyle(Style.Parse("grey37")));
        AnsiConsole.MarkupLine("[dim]Tips:[/]");
        AnsiConsole.MarkupLine("  • Type the shortcut letter for quick access (e.g., 'd' for devices)");
        AnsiConsole.MarkupLine("  • Or type the full command name (e.g., 'devices')");
        AnsiConsole.MarkupLine("  • Press [cyan]ESC or Q[/] in menus to go back/cancel");
        AnsiConsole.MarkupLine("  • Press [cyan]ESC or Q[/] in graph views to return to dashboard");
        AnsiConsole.MarkupLine("  • Use [cyan]arrow keys[/] in selectors, [cyan]Enter[/] to confirm");
        AnsiConsole.MarkupLine("  • Commands are case-insensitive");
        Console.WriteLine();
    }

    private void ShowDevicesTable()
    {
        bool viewing = true;

        while (viewing)
        {
            RenderViewHeader("Network Devices");

            var devices = _graphService.GetAllDevices();

            if (devices.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No devices discovered yet.[/]");
                return;
            }

            var allConnections = _graphService.GetAllConnections();

            var grouped = GroupDevicesByType(devices, allConnections);
            var gateways = grouped.Gateways;
            var peers = grouped.Peers;
            var activeLocal = grouped.ActiveLocal;
            var activeRemote = grouped.ActiveRemote;
            var inactive = grouped.Inactive;

            var allDevicesOrdered = gateways.Concat(peers).Concat(activeLocal).Concat(activeRemote).Concat(inactive).ToList();

            var table = new Table()
                .Border(TableBorder.Markdown)
                .BorderColor(Color.Grey37)
                .ShowRowSeparators()
                .AddColumn(new TableColumn($"[bold white]Status[/]").Centered())
                .AddColumn($"[bold white]Type[/]")
                .AddColumn($"[bold white]Device Name / IP[/]")
                .AddColumn($"[bold white]MAC Address[/]")
                .AddColumn($"[bold white]Hostname[/]")
                .AddColumn(new TableColumn($"[bold white]Connections[/]").RightAligned())
                .AddColumn(new TableColumn($"[bold white]Packets[/]").RightAligned())
                .AddColumn(new TableColumn($"[bold white]Bytes[/]").RightAligned())
                .AddColumn($"[bold white]Last Seen[/]");

            foreach (var device in allDevicesOrdered)
            {
                var status = GetDeviceStatus(device, allConnections);

                var deviceType = GetDeviceTypeLabel(device);

                var deviceName = GetDeviceName(device);

                var hostname = device.Hostname ?? "[dim]Unknown[/]";

                var connectionCount = allConnections.Count(c =>
                    c.SourceDevice.MACAddress == device.MACAddress ||
                    c.DestinationDevice.MACAddress == device.MACAddress);

                var lastSeen = FormatRelativeTime(device.LastSeen);

                table.AddRow(
                    status,
                    deviceType,
                    Markup.Escape(deviceName),
                    device.MACAddress,
                    hostname,
                    connectionCount.ToString(),
                    device.PacketCount.ToString("N0"),
                    FormatBytes(device.BytesTransferred),
                    lastSeen
                );
            }

            string renderedTable = RenderToString(table);

            var mainHeader = new List<string>();
            mainHeader.Add(RenderToString(new Markup("[bold]TLScope[/][dim] Network Security Visualization Tool[/]")).TrimEnd());
            mainHeader.Add(RenderToString(new Rule($"[white]Network Devices[/]").RuleStyle(Style.Parse("grey37"))).TrimEnd());

            var tableLines = renderedTable.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            var allHeaderLines = tableLines.Take(3).ToArray();

            var titleLine = allHeaderLines.Length > 0 ? allHeaderLines[0] : "";
            var remainingHeaders = allHeaderLines.Length > 1 ? allHeaderLines.Skip(1).ToArray() : Array.Empty<string>();

            var dataRows = tableLines.Skip(3).ToList();

            var orderedDevices = new List<Device>();
            orderedDevices.AddRange(gateways);
            orderedDevices.AddRange(peers);
            orderedDevices.AddRange(activeLocal);
            orderedDevices.AddRange(activeRemote);
            orderedDevices.AddRange(inactive);

            var selectableRows = new List<string>();
            var rowToDeviceMap = new Dictionary<string, Device>();
            int deviceIndex = 0;

            foreach (var row in dataRows)
            {
                var cleanRow = System.Text.RegularExpressions.Regex.Replace(row, @"\x1b\[[0-9;]*m", "");

                if (string.IsNullOrWhiteSpace(cleanRow))
                {
                    continue;
                }

                if (cleanRow.All(c => "─│├┤┬┴┼╭╮╰╯═║╔╗╚╝╠╣╦╩╬ :|+-".Contains(c)))
                {
                    continue;
                }

                if (cleanRow.Contains(":--:") || cleanRow.Contains("---"))
                {
                    continue;
                }

                if (row.Contains("═══") || row.Contains("Gateways") || row.Contains("TLS Peers") ||
                    row.Contains("Active Local") || row.Contains("Active Remote") || row.Contains("Inactive"))
                {
                    continue;
                }

                if (deviceIndex < orderedDevices.Count)
                {
                    selectableRows.Add(row);
                    rowToDeviceMap[row] = orderedDevices[deviceIndex];
                    deviceIndex++;
                }
            }

            var selectedRow = PromptSelection(
                titleLine,
                selectableRows,
                pageSize: Console.WindowHeight - 15,
                isRawAnsi: true,
                headerLines: remainingHeaders,
                mainHeaderLines: mainHeader.ToArray()
            );

            if (selectedRow != null && rowToDeviceMap.ContainsKey(selectedRow))
            {
                ShowDeviceDetailsComprehensive(rowToDeviceMap[selectedRow]);
            }
            else
            {
                viewing = false;
            }
        }
    }

    /// <summary>
    /// Render device details with all sections
    /// </summary>
    private void RenderDeviceDetailsContent(Device device)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]TLScope[/][dim] Network Security Visualization Tool[/]");
        AnsiConsole.Write(new Rule($"[white]Device Details: {device.DeviceName ?? device.IPAddress}[/]").RuleStyle(Style.Parse("grey37")));
        AnsiConsole.WriteLine();

        Avatar? avatar = null;
        string avatarColor = "#FFFFFF";
        string avatarType = "Unknown";

        if (device.TLSPeer != null)
        {
            avatar = AvatarUtility.GetAvatar(device.TLSPeer.AvatarType);
            avatarColor = device.TLSPeer.AvatarColor;
            avatarType = device.TLSPeer.AvatarType;
        }
        else
        {
            var avatarIndex = Math.Abs(device.MACAddress.GetHashCode()) % AvatarUtility.GetAvatarEnums().Count;
            var avatarEnum = AvatarUtility.GetAvatarEnums()[avatarIndex];
            avatar = AvatarUtility.GetAvatar(avatarEnum);
            avatarColor = AvatarUtility.GenerateColorFromSSHKey(device.MACAddress);
            avatarType = avatarEnum;
        }

        var basicGrid = new Grid();
        basicGrid.AddColumn();
        basicGrid.AddColumn();

        basicGrid.AddRow(new Markup("[bold]Device Name:[/]"), new Markup(Markup.Escape(device.DeviceName ?? "Unknown")));
        basicGrid.AddRow(new Markup("[bold]IP Address:[/]"), new Markup(Markup.Escape(device.IPAddress)));
        basicGrid.AddRow(new Markup("[bold]MAC Address:[/]"), new Markup(Markup.Escape(device.MACAddress)));
        basicGrid.AddRow(new Markup("[bold]Hostname:[/]"), new Markup(Markup.Escape(device.Hostname ?? "Unknown")));
        basicGrid.AddRow(new Markup("[bold]OS:[/]"), new Markup(Markup.Escape(device.OperatingSystem ?? "Unknown")));
        basicGrid.AddRow(new Markup("[bold]Vendor:[/]"), new Markup(Markup.Escape(device.Vendor ?? "Unknown")));

        AnsiConsole.Write(new Panel(basicGrid).Header("[cyan]Basic Information[/]").Border(BoxBorder.Ascii));
        AnsiConsole.WriteLine();

        var networkGrid = new Grid();
        networkGrid.AddColumn();
        networkGrid.AddColumn();

        var locationIcon = device.IsLocal ? "[cyan]●[/]" : "[orange1]◍[/]";
        var locationType = device.IsLocal ? (device.IsVirtualDevice ? "Virtual (Remote host)" : "Local Network") : "Remote/Internet";
        networkGrid.AddRow(new Markup("[bold]Location:[/]"), new Markup($"{locationIcon} {locationType}"));

        if (device.IsGateway)
        {
            var gatewayIcon = device.IsDefaultGateway ? "[yellow]◆[/]" : "[yellow]◇[/]";
            var gatewayLabel = device.IsDefaultGateway ? "Default Gateway" : "Gateway";
            networkGrid.AddRow(new Markup("[bold]Gateway Status:[/]"), new Markup($"{gatewayIcon} {gatewayLabel}"));

            if (!string.IsNullOrEmpty(device.GatewayRole))
            {
                networkGrid.AddRow(new Markup("[bold]Gateway Role:[/]"), new Markup(Markup.Escape(device.GatewayRole)));
            }
        }

        if (device.HopCount.HasValue)
        {
            networkGrid.AddRow(new Markup("[bold]Hop Count:[/]"), new Markup($"{device.HopCount} hop(s) from local device"));
        }

        if (device.IsTLScopePeer)
        {
            networkGrid.AddRow(new Markup("[bold]TLScope Peer:[/]"), new Markup("[green]◉ Yes[/]"));
        }

        AnsiConsole.Write(new Panel(networkGrid).Header("[yellow]Network Classification[/]").Border(BoxBorder.Ascii));
        AnsiConsole.WriteLine();

        var timelineGrid = new Grid();
        timelineGrid.AddColumn();
        timelineGrid.AddColumn();

        timelineGrid.AddRow(new Markup("[bold]First Seen:[/]"), new Markup(FormatRelativeTime(device.FirstSeen)));
        timelineGrid.AddRow(new Markup("[bold]Last Seen:[/]"), new Markup(FormatRelativeTime(device.LastSeen)));

        var timeActive = DateTime.UtcNow - device.FirstSeen;
        var timeActiveStr = timeActive.TotalDays >= 1
            ? $"{timeActive.TotalDays:F1} days"
            : timeActive.TotalHours >= 1
                ? $"{timeActive.TotalHours:F1} hours"
                : $"{timeActive.TotalMinutes:F0} minutes";
        timelineGrid.AddRow(new Markup("[bold]Time Active:[/]"), new Markup(timeActiveStr));

        var statusMarkup = device.IsActive ? "[green]✓ Active[/]" : "[grey37]✗ Inactive[/]";
        timelineGrid.AddRow(new Markup("[bold]Status:[/]"), new Markup(statusMarkup));

        AnsiConsole.Write(new Panel(timelineGrid).Header("[white]Timeline[/]").Border(BoxBorder.Ascii));
        AnsiConsole.WriteLine();

        var trafficGrid = new Grid();
        trafficGrid.AddColumn();
        trafficGrid.AddColumn();

        trafficGrid.AddRow(new Markup("[bold]Total Packets:[/]"), new Markup($"{device.PacketCount:N0}"));
        trafficGrid.AddRow(new Markup("[bold]Total Bytes:[/]"), new Markup(FormatBytes(device.BytesTransferred)));
        trafficGrid.AddRow(new Markup("[bold]Open Ports:[/]"), new Markup(device.OpenPorts.Any() ? string.Join(", ", device.OpenPorts) : "None detected"));

        AnsiConsole.Write(new Panel(trafficGrid).Header("[cyan]Traffic Statistics[/]").Border(BoxBorder.Ascii));
        AnsiConsole.WriteLine();

        if (device.TLSPeer != null)
        {
            var peerGrid = new Grid();
            peerGrid.AddColumn();
            peerGrid.AddColumn();

            peerGrid.AddRow(new Markup("[bold]Username:[/]"), new Markup(Markup.Escape(device.TLSPeer.Username)));
            peerGrid.AddRow(new Markup("[bold]Avatar Type:[/]"), new Markup(Markup.Escape(avatarType)));
            peerGrid.AddRow(new Markup("[bold]Avatar Color:[/]"), new Markup(Markup.Escape(avatarColor)));
            peerGrid.AddRow(new Markup("[bold]Port:[/]"), new Markup($"{device.TLSPeer.Port}"));
            peerGrid.AddRow(new Markup("[bold]Connected:[/]"), new Markup(device.TLSPeer.IsConnected ? "[green]Yes[/]" : "[dim]No[/]"));

            if (device.TLSPeer.IsVerified)
            {
                peerGrid.AddRow(new Markup("[bold]Verified:[/]"), new Markup($"[green]✓ Yes[/] (at {device.TLSPeer.LastVerified:g})"));
            }

            if (!string.IsNullOrEmpty(device.TLSPeer.Version))
            {
                peerGrid.AddRow(new Markup("[bold]TLScope Version:[/]"), new Markup(Markup.Escape(device.TLSPeer.Version)));
            }

            if (device.TLSPeer.DeviceCount > 0)
            {
                peerGrid.AddRow(new Markup("[bold]Peer's Device Count:[/]"), new Markup($"{device.TLSPeer.DeviceCount}"));
            }

            if (!string.IsNullOrEmpty(device.TLSPeer.SSHPublicKey))
            {
                var key = device.TLSPeer.SSHPublicKey;
                var fingerprint = key.Length > 48
                    ? $"{key.Substring(0, 16)}...{key.Substring(key.Length - 16)}"
                    : key;
                peerGrid.AddRow(new Markup("[bold]SSH Key:[/]"), new Markup($"[dim]{Markup.Escape(fingerprint)}[/]"));
            }

            AnsiConsole.Write(new Panel(peerGrid).Header("[green]TLS Peer Extended Information[/]").Border(BoxBorder.Ascii));
            AnsiConsole.WriteLine();
        }

        AnsiConsole.WriteLine();
        WaitForExit("[dim]Press any key to continue..[/]");
    }

    /// <summary>
    /// Show device action menu
    /// </summary>
    private void ShowDeviceDetailsComprehensive(Device device)
    {
        bool viewing = true;

        while (viewing)
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[bold]TLScope[/][dim] Network Security Visualization Tool[/]");
            AnsiConsole.Write(new Rule($"[white]Device Actions: {device.DeviceName ?? device.IPAddress}[/]").RuleStyle(Style.Parse("grey37")));
            AnsiConsole.WriteLine();

            var summaryGrid = new Grid();
            summaryGrid.AddColumn();
            summaryGrid.AddColumn();
            summaryGrid.AddRow(new Markup("[bold]IP Address:[/]"), new Markup(Markup.Escape(device.IPAddress)));
            summaryGrid.AddRow(new Markup("[bold]MAC Address:[/]"), new Markup(Markup.Escape(device.MACAddress)));
            summaryGrid.AddRow(new Markup("[bold]Device Name:[/]"), new Markup(Markup.Escape(device.DeviceName ?? "Unknown")));
            summaryGrid.AddRow(new Markup("[bold]Status:[/]"), new Markup(device.IsActive ? "[green]✓ Active[/]" : "[grey37]✗ Inactive[/]"));
            AnsiConsole.Write(new Panel(summaryGrid).Header("[cyan]Device Summary[/]").Border(BoxBorder.Ascii));
            AnsiConsole.WriteLine();

            var actions = new List<string>();
            actions.Add("View Comprehensive Details");
            actions.Add("Filter Network View (show only this device's connections)");
            actions.Add("Highlight Device Connections");
            actions.Add("[yellow]« Back to Device List[/]");

            var action = PromptSelection("[bold]Select an action:[/]", actions, pageSize: 10);

            if (action == null || action.Contains("Back"))
            {
                viewing = false;
            }
            else if (action.Contains("View Comprehensive Details"))
            {
                RenderDeviceDetailsContent(device);
            }
            else if (action.Contains("Filter"))
            {
                FilterByDevice(device);
                viewing = false;
            }
            else if (action.Contains("Highlight"))
            {
                HighlightDevice(device);
                viewing = false;
            }
        }
    }

    private void ShowInteractiveDeviceList(List<Device> devices)
    {
        bool viewing = true;

        while (viewing)
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[bold]TLScope[/][dim] Network Security Visualization Tool[/]");
            AnsiConsole.Write(new Rule($"[white]Interactive Device Selection[/]").RuleStyle(Style.Parse("grey37")));
            AnsiConsole.WriteLine();

            var allConnections = _graphService.GetAllConnections();
            var grouped = GroupDevicesByType(devices, allConnections);
            var gateways = grouped.Gateways;
            var peers = grouped.Peers;
            var active = grouped.ActiveLocal.Concat(grouped.ActiveRemote).OrderByDescending(d => d.LastSeen).ToList();
            var inactive = grouped.Inactive;

            var allDevicesOrdered = gateways.Concat(peers).Concat(active).Concat(inactive).ToList();

            if (allDevicesOrdered.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No devices found.[/]");
                return;
            }

            var deviceChoices = new List<string>();
            int index = 1;

            if (gateways.Any())
            {
                deviceChoices.Add($"[bold yellow]═══ Gateways ({gateways.Count}) ═══[/]");
                foreach (var device in gateways)
                {
                    var name = GetDeviceName(device);
                    var icon = device.IsDefaultGateway ? "◆" : "◇";
                    var label = device.IsDefaultGateway ? "Default Gateway" : "Gateway";
                    deviceChoices.Add($"  [[{index}]] [yellow]{icon}[/] {Markup.Escape(name)} [dim]({label}) ({Markup.Escape(device.IPAddress)}) - {FormatNumber(device.PacketCount)} packets[/]");
                    index++;
                }
            }

            if (peers.Any())
            {
                deviceChoices.Add($"[bold green]═══ TLS Peers ({peers.Count}) ═══[/]");
                foreach (var device in peers)
                {
                    var name = GetDeviceName(device);
                    deviceChoices.Add($"  [[{index}]] [green]◉[/] {Markup.Escape(name)} [dim]({Markup.Escape(device.IPAddress)}) - {FormatNumber(device.PacketCount)} packets[/]");
                    index++;
                }
            }

            if (active.Any())
            {
                deviceChoices.Add($"[bold cyan]═══ Active Devices ({active.Count}) ═══[/]");
                foreach (var device in active)
                {
                    var name = GetDeviceName(device);
                    var iconColor = (device.IsVirtualDevice || !device.IsLocal) ? "orange1" : "cyan";
                    deviceChoices.Add($"  [[{index}]] [{iconColor}]●[/] {Markup.Escape(name)} [dim]({Markup.Escape(device.IPAddress)}) - {FormatNumber(device.PacketCount)} packets[/]");
                    index++;
                }
            }

            if (inactive.Any())
            {
                deviceChoices.Add($"[bold grey37]═══ Inactive Devices ({inactive.Count}) ═══[/]");
                foreach (var device in inactive)
                {
                    var name = GetDeviceName(device);
                    deviceChoices.Add($"  [[{index}]] [grey37]○[/] {Markup.Escape(name)} [dim]({Markup.Escape(device.IPAddress)}) - {FormatNumber(device.PacketCount)} packets[/]");
                    index++;
                }
            }

            deviceChoices.Add("[dim]───────────────────[/]");
            deviceChoices.Add("[yellow]« Back to Menu[/]");

            var selection = PromptSelection("[bold]Select a device:[/]", deviceChoices, pageSize: 20);

            if (selection == null)
            {
                viewing = false;
                continue;
            }

            if (selection.Contains("Back to Menu"))
            {
                viewing = false;
                continue;
            }

            if (selection.Contains("═══") || selection.Contains("───"))
            {
                continue; // Skip separator selections
            }

            var match = System.Text.RegularExpressions.Regex.Match(selection, @"\[\[(\d+)\]\]");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int deviceNumber))
            {
                int deviceIndex = deviceNumber - 1;
                if (deviceIndex >= 0 && deviceIndex < allDevicesOrdered.Count)
                {
                    var selectedDevice = allDevicesOrdered[deviceIndex];
                    bool stayInInteractiveMode = ShowDeviceActionsMenu(selectedDevice);
                    if (!stayInInteractiveMode)
                    {
                        viewing = false;
                    }
                }
            }
        }
    }

    private bool ShowDeviceActionsMenu(Device device)
    {
        bool viewing = true;
        bool stayInInteractiveMode = true;

        while (viewing)
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[bold]TLScope[/][dim] Network Security Visualization Tool[/]");
            AnsiConsole.Write(new Rule($"[white]Device Actions[/]").RuleStyle(Style.Parse("grey37")));
            AnsiConsole.WriteLine();

            var summaryTable = new Table()
                .Border(TableBorder.Markdown)
                .BorderColor(Color.Cyan3)
                .AddColumn(new TableColumn("[bold]Property[/]").LeftAligned())
                .AddColumn(new TableColumn("[bold]Value[/]").LeftAligned());

            var name = device.DeviceName ?? device.Hostname ?? "Unknown";
            summaryTable.AddRow("[cyan]Device Name[/]", Markup.Escape(name));
            summaryTable.AddRow("[cyan]IP Address[/]", device.IPAddress ?? "N/A");
            summaryTable.AddRow("[cyan]MAC Address[/]", device.MACAddress);
            if (!string.IsNullOrEmpty(device.Vendor))
                summaryTable.AddRow("[cyan]Vendor[/]", Markup.Escape(device.Vendor));
            summaryTable.AddRow("[cyan]Status[/]", device.IsActive ? "[green]Active[/]" : "[grey37]Inactive[/]");
            summaryTable.AddRow("[cyan]Packets[/]", FormatNumber(device.PacketCount));
            summaryTable.AddRow("[cyan]Bytes[/]", FormatNumber(device.BytesTransferred));
            summaryTable.AddRow("[cyan]Last Seen[/]", device.LastSeen.ToString("yyyy-MM-dd HH:mm:ss"));

            AnsiConsole.Write(summaryTable);
            AnsiConsole.WriteLine();

            var choices = new List<string>
            {
                "View Detailed Information",
                "Filter Network View (show only this device's connections)",
                "Highlight Device Connections",
                "Back to Device List",
                "Back to Dashboard"
            };

            var statusLines = new List<string>();
            if (_filteredDevice != null)
            {
                statusLines.Add($"[yellow]⚠ Network view filtered to: {_filteredDevice.IPAddress}[/]");
            }
            if (_highlightedDeviceMACs.Contains(device.MACAddress))
            {
                statusLines.Add($"[cyan]★ This device is highlighted[/]");
            }

            if (statusLines.Any())
            {
                foreach (var line in statusLines)
                {
                    AnsiConsole.MarkupLine(line);
                }
                AnsiConsole.WriteLine();
            }

            var action = PromptSelection("[bold]Select an action:[/]", choices, pageSize: 10);

            if (action == null)
            {
                viewing = false;
                stayInInteractiveMode = true;
                continue;
            }

            switch (action)
            {
                case "View Detailed Information":
                    ShowDeviceDetails(new List<Device> { device });
                    break;

                case "Filter Network View (show only this device's connections)":
                    FilterByDevice(device);
                    viewing = false;
                    break;

                case "Highlight Device Connections":
                    HighlightDevice(device);
                    break;

                case "Back to Device List":
                    viewing = false;
                    stayInInteractiveMode = true;
                    break;

                case "Back to Dashboard":
                    viewing = false;
                    stayInInteractiveMode = false;
                    break;
            }
        }

        return stayInInteractiveMode;
    }

    private void FilterByDevice(Device device)
    {
        _filteredDevice = device;
        lock (_refreshLock)
        {
            _dashboardNeedsRefresh = true;
        }
        Log.Information($"Filtered network view to device: {device.IPAddress} ({device.MACAddress})");

        AnsiConsole.MarkupLine($"[green]✓[/] Network view filtered to [cyan]{device.IPAddress}[/]");
        AnsiConsole.MarkupLine("[dim]Type 'clear' in the main dashboard to clear filter[/]");
        Thread.Sleep(2000);
    }

    private void HighlightDevice(Device device)
    {
        if (_highlightedDeviceMACs.Contains(device.MACAddress))
        {
            _highlightedDeviceMACs.Remove(device.MACAddress);
            Log.Information($"Removed highlight from device: {device.IPAddress} ({device.MACAddress})");
            AnsiConsole.MarkupLine($"[yellow]★[/] Highlight removed from [cyan]{device.IPAddress}[/]");
        }
        else
        {
            _highlightedDeviceMACs.Add(device.MACAddress);
            Log.Information($"Highlighted device: {device.IPAddress} ({device.MACAddress})");
            AnsiConsole.MarkupLine($"[yellow]★[/] Device [cyan]{device.IPAddress}[/] highlighted");
        }

        lock (_refreshLock)
        {
            _dashboardNeedsRefresh = true;
        }
        Thread.Sleep(1500);
    }

    private void ClearFilters()
    {
        bool hadFilters = _filteredDevice != null || _highlightedDeviceMACs.Any();

        _filteredDevice = null;
        _highlightedDeviceMACs.Clear();

        if (hadFilters)
        {
            lock (_refreshLock)
            {
                _dashboardNeedsRefresh = true;
            }
            Log.Information("Cleared all device filters and highlights");
            AnsiConsole.MarkupLine("[green]✓[/] Filters and highlights cleared");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]No filters to clear[/]");
        }

        Thread.Sleep(1500);
    }

    private void ShowDevicesTree(List<Device> devices)
    {
        var tree = new Tree("[bold white]Network Devices[/]")
            .Style(Style.Parse("grey37"));

        var tlsPeers = devices.Where(d => d.TLSPeer != null).OrderByDescending(d => d.LastSeen).ToList();
        var activeDevices = devices.Where(d => d.TLSPeer == null && d.IsActive).OrderByDescending(d => d.LastSeen).ToList();
        var inactiveDevices = devices.Where(d => d.TLSPeer == null && !d.IsActive).OrderByDescending(d => d.LastSeen).ToList();

        if (tlsPeers.Any())
        {
            var peersNode = tree.AddNode($"[green]◉ TLS Peers[/] [dim]({tlsPeers.Count})[/]");
            foreach (var device in tlsPeers)
            {
                var deviceInfo = $"[green]●[/] [bold]{device.DeviceName ?? device.Hostname ?? "Unknown"}[/]";
                var deviceNode = peersNode.AddNode(deviceInfo);

                deviceNode.AddNode($"[dim]IP:[/] {device.IPAddress}");
                deviceNode.AddNode($"[dim]MAC:[/] {device.MACAddress}");
                if (!string.IsNullOrEmpty(device.Hostname) && device.Hostname != device.DeviceName)
                    deviceNode.AddNode($"[dim]Hostname:[/] {device.Hostname}");
                if (!string.IsNullOrEmpty(device.OperatingSystem))
                    deviceNode.AddNode($"[dim]OS:[/] {device.OperatingSystem}");
                deviceNode.AddNode($"[dim]Packets:[/] {device.PacketCount:N0}");
                deviceNode.AddNode($"[dim]Bytes:[/] {FormatBytes(device.BytesTransferred)}");
                if (device.TLSPeer != null)
                {
                    deviceNode.AddNode($"[dim]Username:[/] [green]{device.TLSPeer.Username}[/]");
                    deviceNode.AddNode($"[dim]Avatar:[/] {device.TLSPeer.AvatarType}");
                }
            }
        }

        if (activeDevices.Any())
        {
            var activeNode = tree.AddNode($"[cyan]● Active Devices[/] [dim]({activeDevices.Count})[/]");
            foreach (var device in activeDevices)
            {
                var deviceInfo = $"[cyan]●[/] [bold]{device.DeviceName ?? device.Hostname ?? "Unknown"}[/]";
                var deviceNode = activeNode.AddNode(deviceInfo);

                deviceNode.AddNode($"[dim]IP:[/] {device.IPAddress}");
                deviceNode.AddNode($"[dim]MAC:[/] {device.MACAddress}");
                if (!string.IsNullOrEmpty(device.Hostname) && device.Hostname != device.DeviceName)
                    deviceNode.AddNode($"[dim]Hostname:[/] {device.Hostname}");
                if (!string.IsNullOrEmpty(device.OperatingSystem))
                    deviceNode.AddNode($"[dim]OS:[/] {device.OperatingSystem}");
                deviceNode.AddNode($"[dim]Packets:[/] {device.PacketCount:N0}");
                deviceNode.AddNode($"[dim]Bytes:[/] {FormatBytes(device.BytesTransferred)}");
            }
        }

        if (inactiveDevices.Any())
        {
            var inactiveNode = tree.AddNode($"[white]○ Inactive Devices[/] [dim]({inactiveDevices.Count})[/]");
            foreach (var device in inactiveDevices)
            {
                var deviceInfo = $"[white]○[/] [dim]{device.DeviceName ?? device.Hostname ?? "Unknown"}[/]";
                var deviceNode = inactiveNode.AddNode(deviceInfo);

                deviceNode.AddNode($"[dim]IP:[/] [grey37]{device.IPAddress}[/]");
                deviceNode.AddNode($"[dim]MAC:[/] [grey37]{device.MACAddress}[/]");
                if (!string.IsNullOrEmpty(device.Hostname) && device.Hostname != device.DeviceName)
                    deviceNode.AddNode($"[dim]Hostname:[/] [grey37]{device.Hostname}[/]");
                if (!string.IsNullOrEmpty(device.OperatingSystem))
                    deviceNode.AddNode($"[dim]OS:[/] [grey37]{device.OperatingSystem}[/]");
                deviceNode.AddNode($"[dim]Packets:[/] [grey37]{device.PacketCount:N0}[/]");
                deviceNode.AddNode($"[dim]Bytes:[/] [grey37]{FormatBytes(device.BytesTransferred)}[/]");
            }
        }

        AnsiConsole.Write(tree);

        if (AnsiConsole.Confirm("\nView device details?", false))
        {
            ShowDeviceDetails(devices);
        }
    }

    private void ShowDeviceDetails(List<Device> devices)
    {
        var allConnections = _graphService.GetAllConnections();

        var deviceNames = devices.Select(d =>
        {
            var (icon, color) = GetDeviceIconAndColor(d, allConnections);
            return $"[{color}]{icon}[/] {d.DeviceName ?? d.Hostname ?? d.IPAddress}";
        }).ToList();

        deviceNames.Add("[yellow]« Back[/]");

        var choice = PromptSelection("[default]Select device to view details:[/]", deviceNames, pageSize: 15);

        if (choice == null || choice.Contains("Back"))
        {
            return;
        }

        var index = deviceNames.IndexOf(choice);
        if (index >= 0 && index < devices.Count)
        {
            var device = devices[index];
            AnsiConsole.Clear();

            Avatar? avatar = null;
            string avatarColor = "#FFFFFF";
            string avatarType = "Unknown";

            if (device.TLSPeer != null)
            {
                avatar = AvatarUtility.GetAvatar(device.TLSPeer.AvatarType);
                avatarColor = device.TLSPeer.AvatarColor;
                avatarType = device.TLSPeer.AvatarType;
            }
            else
            {
                var avatarIndex = Math.Abs(device.MACAddress.GetHashCode()) % AvatarUtility.GetAvatarEnums().Count;
                var avatarEnum = AvatarUtility.GetAvatarEnums()[avatarIndex];
                avatar = AvatarUtility.GetAvatar(avatarEnum);
                avatarColor = AvatarUtility.GenerateColorFromSSHKey(device.MACAddress);
                avatarType = avatarEnum;
            }

            var avatarArt = "";
            if (avatar != null)
            {
                foreach (var line in avatar.Appearance)
                {
                    avatarArt += line + "\n";
                }
            }

            var peerInfo = "";
            if (device.TLSPeer != null)
            {
                peerInfo = $@"
[default]TLS Peer Info:[/]
  Username: {device.TLSPeer.Username}
  Avatar: {avatarType}
  Color: {avatarColor}
  Connected: {(device.TLSPeer.IsConnected ? "[green]Yes[/]" : "No")}
";
            }

            var connections = _graphService.GetDeviceConnections(device);
            var connectionsList = string.Join(", ", connections.Select(c =>
                c.SourceDevice == device ? c.DestinationDevice.IPAddress : c.SourceDevice.IPAddress
            ));

            var detailsText = $@"[default]Avatar:[/]
{avatarArt}
[default]Device Information:[/]
  Device: {device.DeviceName ?? "Unknown"}
  IP: {device.IPAddress}
  MAC: {device.MACAddress}
  Hostname: {device.Hostname ?? "Unknown"}
  OS: {device.OperatingSystem ?? "Unknown"}
  Vendor: {device.Vendor ?? "Unknown"}

[default]TLScope Peer:[/] {(device.TLSPeer != null ? "[green]Yes[/]" : "No")}{peerInfo}
[default]Activity:[/]
  First Seen: {FormatRelativeTime(device.FirstSeen, useColor: false)}
  Last Seen: {FormatRelativeTime(device.LastSeen, useColor: false)}
  Status: {(device.IsActive ? "[green]✓ Active[/]" : "✗ Inactive")}

[default]Traffic:[/]
  Packets: {device.PacketCount:N0}
  Bytes: {FormatBytes(device.BytesTransferred)}

[default]Network:[/]
  Open Ports: {(device.OpenPorts.Any() ? string.Join(", ", device.OpenPorts) : "None detected")}
  Connections: {connections.Count}
  {(connections.Count > 0 ? $"Connected to: {connectionsList}" : "")}
";

            var panel = new Panel(detailsText)
            {
                Header = new PanelHeader($"[default]{device.DeviceName ?? device.IPAddress}[/]"),
                Border = BoxBorder.Ascii,
                BorderStyle = new Style(Color.Default)
            };

            AnsiConsole.Write(panel);
        }
    }


    private void ShowPeersTable()
    {
        RenderViewHeader("TLS Peers");

        var peers = _peerService?.GetDiscoveredPeers() ?? new List<TLSPeer>();

        if (peers.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No peers discovered yet.[/]");

            if (AnsiConsole.Confirm("Announce your presence to the network?", true))
            {
                _peerService?.AnnouncePeer();
                AnsiConsole.MarkupLine("[green]Peer announcement sent![/]");
            }
            return;
        }

        var table = new Table()
            .Border(TableBorder.Markdown)
            .BorderColor(Color.Default)
            .AddColumn(new TableColumn("[default]Status[/]").Centered())
            .AddColumn("[default]Username[/]")
            .AddColumn("[default]IP Address[/]")
            .AddColumn("[default]Avatar[/]")
            .AddColumn("[default]Color[/]")
            .AddColumn("[default]Discovered[/]");

        foreach (var peer in peers.OrderByDescending(p => p.LastConnected))
        {
            var status = peer.IsConnected ? "[green]●[/]" : "○";
            var discovered = (DateTime.Now - peer.LastConnected).TotalSeconds < 60
                ? "[green]Recently[/]"
                : $"{(DateTime.Now - peer.LastConnected).TotalMinutes:F0}m ago";

            table.AddRow(
                status,
                peer.Username,
                peer.IPAddress?.ToString() ?? "Unknown",
                peer.AvatarType,
                peer.AvatarColor,
                discovered
            );
        }

        AnsiConsole.Write(table);

        if (AnsiConsole.Confirm("\nAnnounce your presence to the network?", false))
        {
            _peerService?.AnnouncePeer();
            AnsiConsole.MarkupLine("[green]Peer announcement sent![/]");
        }
    }

    private void ShowConnectionsTable()
    {
        bool viewing = true;

        while (viewing)
        {
            var connections = _graphService.GetAllConnections();

            if (connections.Count == 0)
            {
                AnsiConsole.Clear();
                AnsiConsole.MarkupLine("[bold]TLScope[/][dim] Network Security Visualization Tool[/]");
                AnsiConsole.Write(new Rule($"[white]Network Connections[/]").RuleStyle(Style.Parse("grey37")));
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]No connections detected yet.[/]");
                AnsiConsole.WriteLine();
                WaitForExit();
                return;
            }

            var table = new Table()
                .Border(TableBorder.Markdown)
                .BorderColor(Color.Grey37)
                .ShowRowSeparators()
                .AddColumn(new TableColumn($"[bold white]Status[/]").Centered())
                .AddColumn($"[bold white]Type[/]")
                .AddColumn($"[bold white]Source Device[/]")
                .AddColumn(new TableColumn($"[bold white]→[/]").Centered())
                .AddColumn($"[bold white]Destination Device[/]")
                .AddColumn($"[bold white]Protocol[/]")
                .AddColumn(new TableColumn($"[bold white]Packets[/]").RightAligned())
                .AddColumn(new TableColumn($"[bold white]Bytes[/]").RightAligned())
                .AddColumn($"[bold white]First Seen[/]")
                .AddColumn($"[bold white]Last Seen[/]");

            var orderedConnections = connections
                .OrderByDescending(c => c.IsTLSPeerConnection)
                .ThenByDescending(c => c.IsActive)
                .ThenByDescending(c => c.LastSeen)
                .ToList();

            foreach (var conn in orderedConnections)
            {
                var status = GetConnectionStatus(conn);

                var connType = conn.Type switch
                {
                    ConnectionType.DirectL2 => "[cyan]Direct L2[/]",
                    ConnectionType.RoutedL3 => "[yellow]Routed L3[/]",
                    ConnectionType.Internet => "[orange1]Internet[/]",
                    ConnectionType.TLSPeer => "[green]TLS Peer[/]",
                    _ => "[dim]Unknown[/]"
                };

                var sourceName = conn.SourceDevice.DeviceName ?? conn.SourceDevice.Hostname ?? conn.SourceDevice.IPAddress;
                var destName = conn.DestinationDevice.DeviceName ?? conn.DestinationDevice.Hostname ?? conn.DestinationDevice.IPAddress;

                var protocol = conn.Protocol ?? "Unknown";
                if (conn.SourcePort.HasValue || conn.DestinationPort.HasValue)
                {
                    var ports = $"{conn.SourcePort}→{conn.DestinationPort}";
                    protocol = $"{protocol} ({ports})";
                }

                var firstSeen = FormatRelativeTime(conn.FirstSeen);
                var lastSeen = FormatRelativeTime(conn.LastSeen);

                table.AddRow(
                    status,
                    connType,
                    Markup.Escape(sourceName),
                    "→",
                    Markup.Escape(destName),
                    protocol,
                    conn.PacketCount.ToString("N0"),
                    FormatBytes(conn.BytesTransferred),
                    firstSeen,
                    lastSeen
                );
            }

            string renderedTable = RenderToString(table);

            var mainHeader = new List<string>();
            mainHeader.Add(RenderToString(new Markup("[bold]TLScope[/][dim] Network Security Visualization Tool[/]")).TrimEnd());
            mainHeader.Add(RenderToString(new Rule($"[white]Network Connections[/]").RuleStyle(Style.Parse("grey37"))).TrimEnd());

            var tableLines = renderedTable.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            var allHeaderLines = tableLines.Take(3).ToArray();

            var titleLine = allHeaderLines.Length > 0 ? allHeaderLines[0] : "";
            var remainingHeaders = allHeaderLines.Length > 1 ? allHeaderLines.Skip(1).ToArray() : Array.Empty<string>();

            var dataRows = tableLines.Skip(3).ToList();

            var selectableRows = new List<string>();
            var rowToConnectionMap = new Dictionary<string, Connection>();
            int connectionIndex = 0;

            foreach (var row in dataRows)
            {
                var cleanRow = System.Text.RegularExpressions.Regex.Replace(row, @"\x1b\[[0-9;]*m", "");

                if (string.IsNullOrWhiteSpace(cleanRow))
                {
                    continue;
                }

                if (cleanRow.All(c => "─│├┤┬┴┼╭╮╰╯═║╔╗╚╝╠╣╦╩╬ :|+-".Contains(c)))
                {
                    continue;
                }

                if (cleanRow.Contains(":--:") || cleanRow.Contains("---"))
                {
                    continue;
                }

                if (connectionIndex < orderedConnections.Count)
                {
                    selectableRows.Add(row);
                    rowToConnectionMap[row] = orderedConnections[connectionIndex];
                    connectionIndex++;
                }
            }

            var selectedRow = PromptSelection(
                titleLine,
                selectableRows,
                pageSize: Console.WindowHeight - 15,
                isRawAnsi: true,
                headerLines: remainingHeaders,
                mainHeaderLines: mainHeader.ToArray()
            );

            if (selectedRow != null && rowToConnectionMap.ContainsKey(selectedRow))
            {
                ShowConnectionDetails(rowToConnectionMap[selectedRow]);
            }
            else
            {
                viewing = false;
            }
        }
    }

    /// <summary>
    /// Show comprehensive connection details when selected
    /// </summary>
    private void ShowConnectionDetails(Connection connection)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]TLScope[/][dim] Network Security Visualization Tool[/]");
        AnsiConsole.Write(new Rule($"[white]Connection Details[/]").RuleStyle(Style.Parse("grey37")));
        AnsiConsole.WriteLine();

        var overviewGrid = new Grid();
        overviewGrid.AddColumn();
        overviewGrid.AddColumn();

        var statusMarkup = connection.IsActive ? "[green]✓ Active[/]" : "[grey37]✗ Inactive[/]";
        overviewGrid.AddRow(new Markup("[bold]Status:[/]"), new Markup(statusMarkup));

        var connType = connection.Type switch
        {
            ConnectionType.DirectL2 => "[cyan]Direct Layer 2 (Same LAN)[/]",
            ConnectionType.RoutedL3 => "[yellow]Routed Layer 3 (Through Gateway)[/]",
            ConnectionType.Internet => "[orange1]Internet Connection[/]",
            ConnectionType.TLSPeer => "[green]TLScope Peer-to-Peer[/]",
            _ => "[dim]Unknown[/]"
        };
        overviewGrid.AddRow(new Markup("[bold]Connection Type:[/]"), new Markup(connType));

        if (connection.IsTLSPeerConnection)
        {
            overviewGrid.AddRow(new Markup("[bold]TLScope Peer:[/]"), new Markup("[green]✓ Yes[/]"));
        }

        AnsiConsole.Write(new Panel(overviewGrid).Header("[cyan]Overview[/]").Border(BoxBorder.Ascii));
        AnsiConsole.WriteLine();

        var endpointsGrid = new Grid();
        endpointsGrid.AddColumn();
        endpointsGrid.AddColumn();

        var sourceName = connection.SourceDevice.DeviceName ?? connection.SourceDevice.Hostname ?? connection.SourceDevice.IPAddress;
        var destName = connection.DestinationDevice.DeviceName ?? connection.DestinationDevice.Hostname ?? connection.DestinationDevice.IPAddress;

        endpointsGrid.AddRow(new Markup("[bold]Source:[/]"), new Markup(Markup.Escape(sourceName)));
        endpointsGrid.AddRow(new Markup("[bold]Source IP:[/]"), new Markup(Markup.Escape(connection.SourceDevice.IPAddress)));
        endpointsGrid.AddRow(new Markup("[bold]Source MAC:[/]"), new Markup(Markup.Escape(connection.SourceDevice.MACAddress)));
        if (connection.SourcePort.HasValue)
            endpointsGrid.AddRow(new Markup("[bold]Source Port:[/]"), new Markup(connection.SourcePort.Value.ToString()));

        endpointsGrid.AddRow(new Text(""), new Text(""));

        endpointsGrid.AddRow(new Markup("[bold]Destination:[/]"), new Markup(Markup.Escape(destName)));
        endpointsGrid.AddRow(new Markup("[bold]Destination IP:[/]"), new Markup(Markup.Escape(connection.DestinationDevice.IPAddress)));
        endpointsGrid.AddRow(new Markup("[bold]Destination MAC:[/]"), new Markup(Markup.Escape(connection.DestinationDevice.MACAddress)));
        if (connection.DestinationPort.HasValue)
            endpointsGrid.AddRow(new Markup("[bold]Destination Port:[/]"), new Markup(connection.DestinationPort.Value.ToString()));

        AnsiConsole.Write(new Panel(endpointsGrid).Header("[yellow]Endpoints[/]").Border(BoxBorder.Ascii));
        AnsiConsole.WriteLine();

        var protocolGrid = new Grid();
        protocolGrid.AddColumn();
        protocolGrid.AddColumn();

        protocolGrid.AddRow(new Markup("[bold]Protocol:[/]"), new Markup(Markup.Escape(connection.Protocol ?? "Unknown")));

        if (connection.State != null)
            protocolGrid.AddRow(new Markup("[bold]State:[/]"), new Markup(Markup.Escape(connection.State)));

        if (connection.MinTTL.HasValue)
            protocolGrid.AddRow(new Markup("[bold]Min TTL:[/]"), new Markup(connection.MinTTL.Value.ToString()));

        if (connection.MaxTTL.HasValue)
            protocolGrid.AddRow(new Markup("[bold]Max TTL:[/]"), new Markup(connection.MaxTTL.Value.ToString()));

        if (connection.AverageTTL.HasValue)
            protocolGrid.AddRow(new Markup("[bold]Average TTL:[/]"), new Markup(connection.AverageTTL.Value.ToString()));

        if (connection.PacketCountForTTL > 0)
            protocolGrid.AddRow(new Markup("[bold]TTL Samples:[/]"), new Markup($"{connection.PacketCountForTTL} packets"));

        AnsiConsole.Write(new Panel(protocolGrid).Header("[white]Protocol & Routing[/]").Border(BoxBorder.Ascii));
        AnsiConsole.WriteLine();

        var trafficGrid = new Grid();
        trafficGrid.AddColumn();
        trafficGrid.AddColumn();

        trafficGrid.AddRow(new Markup("[bold]Total Packets:[/]"), new Markup($"{connection.PacketCount:N0}"));
        trafficGrid.AddRow(new Markup("[bold]Total Bytes:[/]"), new Markup(FormatBytes(connection.BytesTransferred)));

        AnsiConsole.Write(new Panel(trafficGrid).Header("[cyan]Traffic Statistics[/]").Border(BoxBorder.Ascii));
        AnsiConsole.WriteLine();

        var timelineGrid = new Grid();
        timelineGrid.AddColumn();
        timelineGrid.AddColumn();

        timelineGrid.AddRow(new Markup("[bold]First Seen:[/]"), new Markup(FormatRelativeTime(connection.FirstSeen)));
        timelineGrid.AddRow(new Markup("[bold]Last Seen:[/]"), new Markup(FormatRelativeTime(connection.LastSeen)));

        var duration = connection.LastSeen - connection.FirstSeen;
        var durationStr = duration.TotalDays >= 1
            ? $"{duration.TotalDays:F1} days"
            : duration.TotalHours >= 1
                ? $"{duration.TotalHours:F1} hours"
                : $"{duration.TotalMinutes:F0} minutes";
        timelineGrid.AddRow(new Markup("[bold]Duration:[/]"), new Markup(durationStr));

        AnsiConsole.Write(new Panel(timelineGrid).Header("[white]Timeline[/]").Border(BoxBorder.Ascii));
        AnsiConsole.WriteLine();

        WaitForExit();
    }

    private async Task CustomizeAvatar()
    {
        if (_currentUser == null) return;

        var avatarLines = _userService.GetUserAvatar(_currentUser).ToArray();

        var lineVariations = new List<string>[4];
        for (int i = 0; i < 4; i++)
        {
            lineVariations[i] = GetLineVariations(i);
        }

        var lineOptionIndices = new int[4];
        for (int i = 0; i < 4; i++)
        {
            var index = lineVariations[i].IndexOf(avatarLines[i]);
            lineOptionIndices[i] = index >= 0 ? index : 0;
            avatarLines[i] = lineVariations[i][lineOptionIndices[i]];
        }

        int selectedLine = 0;
        bool isDirty = false;
        bool exit = false;

        while (!exit)
        {
            RenderCenteredAvatar(avatarLines, selectedLine, isDirty, lineOptionIndices, lineVariations);

            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    selectedLine = Math.Max(0, selectedLine - 1);
                    break;

                case ConsoleKey.DownArrow:
                    selectedLine = Math.Min(3, selectedLine + 1);  // Avatar has 4 lines (0-3)
                    break;

                case ConsoleKey.LeftArrow:
                    lineOptionIndices[selectedLine] = (lineOptionIndices[selectedLine] - 1 + lineVariations[selectedLine].Count)
                                                      % lineVariations[selectedLine].Count;
                    avatarLines[selectedLine] = lineVariations[selectedLine][lineOptionIndices[selectedLine]];
                    isDirty = true;
                    break;

                case ConsoleKey.RightArrow:
                    lineOptionIndices[selectedLine] = (lineOptionIndices[selectedLine] + 1)
                                                      % lineVariations[selectedLine].Count;
                    avatarLines[selectedLine] = lineVariations[selectedLine][lineOptionIndices[selectedLine]];
                    isDirty = true;
                    break;

                case ConsoleKey.S:
                    await _userService.SaveCustomAvatar(_currentUser, avatarLines);
                    AnsiConsole.Clear();
                    AnsiConsole.MarkupLine("[green]✓ Custom avatar saved![/]");
                    await Task.Delay(1000);
                    exit = true;
                    break;

                case ConsoleKey.R:
                    await ResetToPredefinedAvatar();
                    exit = true;
                    break;

                case ConsoleKey.Escape:
                    if (isDirty)
                    {
                        AnsiConsole.Clear();
                        if (AnsiConsole.Confirm("[yellow]You have unsaved changes. Exit anyway?[/]", false))
                        {
                            exit = true;
                        }
                    }
                    else
                    {
                        exit = true;
                    }
                    break;
            }
        }
    }

    private void RenderCenteredAvatar(string[] avatarLines, int selectedLine, bool isDirty,
                                       int[] lineOptionIndices, List<string>[] lineVariations)
    {
        AnsiConsole.Clear();

        var sshKey = _currentUser?.SSHPublicKey ?? "No SSH Key";
        var avatarColor = _currentUser?.AvatarColor ?? "#FFFFFF";
        var spectreColor = AvatarUtility.HexToSpectreColor(avatarColor);

        var randomartLines = SSHRandomart.GenerateRandomart(sshKey);

        var leftContent = new List<Text>();
        leftContent.Add(new Text(""));
        leftContent.Add(new Text("  Your Identity  "));
        leftContent.Add(new Text(""));

        foreach (var line in randomartLines)
        {
            leftContent.Add(new Text(line, new Style(spectreColor)));
        }

        leftContent.Add(new Text(""));
        leftContent.Add(new Text($"Color: {avatarColor}"));
        leftContent.Add(new Text(""));

        leftContent.Add(new Text("██████████████", new Style(spectreColor)));
        leftContent.Add(new Text("██████████████", new Style(spectreColor)));
        leftContent.Add(new Text("██████████████", new Style(spectreColor)));
        leftContent.Add(new Text(""));

        var rightContent = new List<Text>();
        rightContent.Add(new Text(""));
        rightContent.Add(new Text($"Customize Avatar{(isDirty ? " *" : "")}"));
        rightContent.Add(new Text(""));

        for (int i = 0; i < avatarLines.Length; i++)
        {
            var line = avatarLines[i];

            if (i == selectedLine)
            {
                var styledText = new Text($"*{line}*", new Style(spectreColor));
                rightContent.Add(styledText);
            }
            else
            {
                rightContent.Add(new Text(line, new Style(spectreColor)));
            }
        }

        rightContent.Add(new Text(""));

        var combinedTable = new Table()
            .Border(TableBorder.Markdown)
            .BorderColor(spectreColor)
            .HideHeaders()
            .AddColumn(new TableColumn("").Centered())
            .AddColumn(new TableColumn("").Centered());

        int maxRows = Math.Max(leftContent.Count, rightContent.Count);

        for (int i = 0; i < maxRows; i++)
        {
            var left = i < leftContent.Count ? leftContent[i] : new Text("");
            var right = i < rightContent.Count ? rightContent[i] : new Text("");
            combinedTable.AddRow(left, right);
        }

        int consoleHeight = Console.WindowHeight;
        int contentHeight = 25; // Approximate height of content
        int topPadding = Math.Max(0, (consoleHeight - contentHeight) / 2);

        for (int i = 0; i < topPadding; i++)
        {
            AnsiConsole.WriteLine();
        }

        AnsiConsole.Write(Align.Center(combinedTable));

        Console.WriteLine();
        Console.WriteLine();
        var instructions = "[dim]↑/↓[/] Select Line  [dim]│[/]  " +
                          "[dim]←/→[/] Change  [dim]│[/]  " +
                          "S Save  [dim]│[/]  " +
                          "[yellow]R[/] Reset  [dim]│[/]  " +
                          "[red]Esc[/] Cancel";
        AnsiConsole.Write(Align.Center(new Markup(instructions)));

        var lineInfo = $"[dim]Line {selectedLine + 1}/4  •  Option {lineOptionIndices[selectedLine] + 1}/{lineVariations[selectedLine].Count}[/]";
        Console.WriteLine();
        AnsiConsole.Write(Align.Center(new Markup(lineInfo)));
    }

    /// <summary>
    /// Hardcoded avatar line options extracted from appearances.json
    /// </summary>
    private static readonly Dictionary<int, List<string>> HardcodedLineOptions = new()
    {
        [0] = new List<string>
        {
            "       ",  // empty
            "   o   ",  // simple dot
            "  _*_  ",  // devil horns
            "   V   ",  // knight helmet
            "  ,/^\\_", // wizard hat
            ".( - ).", // silly
            "   .   "   // skeleton
        },

        [1] = new List<string>
        {
            "  ───  ",      // head var 1
            " .---. ",      // head var 2
            "./\\_/\\.",    // Cat Ears
            " _---_ ",      // monk
            "./|-|\\.",     // devil
            "_/___{_",      // wizard
            "(/\\|/\\)",    // silly
            ".n_._n."       // bear ears
        },

        [2] = new List<string>
        {
            "(     )", // empty
            "( . . )", // beady eyes
            "( .L. )", // strong nose
            "c .l. Ↄ", // monkey
            "< . . >", // elf ears
            "{(\\./)}", // angry
            "( ^.^ )", // happy
            "( o.^ )", // wink
            "( =,= )", // sleepy
            "( o,o )", // snotty
            "( $.$ )", // money
            "( o.o )", // normal round
            "( v.v )", // monk
            "( @.@ )", // devil
            "(\\\\|//)", // knight
            "( o.o T", // wizard
            "([o.o])", // ninja
            "(.oCo.)", // silly
            ". 0▲0 .", // skeleton
            "| . . |", // square
            "(#o.0 )", // zombie
            "( o^o )"  // vamp
        },

        [3] = new List<string>
        {
            " >   < ", // empty
            " > - < ", // normal
            " > ∩ < ", // sad
            " > u < ", // happy round
            "Zzz^ < ", // sleepy
            "H> - <n", // monk
            "=> v <=", // devil
            "T> u < ", // knight
            " > W <|", // wizard
            " > = < ", // silly
            " >)m(< ", // skeleton
            " >_C#< ", // zombie
            "^>v v<^"  // vamp
        }
    };

    private List<string> GetLineVariations(int lineIndex)
    {
        var variations = new List<string>(HardcodedLineOptions[lineIndex]);

        try
        {
            var allAvatars = AvatarUtility.LoadAvatars();
            var customLines = allAvatars
                .Select(a => a.Appearance[lineIndex])
                .Distinct()
                .Where(line => !variations.Contains(line)) // Only add new custom lines
                .ToList();

            variations.AddRange(customLines);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load custom avatar variations");
        }

        return variations;
    }

    private async Task ResetToPredefinedAvatar()
    {
        if (_currentUser == null) return;

        AnsiConsole.Clear();
        var avatars = AvatarUtility.GetAvatarEnums();
        avatars.Add("[yellow]« Cancel[/]");

        var selectedAvatar = PromptSelection("[default]Choose a predefined avatar:[/]", avatars, pageSize: 15);

        if (selectedAvatar == null || selectedAvatar.Contains("Cancel"))
        {
            return;
        }

        _currentUser.AvatarType = selectedAvatar;
        await _userService.ClearCustomAvatar(_currentUser);

        AnsiConsole.MarkupLine($"[green]✓ Avatar reset to {selectedAvatar}[/]");
        await Task.Delay(1000);
    }

    private async Task ManageNetworkInterface()
    {
        RenderViewHeader("Network Interface Management");

        var interfaces = _captureService.GetAvailableInterfaces();

        if (interfaces.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No network interfaces found.[/]");
            AnsiConsole.MarkupLine("[yellow]Run with elevated privileges (sudo) to access network interfaces.[/]");
            return;
        }

        var currentInterface = _captureService.GetCurrentInterface();
        var isCapturing = _captureService.IsCapturing();

        AnsiConsole.MarkupLine($"Current Interface: {currentInterface ?? "None"}");
        var statusMarkup = isCapturing ? "[green]Capturing[/]" : "[dim]Stopped[/]";
        AnsiConsole.MarkupLine($"Status: {statusMarkup}");
        Console.WriteLine();

        var choices = interfaces.Concat(new[] { "[yellow]« Back to Menu[/]" }).ToList();
        var choice = PromptSelection("Select network interface:", choices, pageSize: 15);

        if (choice == null || choice.Contains("Back to Menu"))
        {
            return;
        }

        var interfaceName = choice.Split(" - ")[0];

        if (isCapturing)
        {
            _captureService.StopCapture();
            AnsiConsole.MarkupLine("[yellow]Stopped current capture[/]");
        }

        try
        {
            await AnsiConsole.Status()
                .StartAsync("Starting capture...", async ctx =>
                {
                    _captureService.StartCapture(interfaceName);
                    await Task.Delay(1000);
                });

            AnsiConsole.MarkupLine($"[green]Started capture on {choice}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to start capture: {ex.Message}[/]");
        }
    }

    private void ManageExclusions()
    {
        bool managing = true;

        while (managing)
        {
            RenderViewHeader("Manage Exclusions");

            AnsiConsole.MarkupLine("[dim]Configure IPs, hostnames, and MAC addresses to exclude from monitoring[/]");
            Console.WriteLine();

            var grid = new Grid();
            grid.AddColumn(new GridColumn().Width(20));
            grid.AddColumn(new GridColumn());

            grid.AddRow(
                new Markup("[bold]Excluded IPs:[/]"),
                new Markup(_excludedIPs.Count > 0 ? string.Join(", ", _excludedIPs) : "[dim]none[/]")
            );
            grid.AddRow(
                new Markup("[bold]Excluded Hostnames:[/]"),
                new Markup(_excludedHostnames.Count > 0 ? string.Join(", ", _excludedHostnames) : "[dim]none[/]")
            );
            grid.AddRow(
                new Markup("[bold]Excluded MACs:[/]"),
                new Markup(_excludedMACs.Count > 0 ? string.Join(", ", _excludedMACs) : "[dim]none[/]")
            );

            AnsiConsole.Write(grid);
            Console.WriteLine();
            Console.WriteLine();

            var actionChoices = new List<string> {
                "Add IP Address",
                "Add Hostname",
                "Add MAC Address",
                "Remove IP Address",
                "Remove Hostname",
                "Remove MAC Address",
                "Clear All Exclusions",
                "[yellow]« Back to Menu[/]"
            };

            var action = PromptSelection("What would you like to do?", actionChoices);

            if (action == null || action.Contains("Back to Menu"))
            {
                managing = false;
                continue;
            }

            switch (action)
            {
                case "Add IP Address":
                    var ip = AnsiConsole.Ask<string>("Enter IP address to exclude:");
                    _excludedIPs.Add(ip);
                    SaveExclusionsConfig();
                    AnsiConsole.MarkupLine($"[green]Added {ip} to exclusions[/]");
                    Thread.Sleep(1000);
                    break;

                case "Add Hostname":
                    var hostname = AnsiConsole.Ask<string>("Enter hostname to exclude:");
                    _excludedHostnames.Add(hostname);
                    SaveExclusionsConfig();
                    AnsiConsole.MarkupLine($"[green]Added {hostname} to exclusions[/]");
                    Thread.Sleep(1000);
                    break;

                case "Add MAC Address":
                    var mac = AnsiConsole.Ask<string>("Enter MAC address to exclude (e.g., AA:BB:CC:DD:EE:FF):");
                    _excludedMACs.Add(mac);
                    SaveExclusionsConfig();
                    AnsiConsole.MarkupLine($"[green]Added {mac} to exclusions[/]");
                    Thread.Sleep(1000);
                    break;

                case "Remove IP Address":
                    if (_excludedIPs.Count > 0)
                    {
                        var ipChoices = _excludedIPs.Concat(new[] { "[yellow]« Cancel[/]" }).ToList();
                        var ipToRemove = PromptSelection("Select IP to remove:", ipChoices);

                        if (ipToRemove != null && !ipToRemove.Contains("Cancel"))
                        {
                            _excludedIPs.Remove(ipToRemove);
                            SaveExclusionsConfig();
                            AnsiConsole.MarkupLine($"[yellow]Removed {ipToRemove} from exclusions[/]");
                            Thread.Sleep(1000);
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]No excluded IPs to remove[/]");
                        Thread.Sleep(1000);
                    }
                    break;

                case "Remove Hostname":
                    if (_excludedHostnames.Count > 0)
                    {
                        var hostnameChoices = _excludedHostnames.Concat(new[] { "[yellow]« Cancel[/]" }).ToList();
                        var hostnameToRemove = PromptSelection("Select hostname to remove:", hostnameChoices);

                        if (hostnameToRemove != null && !hostnameToRemove.Contains("Cancel"))
                        {
                            _excludedHostnames.Remove(hostnameToRemove);
                            SaveExclusionsConfig();
                            AnsiConsole.MarkupLine($"[yellow]Removed {hostnameToRemove} from exclusions[/]");
                            Thread.Sleep(1000);
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]No excluded hostnames to remove[/]");
                        Thread.Sleep(1000);
                    }
                    break;

                case "Remove MAC Address":
                    if (_excludedMACs.Count > 0)
                    {
                        var macChoices = _excludedMACs.Concat(new[] { "[yellow]« Cancel[/]" }).ToList();
                        var macToRemove = PromptSelection("Select MAC to remove:", macChoices);

                        if (macToRemove != null && !macToRemove.Contains("Cancel"))
                        {
                            _excludedMACs.Remove(macToRemove);
                            SaveExclusionsConfig();
                            AnsiConsole.MarkupLine($"[yellow]Removed {macToRemove} from exclusions[/]");
                            Thread.Sleep(1000);
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]No excluded MACs to remove[/]");
                        Thread.Sleep(1000);
                    }
                    break;

                case "Clear All Exclusions":
                    if (AnsiConsole.Confirm("Are you sure you want to clear all exclusions?"))
                    {
                        _excludedIPs.Clear();
                        _excludedHostnames.Clear();
                        _excludedMACs.Clear();
                        SaveExclusionsConfig();
                        AnsiConsole.MarkupLine("[yellow]All exclusions cleared[/]");
                        Thread.Sleep(1000);
                    }
                    break;

                case "Back to Menu":
                    managing = false;
                    break;
            }
        }
    }

    private void ShowIPFilterSettings()
    {
        bool managing = true;

        while (managing)
        {
            RenderViewHeader("IP Filter Settings");

            AnsiConsole.MarkupLine("[dim]Configure which IP addresses are filtered during packet capture[/]");
            Console.WriteLine();

            var settingsGrid = new Grid();
            settingsGrid.AddColumn(new GridColumn().Width(30));
            settingsGrid.AddColumn(new GridColumn().Width(12));
            settingsGrid.AddColumn(new GridColumn());

            settingsGrid.AddRow(
                new Markup("[bold]Filter Type[/]"),
                new Markup("[bold]Status[/]"),
                new Markup("[bold]Description[/]")
            );
            settingsGrid.AddRow(new Text(""), new Text(""), new Text(""));

            var loopbackStatus = _filterConfig.FilterLoopback ? "[green]ENABLED[/]" : "[dim]disabled[/]";
            settingsGrid.AddRow(
                new Markup("Loopback Addresses"),
                new Markup(loopbackStatus),
                new Markup("[dim]127.0.0.0/8 (localhost)[/]")
            );

            var broadcastStatus = _filterConfig.FilterBroadcast ? "[green]ENABLED[/]" : "[dim]disabled[/]";
            settingsGrid.AddRow(
                new Markup("Broadcast Address"),
                new Markup(broadcastStatus),
                new Markup("[dim]255.255.255.255[/]")
            );

            var multicastStatus = _filterConfig.FilterMulticast ? "[green]ENABLED[/]" : "[dim]disabled[/]";
            settingsGrid.AddRow(
                new Markup("Multicast Addresses"),
                new Markup(multicastStatus),
                new Markup("[dim]224.0.0.0/4 (224-239.x.x.x)[/]")
            );

            var linkLocalStatus = _filterConfig.FilterLinkLocal ? "[green]ENABLED[/]" : "[dim]disabled[/]";
            settingsGrid.AddRow(
                new Markup("Link-Local Addresses"),
                new Markup(linkLocalStatus),
                new Markup("[dim]169.254.0.0/16[/]")
            );

            var reservedStatus = _filterConfig.FilterReserved ? "[green]ENABLED[/]" : "[dim]disabled[/]";
            settingsGrid.AddRow(
                new Markup("Reserved Addresses"),
                new Markup(reservedStatus),
                new Markup("[dim]0.0.0.0/8, 240.0.0.0/4[/]")
            );

            settingsGrid.AddRow(new Text(""), new Text(""), new Text(""));

            var duplicateStatus = _filterConfig.BlockDuplicateIPs ? "[green]ENABLED[/]" : "[dim]disabled[/]";
            settingsGrid.AddRow(
                new Markup("Block Duplicate IPs"),
                new Markup(duplicateStatus),
                new Markup("[dim]Same IP on different MACs[/]")
            );

            var httpFilterStatus = _filterConfig.FilterHttpTraffic ? "[green]ENABLED[/]" : "[dim]disabled[/]";
            settingsGrid.AddRow(
                new Markup("Filter HTTP/HTTPS Traffic"),
                new Markup(httpFilterStatus),
                new Markup("[dim]Ports 80, 443, 8080, 8443[/]")
            );

            var nonLocalFilterStatus = _filterConfig.FilterNonLocalTraffic ? "[green]ENABLED[/]" : "[dim]disabled[/]";
            settingsGrid.AddRow(
                new Markup("Filter Non-Local Traffic"),
                new Markup(nonLocalFilterStatus),
                new Markup("[dim]Show only local network traffic[/]")
            );

            settingsGrid.AddRow(new Text(""), new Text(""), new Text(""));

            var showInactiveStatus = _filterConfig.ShowInactiveDevices ? "[green]ENABLED[/]" : "[dim]disabled[/]";
            settingsGrid.AddRow(
                new Markup("Show Inactive Devices"),
                new Markup(showInactiveStatus),
                new Markup("[dim]Display devices not seen recently[/]")
            );

            var autoRemoveStatus = _filterConfig.AutoRemoveInactiveDevices ? "[green]ENABLED[/]" : "[dim]disabled[/]";
            settingsGrid.AddRow(
                new Markup("Auto-Remove Inactive Devices"),
                new Markup(autoRemoveStatus),
                new Markup("[dim]Remove inactive devices every 30s[/]")
            );

            AnsiConsole.Write(new Panel(settingsGrid).Border(BoxBorder.Ascii).BorderColor(Color.Cyan1));
            Console.WriteLine();

            AnsiConsole.MarkupLine($"[bold]Filter Statistics:[/]");
            AnsiConsole.MarkupLine($"  • Total addresses filtered: [cyan]{_filterConfig.TotalFiltered}[/]");
            AnsiConsole.MarkupLine($"  • Duplicate IPs blocked: [yellow]{_filterConfig.DuplicatesBlocked}[/]");
            AnsiConsole.MarkupLine($"  • HTTP/HTTPS traffic filtered: [yellow]{_filterConfig.HttpTrafficFiltered}[/]");
            AnsiConsole.MarkupLine($"  • Non-local traffic filtered: [yellow]{_filterConfig.NonLocalTrafficFiltered}[/]");
            Console.WriteLine();

            AnsiConsole.MarkupLine($"[bold]Visualization Settings:[/]");
            var connectionMode = _displayConfig.UseAsciiConnections ? "[yellow]ASCII[/] (compatible)" : "[cyan]Unicode Math Dots[/] (enhanced)";
            AnsiConsole.MarkupLine($"  • Connection characters: {connectionMode}");
            Console.WriteLine();

            AnsiConsole.MarkupLine("[dim]Note: Private network addresses (192.168.x.x, 10.x.x.x, 172.16-31.x.x) are never filtered[/]");
            Console.WriteLine();

            var actionChoices = new List<string> {
                $"{(_filterConfig.FilterLoopback ? "[green][[✓]][/]" : "[[ ]]")} Loopback Filter",
                $"{(_filterConfig.FilterBroadcast ? "[green][[✓]][/]" : "[[ ]]")} Broadcast Filter",
                $"{(_filterConfig.FilterMulticast ? "[green][[✓]][/]" : "[[ ]]")} Multicast Filter",
                $"{(_filterConfig.FilterLinkLocal ? "[green][[✓]][/]" : "[[ ]]")} Link-Local Filter",
                $"{(_filterConfig.FilterReserved ? "[green][[✓]][/]" : "[[ ]]")} Reserved Filter",
                $"{(_filterConfig.BlockDuplicateIPs ? "[green][[✓]][/]" : "[[ ]]")} Duplicate IP Blocking",
                $"{(_filterConfig.FilterHttpTraffic ? "[green][[✓]][/]" : "[[ ]]")} HTTP/HTTPS Filter",
                $"{(_filterConfig.FilterNonLocalTraffic ? "[green][[✓]][/]" : "[[ ]]")} Non-Local Traffic Filter",
                $"{(_filterConfig.ShowInactiveDevices ? "[green][[✓]][/]" : "[[ ]]")} Show Inactive Devices",
                $"{(_filterConfig.AutoRemoveInactiveDevices ? "[green][[✓]][/]" : "[[ ]]")} Auto-Remove Inactive Devices",
                $"{(_displayConfig.UseAsciiConnections ? "[green][[✓]][/]" : "[[ ]]")} ASCII Connection Characters",
                "Reset Statistics",
                "[yellow]« Back to Menu[/]"
            };

            var action = PromptSelection("What would you like to do?", actionChoices);

            if (action == null || action.Contains("Back to Menu"))
            {
                managing = false;
                continue;
            }

            bool settingChanged = false;

            if (action.Contains("Loopback Filter"))
            {
                _filterConfig.FilterLoopback = !_filterConfig.FilterLoopback;
                settingChanged = true;
            }
            else if (action.Contains("Broadcast Filter"))
            {
                _filterConfig.FilterBroadcast = !_filterConfig.FilterBroadcast;
                settingChanged = true;
            }
            else if (action.Contains("Multicast Filter"))
            {
                _filterConfig.FilterMulticast = !_filterConfig.FilterMulticast;
                settingChanged = true;
            }
            else if (action.Contains("Link-Local Filter"))
            {
                _filterConfig.FilterLinkLocal = !_filterConfig.FilterLinkLocal;
                settingChanged = true;
            }
            else if (action.Contains("Reserved Filter"))
            {
                _filterConfig.FilterReserved = !_filterConfig.FilterReserved;
                settingChanged = true;
            }
            else if (action.Contains("Duplicate IP Blocking"))
            {
                _filterConfig.BlockDuplicateIPs = !_filterConfig.BlockDuplicateIPs;
                settingChanged = true;
            }
            else if (action.Contains("HTTP/HTTPS Filter"))
            {
                _filterConfig.FilterHttpTraffic = !_filterConfig.FilterHttpTraffic;
                settingChanged = true;
            }
            else if (action.Contains("Non-Local Traffic Filter"))
            {
                _filterConfig.FilterNonLocalTraffic = !_filterConfig.FilterNonLocalTraffic;
                settingChanged = true;
            }
            else if (action.Contains("Show Inactive Devices"))
            {
                _filterConfig.ShowInactiveDevices = !_filterConfig.ShowInactiveDevices;
                settingChanged = true;
            }
            else if (action.Contains("Auto-Remove Inactive Devices"))
            {
                _filterConfig.AutoRemoveInactiveDevices = !_filterConfig.AutoRemoveInactiveDevices;
                settingChanged = true;
            }
            else if (action.Contains("ASCII Connection Characters"))
            {
                _displayConfig.UseAsciiConnections = !_displayConfig.UseAsciiConnections;
                _displayConfig.Save();
                var mode = _displayConfig.UseAsciiConnections ? "ASCII" : "Unicode Math Dots";
                AnsiConsole.MarkupLine($"[green]✓ Connection characters changed to {mode}[/]");
                Thread.Sleep(1000);
            }
            else if (action == "Reset Statistics")
            {
                if (AnsiConsole.Confirm("Reset filter statistics?"))
                {
                    _filterConfig.TotalFiltered = 0;
                    _filterConfig.DuplicatesBlocked = 0;
                    _filterConfig.HttpTrafficFiltered = 0;
                    _filterConfig.NonLocalTrafficFiltered = 0;
                    _filterConfig.Save();
                    AnsiConsole.MarkupLine("[green]Statistics reset[/]");
                    Thread.Sleep(1000);
                }
            }
            else if (action == "Back to Menu")
            {
                managing = false;
            }

            if (settingChanged)
            {
                _filterConfig.Save();
                AnsiConsole.MarkupLine("[green]✓ Settings saved to tlscope_filters.json[/]");
                Thread.Sleep(1000);
            }
        }
    }

    private async Task ExportGraph()
    {
        RenderViewHeader("Export Options");

        var exportChoices = new List<string> {
            "DOT Graph (Graphviz format)",
            "LaTeX/PDF Report (comprehensive analysis)",
            "[yellow]« Cancel[/]"
        };

        var choice = PromptSelection("Choose export format:", exportChoices);

        if (choice == null || choice.Contains("Cancel"))
            return;

        if (choice.StartsWith("DOT"))
        {
            await ExportDotGraph();
        }
        else if (choice.StartsWith("LaTeX"))
        {
            await ExportLatexReport();
        }
    }

    private async Task ExportDotGraph()
    {
        try
        {
            var filename = AnsiConsole.Ask("Enter filename:", "network_graph.dot");

            var dot = _graphService.ExportToDot();
            await File.WriteAllTextAsync(filename, dot);

            AnsiConsole.MarkupLine($"[green]✓ Graph exported to {filename}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Export failed: {ex.Message}[/]");
        }
    }

    private async Task ExportLatexReport()
    {
        try
        {
            var baseFilename = AnsiConsole.Ask("Enter base filename:", "network_report");

            AnsiConsole.MarkupLine("[dim]Generating LaTeX report...[/]");

            var reportData = await CollectReportData();

            var latexService = new LatexReportService();
            var (success, filePath, isPdf) = await latexService.ExportLatexReport(reportData, baseFilename);

            if (!success)
            {
                AnsiConsole.MarkupLine("[red]✗ Report generation failed[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[green]✓ LaTeX file created: {baseFilename}.tex[/]");
            AnsiConsole.MarkupLine($"[green]✓ DOT file created: {baseFilename}.dot[/]");

            if (isPdf)
            {
                AnsiConsole.MarkupLine($"[green]✓ PDF generated successfully: {filePath}[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[cyan]Report includes:[/]");
                AnsiConsole.MarkupLine($"  • {reportData.TotalDevices} devices analyzed");
                AnsiConsole.MarkupLine($"  • {reportData.TotalConnections} connections documented");
                AnsiConsole.MarkupLine("  • Network topology visualization");
                AnsiConsole.MarkupLine("  • Statistical analysis");
                AnsiConsole.MarkupLine("  • DOT graph export (separate file and appendix pages)");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠ pdflatex not found in PATH[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("To generate PDF, run:");
                AnsiConsole.MarkupLine($"  pdflatex {baseFilename}.tex");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("Or install LaTeX:");
                AnsiConsole.MarkupLine("  Ubuntu/Debian: sudo apt install texlive-full");
                AnsiConsole.MarkupLine("  macOS: brew install mactex");
                AnsiConsole.MarkupLine("  Windows: Install MiKTeX or TeX Live");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Export failed: {ex.Message}[/]");
            Log.Error(ex, "LaTeX report export failed");
        }
    }

    private async Task<ReportData> CollectReportData()
    {
        var devices = _graphService.GetAllDevices();
        var connections = _graphService.GetAllConnections();
        var peers = _peerService?.GetDiscoveredPeers() ?? new List<TLSPeer>();
        var statistics = _graphService.GetStatistics();

        var vertexCut = CalculateVertexCut(devices, connections);
        var (mostConnectedDevice, mostConnectedCount) = GetMostConnectedDevice(devices, connections);

        var sessionDuration = devices.Any()
            ? DateTime.Now - devices.Min(d => d.FirstSeen)
            : TimeSpan.Zero;

        return await Task.FromResult(new ReportData
        {
            CurrentUser = _currentUser,
            ReportGeneratedAt = DateTime.Now,
            SessionDuration = sessionDuration,
            NetworkInterface = _captureService.GetCurrentInterface() ?? "N/A",
            IsCaptureRunning = _captureService.IsCapturing(),
            Devices = devices,
            Connections = connections,
            Peers = peers,
            Statistics = statistics,
            VertexCut = vertexCut,
            MostConnectedDevice = mostConnectedDevice,
            MostConnectedCount = mostConnectedCount,
            FilterConfig = _filterConfig,
            ExcludedIPs = _excludedIPs,
            ExcludedHostnames = _excludedHostnames,
            ExcludedMACs = _excludedMACs,
            DotGraphContent = _graphService.ExportToDot(),
            ProtocolDistribution = _graphService.GetProtocolDistribution(),
            PortDistribution = _graphService.GetPortDistribution()
        });
    }

    private async Task ShowLogs()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]TLScope[/][dim] Network Security Visualization Tool[/]");
        AnsiConsole.Write(new Rule("[white]Recent Activity & Packet Events - LIVE[/]").RuleStyle(Style.Parse("grey37")));
        AnsiConsole.WriteLine();

        int availableHeight = Console.WindowHeight - 6;
        int maxRows = Math.Max(10, availableHeight);

        var table = new Table()
            .Border(TableBorder.Ascii)
            .BorderColor(Color.Grey37)
            .Expand()
            .AddColumn("[bold white]Time[/]")
            .AddColumn("[bold white]Event[/]");

        bool hasMessages = false;
        lock (_logLock)
        {
            hasMessages = _logMessages.Count > 0;
        }

        if (!hasMessages)
        {
            AnsiConsole.MarkupLine("[yellow]No log messages yet. Waiting for events...[/]");
            AnsiConsole.Markup("[dim]Press Q to exit..[/]");

            await WaitForExitLoopAsync();
            return;
        }

        await AnsiConsole.Live(table)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Crop)
            .Cropping(VerticalOverflowCropping.Top)
            .StartAsync(async ctx =>
            {
                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Q)
                        {
                            break;
                        }
                    }

                    table = new Table()
                        .Border(TableBorder.Ascii)
                        .BorderColor(Color.Grey37)
                        .Expand()
                        .AddColumn("[bold white]Time[/]")
                        .AddColumn("[bold white]Event[/]");

                    List<string> messagesToDisplay;
                    lock (_logLock)
                    {
                        messagesToDisplay = _logMessages
                            .Take(maxRows)
                            .Reverse()
                            .ToList();
                    }

                    foreach (var msg in messagesToDisplay)
                    {
                        var parts = msg.Split(new[] { "]] " }, 2, StringSplitOptions.None);
                        if (parts.Length == 2)
                        {
                            var timestamp = parts[0].Replace("[[", "");
                            var message = parts[1];

                            try
                            {
                                table.AddRow(
                                    new Markup($"[dim]{timestamp}[/]"),
                                    new Markup(message)
                                );
                            }
                            catch
                            {
                                table.AddRow(
                                    new Text(timestamp),
                                    new Text(message)
                                );
                            }
                        }
                        else
                        {
                            try
                            {
                                table.AddRow(new Markup(""), new Markup(msg));
                            }
                            catch
                            {
                                table.AddRow(new Text(""), new Text(msg));
                            }
                        }
                    }

                    table.AddEmptyRow();
                    table.AddRow(
                        new Markup("[dim]Press Q to exit..[/]"),
                        new Markup($"[dim]Live updating... ({messagesToDisplay.Count} events)[/]")
                    );

                    ctx.UpdateTarget(table);
                    ctx.Refresh();

                    await Task.Delay(100);
                }
            });
    }

    private async Task TriggerNetworkScan()
    {
        RenderViewHeader("Network ICMP Scan");

        if (!_captureService.IsCapturing())
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Packet capture is not running. Start capture first.[/]");
            Console.WriteLine();
            WaitForExit("[dim]Press any key to return to menu..[/]");
            return;
        }

        var currentInterface = _captureService.GetCurrentInterface();
        if (string.IsNullOrEmpty(currentInterface))
        {
            AnsiConsole.MarkupLine("[yellow]⚠ No network interface selected.[/]");
            Console.WriteLine();
            WaitForExit("[dim]Press any key to return to menu..[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[dim]Interface:[/] [cyan]{currentInterface}[/]");
        AnsiConsole.MarkupLine("[dim]This will perform an active ICMP ping sweep of the local subnet.[/]");
        Console.WriteLine();

        var confirm = AnsiConsole.Confirm("Start network scan?", true);
        if (!confirm)
        {
            return;
        }

        Console.WriteLine();
        List<string> discoveredIPs = new();

        await AnsiConsole.Status()
            .StartAsync("Scanning network...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                ctx.SpinnerStyle(Style.Parse("cyan"));

                try
                {
                    discoveredIPs = await _captureService.ScanNetworkAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Network scan failed");
                }
            });

        Console.WriteLine();
        if (discoveredIPs.Count > 0)
        {
            AnsiConsole.MarkupLine($"[green]✓ Scan complete! Discovered {discoveredIPs.Count} responsive devices.[/]");
            Console.WriteLine();

            var table = new Table()
                .Border(TableBorder.Markdown)
                .BorderColor(Color.Grey37)
                .AddColumn("[cyan]IP Address[/]")
                .AddColumn("[cyan]Status[/]");

            foreach (var ip in discoveredIPs.Take(20))
            {
                table.AddRow(ip, "[green]✓ Responding[/]");
            }

            if (discoveredIPs.Count > 20)
            {
                table.AddRow("[dim]...[/]", $"[dim]({discoveredIPs.Count - 20} more)[/]");
            }

            AnsiConsole.Write(table);
            Console.WriteLine();
            AnsiConsole.MarkupLine("[dim]Discovered devices will appear in the devices list and network graph.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]⚠ No devices responded to ICMP ping.[/]");
            AnsiConsole.MarkupLine("[dim]This may be normal if devices have ICMP disabled or firewalls are blocking pings.[/]");
        }

        Console.WriteLine();
    }

    private void ShowStatistics()
    {
        RenderViewHeader("Network Statistics & Topology Analysis");

        var devices = _graphService.GetAllDevices();
        var connections = _graphService.GetAllConnections();
        var peers = _peerService?.GetDiscoveredPeers() ?? new List<TLSPeer>();

        var totalPackets = devices.Sum(d => d.PacketCount);
        var totalBytes = devices.Sum(d => d.BytesTransferred);
        var activeDevices = devices.Count(d => d.IsActive);
        var inactiveDevices = devices.Count - activeDevices;
        var connectedPeers = peers.Count(p => p.IsConnected);
        var activeConnections = connections.Count(c => c.IsActive);

        var vertexCut = CalculateVertexCut(devices, connections);
        var avgConnectionsPerDevice = devices.Count > 0 ? connections.Count / (double)devices.Count : 0;
        var (mostConnectedDevice, mostConnectedCount) = GetMostConnectedDevice(devices, connections);

        var gateways = devices.Where(d => d.IsGateway).ToList();
        var defaultGateway = devices.FirstOrDefault(d => d.IsDefaultGateway);
        var directL2Connections = connections.Count(c => c.Type == ConnectionType.DirectL2);
        var routedL3Connections = connections.Count(c => c.Type == ConnectionType.RoutedL3);
        var internetConnections = connections.Count(c => c.Type == ConnectionType.Internet);
        var tlsPeerConnections = connections.Count(c => c.Type == ConnectionType.TLSPeer);
        var connectionsWithTTL = connections.Where(c => c.AverageTTL.HasValue).ToList();
        var avgTTL = connectionsWithTTL.Any() ? connectionsWithTTL.Average(c => c.AverageTTL!.Value) : 0;

        var layoutMode = GetLayoutMode();

        if (layoutMode == LayoutMode.TwoColumn)
        {

            var statsContent = new Grid()
                .AddColumn()
                .AddColumn()
                .AddRow("[bold white]Device Statistics[/]", "")
                .AddRow("[default]Total Devices:[/]", devices.Count.ToString())
                .AddRow("[default]Active Devices:[/]", $"[green]{activeDevices}[/]")
                .AddRow("[default]Inactive Devices:[/]", $"[dim]{inactiveDevices}[/]")
                .AddRow("", "")
                .AddRow("[bold white]Connection Statistics[/]", "")
                .AddRow("[default]Total Connections:[/]", connections.Count.ToString())
                .AddRow("[default]Active Connections:[/]", $"[green]{activeConnections}[/]")
                .AddRow("[default]Avg Connections/Device:[/]", $"{avgConnectionsPerDevice:F2}")
                .AddRow("", "")
                .AddRow("[bold white]Traffic Statistics[/]", "")
                .AddRow("[default]Total Packets:[/]", totalPackets.ToString("N0"))
                .AddRow("[default]Total Bytes:[/]", FormatBytes(totalBytes))
                .AddRow("", "")
                .AddRow("[bold white]Peer Statistics[/]", "")
                .AddRow("[default]TLS Peers Discovered:[/]", peers.Count.ToString())
                .AddRow("[default]Connected Peers:[/]", $"[green]{connectedPeers}[/]")
                .AddRow("", "")
                .AddRow("[bold white]Topology Analysis[/]", "")
                .AddRow("[default]Gateway Devices:[/]", gateways.Count.ToString())
                .AddRow("[default]Default Gateway:[/]", defaultGateway != null
                    ? $"[yellow]{defaultGateway.IPAddress}[/]"
                    : "[dim]Not detected[/]")
                .AddRow("[default]Direct L2 Connections:[/]", directL2Connections > 0 ? $"[green]{directL2Connections}[/]" : "0")
                .AddRow("[default]Routed L3 Connections:[/]", routedL3Connections > 0 ? $"[cyan]{routedL3Connections}[/]" : "0")
                .AddRow("[default]Internet Connections:[/]", internetConnections > 0 ? $"[blue]{internetConnections}[/]" : "0")
                .AddRow("[default]TLS Peer Connections:[/]", tlsPeerConnections > 0 ? $"[green]{tlsPeerConnections}[/]" : "0")
                .AddRow("[default]Avg TTL (tracked):[/]", connectionsWithTTL.Any() ? $"{avgTTL:F1}" : "N/A")
                .AddRow("", "")
                .AddRow("[bold white]Graph Analysis[/]", "")
                .AddRow("[default]Vertex Cut (Min):[/]", vertexCut.ToString())
                .AddRow("[default]Most Connected Node:[/]", mostConnectedDevice != null
                    ? $"{mostConnectedDevice.Hostname ?? mostConnectedDevice.IPAddress} ({mostConnectedCount})"
                    : "N/A")
                .AddRow("[default]Capture Status:[/]",
                    _captureService.IsCapturing() ? "[green]Running[/]" : "[yellow]Stopped[/]");

            var topologyContent = RenderDirectedGraphContent(devices, connections);

            var matrixContent = RenderConnectionMatrixContent(devices, connections);

            var leftColumnGrid = new Grid()
                .AddColumn();
            leftColumnGrid.AddRow(new Markup("[bold white]Network Topology[/]"));
            leftColumnGrid.AddRow(topologyContent);
            leftColumnGrid.AddRow(new Text(""));
            leftColumnGrid.AddRow(new Markup("[bold white]Connection Matrix[/]"));
            leftColumnGrid.AddRow(matrixContent);

            var unifiedTable = new Table()
                .Border(TableBorder.Markdown)
                .BorderColor(Color.Grey37)
                .HideHeaders()
                .Expand();

            unifiedTable.AddColumn(new TableColumn(""));
            unifiedTable.AddColumn(new TableColumn(""));

            unifiedTable.AddRow(
                leftColumnGrid,
                statsContent
            );

            AnsiConsole.Write(unifiedTable);
        }
        else
        {
            var statsGrid = new Grid()
                .AddColumn()
                .AddColumn()
                .AddRow("[bold]Devices[/]", "")
                .AddRow("[default]Total:[/]", devices.Count.ToString())
                .AddRow("[default]Active:[/]", $"[green]{activeDevices}[/]")
                .AddRow("[default]Inactive:[/]", $"[dim]{inactiveDevices}[/]")
                .AddRow("", "")
                .AddRow("[bold]Connections[/]", "")
                .AddRow("[default]Total:[/]", connections.Count.ToString())
                .AddRow("[default]Active:[/]", $"[green]{activeConnections}[/]")
                .AddRow("[default]Avg/Device:[/]", $"{avgConnectionsPerDevice:F2}")
                .AddRow("", "")
                .AddRow("[bold]Traffic[/]", "")
                .AddRow("[default]Packets:[/]", totalPackets.ToString("N0"))
                .AddRow("[default]Bytes:[/]", FormatBytes(totalBytes))
                .AddRow("", "")
                .AddRow("[bold]Topology[/]", "")
                .AddRow("[default]Gateways:[/]", gateways.Count > 0 ? $"[yellow]{gateways.Count}[/]" : "0")
                .AddRow("[default]L2/L3/Inet/Peer:[/]",
                    $"{directL2Connections}/{routedL3Connections}/{internetConnections}/{tlsPeerConnections}")
                .AddRow("[default]Avg TTL:[/]", connectionsWithTTL.Any() ? $"{avgTTL:F1}" : "N/A")
                .AddRow("", "")
                .AddRow("[bold]Graph[/]", "")
                .AddRow("[default]Vertex Cut:[/]", vertexCut.ToString())
                .AddRow("[default]Most Connected:[/]", mostConnectedDevice != null
                    ? $"{mostConnectedDevice.Hostname ?? mostConnectedDevice.IPAddress}"
                    : "N/A")
                .AddRow("[default]Status:[/]",
                    _captureService.IsCapturing() ? "[green]Running[/]" : "[yellow]Stopped[/]");

            var statsPanel = new Panel(statsGrid)
            {
                Header = new PanelHeader("[default]Statistics[/]"),
                Border = BoxBorder.Ascii
            };

            AnsiConsole.Write(statsPanel);
            AnsiConsole.WriteLine();

            var directedGraph = NetworkGraphUtility.RenderCompactDirectedGraph(devices, connections, _currentUser);
            AnsiConsole.Write(directedGraph);
            AnsiConsole.WriteLine();

            var connectionMatrix = NetworkGraphUtility.RenderConnectionMatrix(devices, connections);
            AnsiConsole.Write(connectionMatrix);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Vertex Cut: Minimum nodes to remove to disconnect the network. Higher = better resilience.[/]");
        AnsiConsole.MarkupLine($"[dim]Directed Graph: Connection flow with direction (↓inbound ↑outbound) and strength (→ weak ⇒ medium ⇛ strong).[/]");
        AnsiConsole.MarkupLine($"[dim]Connection Matrix: Packet intensity heat map between devices (░ ▒ ▓ █).[/]");
        AnsiConsole.MarkupLine($"[dim]Connection Types: L2=Direct Layer 2 (same segment), L3=Routed Layer 3 (via gateway), Inet=Internet, Peer=TLS Peer[/]");

        AnsiConsole.WriteLine(); 
    }

    private void ShowFullscreenNetworkGraph()
    {
        RenderViewHeader("Network Topology Graph (Fullscreen)");

        var devices = _graphService.GetAllDevices();
        var connections = _graphService.GetAllConnections();

        if (devices.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No devices discovered yet.[/]");
            return;
        }

        int availableHeight = Math.Max(20, Console.WindowHeight - 15);

        var topologyContent = NetworkGraphUtility.RenderSimpleTopology(devices, connections, _currentUser, heightOverride: availableHeight, showAllDevices: true, windowWidth: _lastWindowWidth, isFullscreen: true);
        AnsiConsole.Write(topologyContent);

        AnsiConsole.MarkupLine($"[dim]Total Devices: [/][cyan]{devices.Count}[/][dim]  |  Connections: [/][cyan]{connections.Count}[/][dim]  |  TLS Peers: [/][green]{devices.Count(d => d.IsTLScopePeer)}[/]");

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Calculate vertex cut of network graph
    /// </summary>
    private int CalculateVertexCut(List<Device> devices, List<Connection> connections)
    {
        if (devices.Count <= 1)
            return 0;

        var adjacencyList = new Dictionary<string, HashSet<string>>();
        foreach (var device in devices.Where(d => d.IsActive))
        {
            adjacencyList[device.MACAddress] = new HashSet<string>();
        }

        foreach (var conn in connections.Where(c => c.IsActive))
        {
            var source = conn.SourceDevice.MACAddress;
            var dest = conn.DestinationDevice.MACAddress;

            if (adjacencyList.ContainsKey(source) && adjacencyList.ContainsKey(dest))
            {
                adjacencyList[source].Add(dest);
                adjacencyList[dest].Add(source);
            }
        }

        if (!IsGraphConnected(adjacencyList))
            return 0;

        if (adjacencyList.Count == 0)
            return 0;

        var minDegree = adjacencyList.Values.Min(neighbors => neighbors.Count);

        if (minDegree == 0)
            return 0;

        return minDegree;
    }

    /// <summary>
    /// Check if a graph is connected using BFS
    /// </summary>
    private bool IsGraphConnected(Dictionary<string, HashSet<string>> adjacencyList)
    {
        if (adjacencyList.Count == 0)
            return true;

        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        var start = adjacencyList.Keys.First();

        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var neighbor in adjacencyList[current])
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return visited.Count == adjacencyList.Count;
    }

    #region Local Network Helpers

    /// <summary>
    /// Cache local device information
    /// </summary>
    private class LocalDeviceInfo
    {
        private HashSet<string> _localIPs = new();
        private HashSet<string> _localMACs = new();
        private string _primaryIP = "N/A";
        private string _primaryMAC = "N/A";
        private DateTime _lastRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Ensure cache is fresh
        /// </summary>
        private void EnsureFresh()
        {
            if (DateTime.UtcNow - _lastRefresh > _cacheLifetime)
            {
                Refresh();
            }
        }

        /// <summary>
        /// Force refresh of local network information
        /// </summary>
        public void Refresh()
        {
            _localIPs = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Select(addr => addr.Address.ToString())
                .ToHashSet();

            _localMACs = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                .Where(ni => ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                .Select(ni => string.Join(":", ni.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2"))))
                .Where(mac => !string.IsNullOrEmpty(mac))
                .ToHashSet();

            var activeInterface = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                .Where(ni => ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                .FirstOrDefault();

            if (activeInterface != null)
            {
                _primaryIP = activeInterface.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    ?.Address.ToString() ?? "N/A";

                _primaryMAC = string.Join(":", activeInterface.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
            }
            else
            {
                _primaryIP = "N/A";
                _primaryMAC = "N/A";
            }

            _lastRefresh = DateTime.UtcNow;
        }

        public HashSet<string> GetLocalIPs()
        {
            EnsureFresh();
            return _localIPs;
        }

        public HashSet<string> GetLocalMACs()
        {
            EnsureFresh();
            return _localMACs;
        }

        public bool IsLocalIP(string ipAddress)
        {
            EnsureFresh();
            return _localIPs.Contains(ipAddress);
        }

        public bool IsLocalMAC(string macAddress)
        {
            EnsureFresh();
            return _localMACs.Contains(macAddress);
        }

        public (string ip, string mac) GetPrimaryIPAndMAC()
        {
            EnsureFresh();
            return (_primaryIP, _primaryMAC);
        }
    }

    private LocalDeviceInfo? _localDeviceInfo;

    private LocalDeviceInfo GetLocalDeviceInfoCache()
    {
        if (_localDeviceInfo == null)
        {
            _localDeviceInfo = new LocalDeviceInfo();
            _localDeviceInfo.Refresh();
        }
        return _localDeviceInfo;
    }

    private (string ip, string mac) GetLocalIPAndMAC()
    {
        return GetLocalDeviceInfoCache().GetPrimaryIPAndMAC();
    }

    private HashSet<string> GetLocalIPs()
    {
        return GetLocalDeviceInfoCache().GetLocalIPs();
    }

    private HashSet<string> GetLocalMACs()
    {
        return GetLocalDeviceInfoCache().GetLocalMACs();
    }

    private bool IsLocalIP(string ipAddress)
    {
        return GetLocalDeviceInfoCache().IsLocalIP(ipAddress);
    }

    private bool IsLocalMAC(string macAddress)
    {
        return GetLocalDeviceInfoCache().IsLocalMAC(macAddress);
    }

    #endregion

    private void LoadExclusionsConfig()
    {
        try
        {
            if (File.Exists(ExclusionsConfigFile))
            {
                var json = File.ReadAllText(ExclusionsConfigFile);
                var config = System.Text.Json.JsonSerializer.Deserialize<ExclusionsConfig>(json);

                if (config != null)
                {
                    _excludedIPs = config.ExcludedIPs?.ToHashSet() ?? new();
                    _excludedHostnames = config.ExcludedHostnames?.ToHashSet() ?? new();
                    _excludedMACs = config.ExcludedMACs?.ToHashSet() ?? new();
                    Log.Information($"Loaded exclusions: {_excludedIPs.Count} IPs, {_excludedHostnames.Count} hostnames, {_excludedMACs.Count} MACs");
                }
            }
            else
            {
                SaveExclusionsConfig();
                Log.Information("Created default exclusions config file");
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to load exclusions config: {ex.Message}");
        }
    }

    private void SaveExclusionsConfig()
    {
        try
        {
            var config = new ExclusionsConfig
            {
                ExcludedIPs = _excludedIPs.ToList(),
                ExcludedHostnames = _excludedHostnames.ToList(),
                ExcludedMACs = _excludedMACs.ToList()
            };

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(config, options);
            File.WriteAllText(ExclusionsConfigFile, json);
            Log.Information("Saved exclusions configuration");
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to save exclusions config: {ex.Message}");
        }
    }

    private bool IsExcludedDevice(Device device)
    {
        if (!string.IsNullOrEmpty(device.IPAddress) && _excludedIPs.Contains(device.IPAddress))
            return true;

        if (!string.IsNullOrEmpty(device.Hostname) && _excludedHostnames.Contains(device.Hostname))
            return true;

        if (!string.IsNullOrEmpty(device.MACAddress) && _excludedMACs.Contains(device.MACAddress))
            return true;

        return false;
    }

    private void OnDeviceDiscovered(object? sender, Device device)
    {
        bool isHostDevice = IsLocalIP(device.IPAddress) || IsLocalMAC(device.MACAddress);

        if (isHostDevice)
        {
            device.DeviceName = "This Device (Host)";
            Log.Debug($"Host device detected: {device.IPAddress} / {device.MACAddress}");
        }

        if (IsExcludedDevice(device))
        {
            Log.Debug($"Skipping excluded device: {device.IPAddress} / {device.Hostname}");
            return;
        }

        _graphService.AddDevice(device);
        AddLogMessage($"Device discovered: {device.IPAddress} ({device.DeviceName ?? "Unknown"})");

        lock (_refreshLock)
        {
            _dashboardNeedsRefresh = true;
        }

        if (!isHostDevice && _peerService != null && IPAddress.TryParse(device.IPAddress, out var ipAddress))
        {
            _peerService.ProbeDevice(ipAddress);
            Log.Debug($"Probing device {device.IPAddress} for TLScope peer discovery");
        }
    }

    private void OnConnectionDetected(object? sender, Connection connection)
    {
        if (IsExcludedDevice(connection.SourceDevice) || IsExcludedDevice(connection.DestinationDevice))
        {
            return;
        }

        _graphService.AddConnection(connection);
    }

    private void OnDeviceAdded(object? sender, Device device)
    {
    }

    private void OnConnectionAdded(object? sender, Connection connection)
    {
        AddLogMessage($"Connection: {connection.SourceDevice.IPAddress} → {connection.DestinationDevice.IPAddress}");

        lock (_refreshLock)
        {
            _dashboardNeedsRefresh = true;
        }
    }

    private void OnGatewayDetected(object? sender, Device gateway)
    {
        AddLogMessage($"Gateway detected: {gateway.IPAddress} ({gateway.GatewayRole})");

        lock (_refreshLock)
        {
            _dashboardNeedsRefresh = true;
        }

        Log.Information($"[TOPOLOGY] Gateway device marked: {gateway.IPAddress} ({gateway.MACAddress})");
    }

    private void OnPeerDiscovered(object? sender, TLSPeer peer)
    {
        AddLogMessage($"Peer discovered: {peer.Username} ({peer.IPAddress})");

        lock (_refreshLock)
        {
            _dashboardNeedsRefresh = true;
        }

        var existingDevice = _graphService.GetAllDevices()
            .FirstOrDefault(d => d.IPAddress == peer.IPAddress);

        if (existingDevice != null)
        {
            existingDevice.IsTLScopePeer = true;
            existingDevice.TLSPeerId = peer.Id;
            existingDevice.TLSPeer = peer;
            existingDevice.DeviceName = peer.Username;

            _graphService.UpdateDevice(existingDevice);
            AddLogMessage($"Linked peer {peer.Username} to existing device");
        }
        else
        {
            var newDevice = new Device
            {
                IPAddress = peer.IPAddress,
                MACAddress = $"PEER:{peer.Username}", // Placeholder MAC for peers
                DeviceName = peer.Username,
                Hostname = peer.Username,
                IsTLScopePeer = true,
                TLSPeerId = peer.Id,
                TLSPeer = peer,
                FirstSeen = peer.FirstSeen,
                LastSeen = DateTime.UtcNow
            };

            _graphService.AddDevice(newDevice);
            AddLogMessage($"Added peer {peer.Username} as new device");
        }
    }

    private void OnPeerConnected(object? sender, TLSPeer peer)
    {
        AddLogMessage($"Peer connected: {peer.Username}");
    }

    private void OnLogMessage(object? sender, string message)
    {
        AddLogMessage(message);
    }

    private void AddLogMessage(string message)
    {
        lock (_logLock)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var escapedMessage = message.Replace("[", "[[").Replace("]", "]]");
            _logMessages.Insert(0, $"[[{timestamp}]] {escapedMessage}");

            if (_logMessages.Count > 100)
            {
                _logMessages.RemoveAt(_logMessages.Count - 1);
            }
        }
    }

    /// <summary>
    /// Render dashboard with adaptive layout
    /// </summary>
    private Panel RenderCombinedInfoTable()
    {
        var devices = _graphService.GetAllDevices();
        var connections = _graphService.GetAllConnections();

        if (_filteredDevice != null)
        {
            connections = connections
                .Where(c => c.SourceDevice.MACAddress == _filteredDevice.MACAddress ||
                           c.DestinationDevice.MACAddress == _filteredDevice.MACAddress)
                .ToList();
        }

        var layoutMode = GetLayoutMode();

        var deviceTree = RenderDeviceTree();

        var compactEvents = RenderCompactRecentEvents();

        if (layoutMode == LayoutMode.TwoColumn)
        {

            var topologyContent = NetworkGraphUtility.RenderSimpleTopology(devices, connections, _currentUser, heightOverride: 15, showAllDevices: true, windowWidth: _lastWindowWidth);

            var leftGrid = new Grid().AddColumn();
            leftGrid.AddRow(topologyContent);
            leftGrid.AddRow(new Text(""));
            leftGrid.AddRow(compactEvents);
            leftGrid.AddRow(new Text(""));

            var leftPanel = new Panel(leftGrid)
            {
                Border = BoxBorder.None,
                Padding = new Padding(0, 0, 1, 0)
            };

            var rightGrid = new Grid().AddColumn();
            rightGrid.AddRow(deviceTree);

            var rightPanel = new Panel(rightGrid)
            {
                Border = BoxBorder.None,
                Padding = new Padding(1, 0, 0, 0)
            };

            var columns = new Columns(leftPanel, rightPanel);

            return new Panel(columns)
            {
                Border = BoxBorder.None,
                BorderStyle = new Style(Color.Default),
                Expand = true,
                Padding = new Padding(1, 0, 1, 0)
            };
        }
        else
        {

            var topologyContent = NetworkGraphUtility.RenderSimpleTopology(devices, connections, _currentUser, showAllDevices: true, windowWidth: _lastWindowWidth);

            var mainGrid = new Grid()
                .AddColumn();

            mainGrid.AddRow(topologyContent);              // Network topology with user in center
            mainGrid.AddRow(new Text(""));                 // Spacer
            mainGrid.AddRow(deviceTree);                   // Device list (no border)
            mainGrid.AddRow(new Text(""));                 // Spacer
            mainGrid.AddRow(compactEvents);                // Recent events at bottom
            mainGrid.AddRow(new Text(""));                 // Spacer

            return new Panel(mainGrid)
            {
                Border = BoxBorder.None,
                BorderStyle = new Style(Color.Grey37),
                Padding = new Padding(1, 0, 1, 0)
            };
        }
    }

    /// <summary>
    /// Get recent events content as list of strings
    /// </summary>
    private List<string> GetRecentEventsContent()
    {
        var lines = new List<string>();

        lock (_logLock)
        {
            var recentEvents = _logMessages.Take(7).ToList();

            if (recentEvents.Count == 0)
            {
                lines.Add("[dim]No recent events[/]");
            }
            else
            {
                foreach (var msg in recentEvents)
                {
                    lines.Add($"[dim]{msg}[/]");
                }
            }
        }

        return lines;
    }


    /// <summary>
    /// Get user info content as list of strings
    /// </summary>
    private List<string> GetUserInfoContent()
    {
        var lines = new List<string>();

        if (_currentUser == null)
        {
            lines.Add("[dim]No user logged in[/]");
            return lines;
        }

        var avatarLines = _userService.GetUserAvatar(_currentUser);

        var currentInterface = _captureService.GetCurrentInterface() ?? "None";

        var (localIP, localMAC) = GetLocalIPAndMAC();

        var sshKeyDisplay = GetSSHKeyDisplay(withMarkup: true);

        var avatarColorHex = _currentUser.AvatarColor;

        foreach (var line in avatarLines)
        {
            var escapedLine = line.Replace("[", "[[").Replace("]", "]]");
            lines.Add($"[{avatarColorHex}]{escapedLine}[/]");
        }

        lines.Add("");
        lines.Add($"user: [bold]{_currentUser.Username}[/]");
        lines.Add($"ssh: {sshKeyDisplay}");
        lines.Add($"interface: [cyan]{currentInterface}[/]");
        lines.Add($"ip: [dim]{localIP}[/]");
        lines.Add($"mac: [dim]{localMAC}[/]");

        return lines;
    }

    /// <summary>
    /// Render device table with status-based grouping
    /// </summary>
    private Table RenderDeviceTree()
    {
        var devices = _graphService.GetAllDevices();
        var connections = _graphService.GetAllConnections();

        var table = new Table()
            .Border(TableBorder.Markdown)
            .BorderColor(Color.Grey37)
            .AddColumn(new TableColumn("[bold]Device[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Packets[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Bytes[/]").RightAligned());

        table.Title = new TableTitle($"[bold cyan]Network Devices[/] [dim]({devices.Count} total)[/]");

        var grouped = GroupDevicesByType(devices, connections);
        var gateways = grouped.Gateways;
        var peers = grouped.Peers;
        var activeLocal = grouped.ActiveLocal;
        var activeRemote = grouped.ActiveRemote;
        var inactive = grouped.Inactive;

        const int maxDeviceRows = 32;
        int rowsUsed = 0;

        if (gateways.Any())
        {
            table.AddRow(new Markup("[yellow bold]◆ Gateways[/]"), new Text(""), new Text(""));
            rowsUsed++;

            int gatewaysToShow = Math.Min(gateways.Count, maxDeviceRows - rowsUsed);
            for (int i = 0; i < gatewaysToShow; i++)
            {
                var gateway = gateways[i];
                var deviceName = GetDeviceName(gateway);
                var (icon, color) = GetDeviceIconAndColor(gateway, connections);
                var highlight = _highlightedDeviceMACs.Contains(gateway.MACAddress) ? "[yellow]★[/] " : "";
                table.AddRow(
                    new Markup($"[{color}]  {icon} {highlight}{Markup.Escape(deviceName)}[/]"),
                    new Text(FormatNumber(gateway.PacketCount)),
                    new Text(FormatBytes(gateway.BytesTransferred))
                );
                rowsUsed++;
            }
        }

        if (peers.Any() && rowsUsed < maxDeviceRows)
        {
            if (gateways.Any())
            {
                table.AddEmptyRow();
                rowsUsed++;
            }

            table.AddRow(new Markup("[green bold]◉ TLS Peers[/]"), new Text(""), new Text(""));
            rowsUsed++;

            int peersToShow = Math.Min(peers.Count, maxDeviceRows - rowsUsed);
            for (int i = 0; i < peersToShow; i++)
            {
                var peer = peers[i];
                var deviceName = GetDeviceName(peer);
                var (icon, color) = GetDeviceIconAndColor(peer, connections);
                var highlight = _highlightedDeviceMACs.Contains(peer.MACAddress) ? "[yellow]★[/] " : "";
                table.AddRow(
                    new Markup($"[{color}]  {icon} {highlight}{Markup.Escape(deviceName)}[/]"),
                    new Text(FormatNumber(peer.PacketCount)),
                    new Text(FormatBytes(peer.BytesTransferred))
                );
                rowsUsed++;
            }
        }

        if (activeLocal.Any() && rowsUsed < maxDeviceRows)
        {
            if (gateways.Any() || peers.Any())
            {
                table.AddEmptyRow();
                rowsUsed++;
            }

            table.AddRow(new Markup("[cyan bold]● Active Local[/]"), new Text(""), new Text(""));
            rowsUsed++;

            int activeToShow = Math.Min(activeLocal.Count, maxDeviceRows - rowsUsed);
            for (int i = 0; i < activeToShow; i++)
            {
                var device = activeLocal[i];
                var deviceName = GetDeviceName(device);
                var (icon, color) = GetDeviceIconAndColor(device, connections);
                var highlight = _highlightedDeviceMACs.Contains(device.MACAddress) ? "[yellow]★[/] " : "";
                table.AddRow(
                    new Markup($"[{color}]  {icon} {highlight}{Markup.Escape(deviceName)}[/]"),
                    new Text(FormatNumber(device.PacketCount)),
                    new Text(FormatBytes(device.BytesTransferred))
                );
                rowsUsed++;
            }
        }

        if (activeRemote.Any() && rowsUsed < maxDeviceRows)
        {
            if (gateways.Any() || peers.Any() || activeLocal.Any())
            {
                table.AddEmptyRow();
                rowsUsed++;
            }

            table.AddRow(new Markup("[orange1 bold]◍ Active Remote[/]"), new Text(""), new Text(""));
            rowsUsed++;

            int remoteToShow = Math.Min(activeRemote.Count, maxDeviceRows - rowsUsed);
            for (int i = 0; i < remoteToShow; i++)
            {
                var device = activeRemote[i];
                var deviceName = GetDeviceName(device);
                var (icon, color) = GetDeviceIconAndColor(device, connections);
                var highlight = _highlightedDeviceMACs.Contains(device.MACAddress) ? "[yellow]★[/] " : "";
                table.AddRow(
                    new Markup($"[{color}]  {icon} {highlight}{Markup.Escape(deviceName)}[/]"),
                    new Text(FormatNumber(device.PacketCount)),
                    new Text(FormatBytes(device.BytesTransferred))
                );
                rowsUsed++;
            }
        }

        if (inactive.Any() && rowsUsed < maxDeviceRows)
        {
            if (gateways.Any() || peers.Any() || activeLocal.Any() || activeRemote.Any())
            {
                table.AddEmptyRow();
                rowsUsed++;
            }

            table.AddRow(new Markup("[grey37 bold]○ Inactive[/]"), new Text(""), new Text(""));
            rowsUsed++;

            int inactiveToShow = Math.Min(inactive.Count, maxDeviceRows - rowsUsed);
            for (int i = 0; i < inactiveToShow; i++)
            {
                var device = inactive[i];
                var deviceName = GetDeviceName(device);
                var (icon, color) = GetDeviceIconAndColor(device, connections);
                var highlight = _highlightedDeviceMACs.Contains(device.MACAddress) ? "[yellow]★[/] " : "";
                table.AddRow(
                    new Markup($"[{color}]  {icon} {highlight}{Markup.Escape(deviceName)}[/]"),
                    new Text(FormatNumber(device.PacketCount)),
                    new Text(FormatBytes(device.BytesTransferred))
                );
                rowsUsed++;
            }
        }

        return table;
    }

    #region Device Helper Methods

    /// <summary>
    /// Container for devices grouped by type
    /// </summary>
    private class GroupedDevices
    {
        public List<Device> Gateways { get; set; } = new();
        public List<Device> Peers { get; set; } = new();
        public List<Device> ActiveLocal { get; set; } = new();
        public List<Device> ActiveRemote { get; set; } = new();
        public List<Device> Inactive { get; set; } = new();
    }

    /// <summary>
    /// Group and sort devices by type (Gateways → Peers → Active Local → Active Remote → Inactive)
    /// </summary>
    private GroupedDevices GroupDevicesByType(List<Device> devices, List<Connection> connections)
    {
        return new GroupedDevices
        {
            Gateways = devices.Where(d => d.IsGateway)
                .OrderByDescending(d => d.IsDefaultGateway)
                .ThenByDescending(d => d.LastSeen).ToList(),
            Peers = devices.Where(d => d.IsTLScopePeer && !d.IsGateway)
                .OrderByDescending(d => d.LastSeen).ToList(),
            ActiveLocal = devices.Where(d => d.IsActiveHybrid(connections) && !d.IsTLScopePeer && !d.IsGateway && d.IsLocal && !d.IsVirtualDevice)
                .OrderByDescending(d => d.LastSeen).ToList(),
            ActiveRemote = devices.Where(d => d.IsActiveHybrid(connections) && !d.IsTLScopePeer && !d.IsGateway && (d.IsVirtualDevice || !d.IsLocal))
                .OrderByDescending(d => d.LastSeen).ToList(),
            Inactive = devices.Where(d => !d.IsActiveHybrid(connections) && !d.IsTLScopePeer && !d.IsGateway)
                .OrderByDescending(d => d.LastSeen).ToList()
        };
    }

    /// <summary>
    /// Get device icon symbol and color based on type
    /// Uses backend IsActiveHybrid logic (time OR connections)
    /// </summary>
    private (string icon, string color) GetDeviceIconAndColor(Device device, List<Connection> connections)
    {
        bool isActive = device.IsActiveHybrid(connections);

        if (device.IsTLScopePeer)
            return (isActive ? "◉" : "○", "green");

        if (device.IsGateway)
            return (isActive ? (device.IsDefaultGateway ? "◆" : "◇") : "○", "yellow");

        if (device.IsVirtualDevice || !device.IsLocal)
            return (isActive ? "◍" : "○", "orange1");

        return (isActive ? "●" : "○", "cyan");
    }

    /// <summary>
    /// Get status markup using backend hybrid logic (time OR connections)
    /// </summary>
    private string GetDeviceStatus(Device device, List<Connection> connections)
    {
        bool isActive = device.IsActiveHybrid(connections);
        return isActive ? "[green]Active[/]" : "[grey37]Inactive[/]";
    }

    /// <summary>
    /// Get status markup using backend connection IsActive property
    /// </summary>
    private string GetConnectionStatus(Connection connection)
    {
        return connection.IsActive ? "[green]Active[/]" : "[grey37]Inactive[/]";
    }

    /// <summary>
    /// Get device type label with markup
    /// </summary>
    private string GetDeviceTypeLabel(Device device)
    {
        if (device.IsTLScopePeer) return "[green]TLS Peer[/]";
        if (device.IsDefaultGateway) return "[yellow]Default Gateway[/]";
        if (device.IsGateway) return "[yellow]Gateway[/]";
        if (device.IsVirtualDevice) return "[orange1]Remote/Internet[/]";
        if (device.IsLocal) return "[cyan]Local Device[/]";
        return "[dim]Unknown[/]";
    }

    /// <summary>
    /// Get device name (DeviceName > Hostname > IPAddress > fallback)
    /// </summary>
    private string GetDeviceName(Device device, string fallback = "Unknown")
    {
        return device.DeviceName ?? device.Hostname ?? device.IPAddress ?? fallback;
    }

    #endregion

    #region View Rendering Helpers

    /// <summary>
    /// Render the standard view header (TLScope title + rule with view name)
    /// </summary>
    private void RenderViewHeader(string title)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]TLScope[/][dim] Network Security Visualization Tool[/]");
        AnsiConsole.Write(new Rule($"[white]{title}[/]").RuleStyle(Style.Parse("grey37")));
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Render an IRenderable (Table, Markup, Rule, etc.) to a string with ANSI formatting
    /// </summary>
    private string RenderToString(IRenderable renderable)
    {
        var stringWriter = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.TrueColor,
            Out = new AnsiConsoleOutput(stringWriter),
            Interactive = InteractionSupport.No
        });

        try
        {
            console.Profile.Width = Console.WindowWidth;
        }
        catch
        {
            console.Profile.Width = 80; // Fallback width
        }

        console.Write(renderable);
        return stringWriter.ToString();
    }

    #region Formatting Helpers

    /// <summary>
    /// Format number with K/M/B suffixes
    /// </summary>
    private string FormatNumber(long number)
    {
        if (number < 1000)
            return number.ToString();
        else if (number < 1_000_000)
            return $"{number / 1000.0:0.#}K";
        else if (number < 1_000_000_000)
            return $"{number / 1_000_000.0:0.#}M";
        else
            return $"{number / 1_000_000_000.0:0.#}B";
    }

    /// <summary>
    /// Format timestamp as relative time (e.g., "2m ago", "30s ago")
    /// </summary>
    private string FormatRelativeTime(DateTime timestamp, bool useColor = true)
    {
        var elapsed = DateTime.UtcNow - timestamp;

        if (elapsed.TotalSeconds < 5)
            return useColor ? "[green]now[/]" : "now";
        else if (elapsed.TotalSeconds < 60)
            return useColor ? $"[green]{(int)elapsed.TotalSeconds}s ago[/]" : $"{(int)elapsed.TotalSeconds}s ago";
        else if (elapsed.TotalMinutes < 60)
            return useColor ? $"[yellow]{(int)elapsed.TotalMinutes}m ago[/]" : $"{(int)elapsed.TotalMinutes}m ago";
        else if (elapsed.TotalHours < 24)
            return useColor ? $"[dim]{(int)elapsed.TotalHours}h ago[/]" : $"{(int)elapsed.TotalHours}h ago";
        else
            return useColor ? $"[dim]{(int)elapsed.TotalDays}d ago[/]" : $"{(int)elapsed.TotalDays}d ago";
    }

    /// <summary>
    /// Format bytes with B/KB/MB/GB/TB suffixes
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Format connection strength as a visual indicator using block characters
    /// Based on recent packet rate (packets per 30 seconds)
    /// </summary>
    private string FormatConnectionStrength(long packetCount)
    {
        if (packetCount == 0)
            return "[dim]·[/]";

        if (packetCount <= 2)
            return "[grey37]░[/]";
        else if (packetCount <= 10)
            return "[yellow]▒[/]";
        else if (packetCount <= 30)
            return "[orange1]▓[/]";
        else
            return "[red]█[/]";
    }

    #endregion

    #region Network Statistics Helpers

    /// <summary>
    /// Find the most connected device in the network
    /// </summary>
    private (Device? device, int connectionCount) GetMostConnectedDevice(List<Device> devices, List<Connection> connections)
    {
        var mostConnectedDevice = devices.OrderByDescending(d =>
            connections.Count(c => c.SourceDevice.MACAddress == d.MACAddress ||
                                   c.DestinationDevice.MACAddress == d.MACAddress)
        ).FirstOrDefault();

        var mostConnectedCount = mostConnectedDevice != null
            ? connections.Count(c => c.SourceDevice.MACAddress == mostConnectedDevice.MACAddress ||
                                     c.DestinationDevice.MACAddress == mostConnectedDevice.MACAddress)
            : 0;

        return (mostConnectedDevice, mostConnectedCount);
    }

    #endregion

    #region User/Avatar Helpers

    /// <summary>
    /// Get SSH key display string (truncated or "temporary")
    /// </summary>
    private string GetSSHKeyDisplay(bool withMarkup = true)
    {
        if (_currentUser == null)
            return withMarkup ? "[dim]none[/]" : "none";

        var isTemp = _currentUser.SSHPublicKey?.StartsWith("ssh-temp") == true;
        var key = _currentUser.SSHPublicKey;

        if (isTemp)
            return withMarkup ? "[dim]temporary[/]" : "temp";

        if (string.IsNullOrEmpty(key))
            return withMarkup ? "[dim]none[/]" : "none";

        var truncated = key.Substring(0, Math.Min(16, key.Length));
        return withMarkup ? $"[dim]{truncated}...[/]" : truncated;
    }

    /// <summary>
    /// Get avatar style for current user
    /// </summary>
    private Style GetAvatarStyle()
    {
        if (_currentUser == null)
            return Style.Plain;

        var avatarColor = AvatarUtility.HexToSpectreColor(_currentUser.AvatarColor);
        return new Style(avatarColor);
    }

    #endregion

    /// <summary>
    /// Render compact user info for bottom row
    /// </summary>
    private Table RenderCompactUserInfo()
    {
        if (_currentUser == null)
        {
            var emptyTable = new Table().Border(TableBorder.None).HideHeaders();
            emptyTable.AddColumn("");
            emptyTable.AddRow("[dim]No user logged in[/]");
            return emptyTable;
        }

        var avatarLines = _userService.GetUserAvatar(_currentUser);
        var avatarStyle = GetAvatarStyle();

        var (localIP, localMAC) = GetLocalIPAndMAC();
        var currentInterface = _captureService.GetCurrentInterface() ?? "None";

        var sshKeyDisplay = GetSSHKeyDisplay(withMarkup: false);

        var peers = _graphService.GetAllDevices().Count(d => d.IsTLScopePeer);

        var randomartLines = SSHRandomart.GenerateRandomart(_currentUser.SSHPublicKey ?? "");

        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("").Padding(0, 0, 6, 0))  // Avatar + Info column with right padding
            .AddColumn(new TableColumn("").Width(19)); // SSH art column (17 chars + 2 border)

        table.AddRow(
            new Text(avatarLines[0], avatarStyle),
            new Markup($"[default]{Markup.Escape(randomartLines[0])}[/]")
        );

        table.AddRow(
            new Text(avatarLines[1], avatarStyle),
            new Markup($"[default]{Markup.Escape(randomartLines[1])}[/]")
        );

        table.AddRow(
            new Text(avatarLines[2], avatarStyle),
            new Markup($"[default]{Markup.Escape(randomartLines[2])}[/]")
        );

        table.AddRow(
            new Text(avatarLines[3], avatarStyle),
            new Markup($"[default]{Markup.Escape(randomartLines[3])}[/]")
        );

        table.AddRow(
            new Text(""),
            new Markup($"[default]{Markup.Escape(randomartLines[4])}[/]")
        );

        table.AddRow(
            new Markup($"[bold]User:[/] {Markup.Escape(_currentUser.Username)}"),
            new Markup($"[default]{Markup.Escape(randomartLines[5])}[/]")
        );

        table.AddRow(
            new Markup($"[dim]SSH:[/] {Markup.Escape(sshKeyDisplay)}..."),
            new Markup($"[default]{Markup.Escape(randomartLines[6])}[/]")
        );

        table.AddRow(
            new Markup($"[dim]Interface:[/] [cyan]{Markup.Escape(currentInterface)}[/]"),
            new Markup($"[default]{Markup.Escape(randomartLines[7])}[/]")
        );

        table.AddRow(
            new Markup($"[dim]IP:[/] {Markup.Escape(localIP)}"),
            new Markup($"[default]{Markup.Escape(randomartLines[8])}[/]")
        );

        table.AddRow(
            new Markup($"[dim]MAC:[/] {Markup.Escape(localMAC)}"),
            new Markup($"[default]{Markup.Escape(randomartLines[9])}[/]")
        );

        table.AddRow(
            new Markup($"[dim]Status:[/] [green]Active[/] | [dim]Peers:[/] {peers}"),
            new Markup($"[default]{Markup.Escape(randomartLines[10])}[/]")
        );

        return table;
    }

    /// <summary>
    /// Render compact recent events for bottom row (chatbox format - 7 lines, NO border)
    /// </summary>
    private Markup RenderCompactRecentEvents()
    {
        lock (_logLock)
        {
            var recentEvents = _logMessages.Take(7).ToList();

            if (recentEvents.Count == 0)
            {
                return new Markup(string.Join("\n", Enumerable.Repeat("[dim]No recent events[/]", 7)));
            }

            var dimEvents = recentEvents.Select(msg => $"[dim]{msg}[/]").ToList();

            while (dimEvents.Count < 7)
            {
                dimEvents.Add("[dim]...[/]");
            }

            var eventsText = string.Join("\n", dimEvents);

            return new Markup(eventsText);
        }
    }

    /// <summary>
    /// Render recent events panel for display in main menu
    /// </summary>
    private Panel RenderRecentEventsPanel()
    {
        lock (_logLock)
        {
            var recentEvents = _logMessages.Take(7).ToList();

            var content = recentEvents.Count == 0
                ? "[dim]No recent events[/]"
                : string.Join("\n", recentEvents.Select(msg => $"[dim]{msg}[/]"));

            return new Panel(content)
            {
                Header = new PanelHeader("[bold]Recent Events[/]"),
                Border = BoxBorder.Ascii,
                Padding = new Padding(1, 0)
            };
        }
    }


    /// <summary>
    /// Render user info panel with avatar and details
    /// </summary>
    private Panel RenderUserInfoPanel()
    {
        if (_currentUser == null)
        {
            return new Panel("[dim]No user logged in[/]")
            {
                Header = new PanelHeader("[bold]User Info[/]"),
                Border = BoxBorder.Ascii,
                Padding = new Padding(1, 0)
            };
        }

        var avatarLines = _userService.GetUserAvatar(_currentUser);

        var currentInterface = _captureService.GetCurrentInterface() ?? "None";

        var (localIP, localMAC) = GetLocalIPAndMAC();

        var sshKeyDisplay = GetSSHKeyDisplay(withMarkup: true);
        var avatarStyle = GetAvatarStyle();

        var grid = new Grid();
        grid.AddColumn(new GridColumn().Width(9));  // Avatar width
        grid.AddColumn(new GridColumn().Width(5));  // Spacing
        grid.AddColumn(new GridColumn().NoWrap());  // Info

        grid.AddRow(
            new Text(avatarLines[0], avatarStyle),
            new Text(""),
            new Markup($"user: [bold]{_currentUser.Username}[/]")
        );

        grid.AddRow(
            new Text(avatarLines[1], avatarStyle),
            new Text(""),
            new Markup($"ssh: {sshKeyDisplay}")
        );

        grid.AddRow(
            new Text(avatarLines[2], avatarStyle),
            new Text(""),
            new Markup($"interface: [cyan]{currentInterface}[/]")
        );

        grid.AddRow(
            new Text(avatarLines[3], avatarStyle),
            new Text(""),
            new Markup($"ip: [dim]{localIP}[/] | mac: [dim]{localMAC}[/]")
        );

        return new Panel(grid)
        {
            Header = new PanelHeader("[bold]User Info[/]"),
            Border = BoxBorder.Ascii,
            Padding = new Padding(1, 0)
        };
    }

    private void ExitApplication()
    {
        AnsiConsole.Markup("[dim]Exiting TLScope...[/]");

        _isRunning = false;

    }

    /// <summary>
    /// Performs cleanup of services and resources.
    /// Called from the finally block in Run() to ensure it always executes.
    /// </summary>
    private void Cleanup()
    {
        if (_disposed)
            return;

        _consoleState?.ExitAlternateBuffer();

        var endTime = DateTime.Now;
        var runtime = endTime - _sessionStartTime;
        Console.WriteLine(AnsiColors.Colorize($"[{endTime:yyyy-MM-dd HH:mm:ss}] Session ended", AnsiColors.Dim));
        Console.WriteLine(AnsiColors.Colorize($"Runtime: {runtime.Hours}h {runtime.Minutes}m {runtime.Seconds}s", AnsiColors.Dim));

        try
        {
            Log.Debug("Stopping packet capture service...");
            _captureService.StopCapture();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error stopping packet capture service during cleanup");
        }

        try
        {
            Log.Debug("Stopping TLS peer service...");
            _peerService?.Stop();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error stopping TLS peer service during cleanup");
        }

        try
        {
            if (_captureService is IDisposable captureDisposable)
            {
                Log.Debug("Disposing packet capture service...");
                captureDisposable.Dispose();
            }

            if (_peerService is IDisposable peerDisposable)
            {
                Log.Debug("Disposing TLS peer service...");
                peerDisposable.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error disposing services during cleanup");
        }

        _disposed = true;
    }

    /// <summary>
    /// Render directed graph content without panel wrapper (for table integration)
    /// </summary>
    private Markup RenderDirectedGraphContent(List<Device> devices, List<Connection> connections)
    {
        if (devices.Count == 0)
        {
            return new Markup("[dim]No devices discovered yet[/]");
        }

        var activeDevices = devices.Where(d => d.IsActive).OrderByDescending(d => d.LastSeen).Take(3).ToList();
        var inactiveDevices = devices.Where(d => !d.IsActive).OrderByDescending(d => d.LastSeen).Take(3).ToList();
        var topDevices = activeDevices.Concat(inactiveDevices).ToList();

        var connectionMap = new Dictionary<string, List<(string dest, int strength, bool outbound)>>();

        foreach (var conn in connections.Where(c => c.IsActive))
        {
            var source = conn.SourceDevice.MACAddress;
            var dest = conn.DestinationDevice.MACAddress;
            var strength = (int)conn.PacketCount;

            if (!connectionMap.ContainsKey(source))
                connectionMap[source] = new List<(string, int, bool)>();
            connectionMap[source].Add((dest, strength, true));

            if (!connectionMap.ContainsKey(dest))
                connectionMap[dest] = new List<(string, int, bool)>();
            connectionMap[dest].Add((source, strength, false));
        }

        var lines = new List<string>();
        lines.Add($"[dim]Showing {topDevices.Count} of {devices.Count} devices[/]");
        lines.Add("");

        if (_currentUser != null)
        {
            lines.Add($"        [bold yellow]★ {_currentUser.Username}[/]");
            lines.Add($"        [dim]│[/]");
        }

        foreach (var device in topDevices)
        {
            var deviceLabel = device.Hostname ?? device.IPAddress;
            if (deviceLabel.Length > 15)
                deviceLabel = deviceLabel.Substring(0, 12) + "...";

            var (deviceSymbol, deviceColor) = GetDeviceIconAndColor(device, connections);

            var totalConnections = connectionMap.ContainsKey(device.MACAddress)
                ? connectionMap[device.MACAddress].Count
                : 0;

            var outbound = connectionMap.ContainsKey(device.MACAddress)
                ? connectionMap[device.MACAddress].Count(c => c.outbound)
                : 0;

            var inbound = totalConnections - outbound;

            string arrow;
            if (totalConnections == 0)
                arrow = "[dim]─[/]"; // No connection
            else if (totalConnections <= 2)
                arrow = "[dim]→[/]"; // Weak (1-2 connections)
            else if (totalConnections <= 5)
                arrow = "[yellow]⇒[/]"; // Medium (3-5 connections)
            else
                arrow = "[red]⇛[/]"; // Strong (6+ connections)

            var connInfo = totalConnections > 0
                ? $"[dim]({inbound}↓ {outbound}↑)[/]"
                : "[dim](no conn)[/]";

            lines.Add($"    {arrow} [{deviceColor}]{deviceSymbol}[/] {deviceLabel,-16} {connInfo}");
        }

        lines.Add("");
        lines.Add("[dim]Arrow strength: [/][dim]→[/] weak [yellow]⇒[/] medium [red]⇛[/] strong");
        lines.Add("[dim]Connections: (inbound↓ outbound↑)[/]");

        var content = string.Join("\n", lines);
        return new Markup(content);
    }

    /// <summary>
    /// Render connection matrix content without panel wrapper (for table integration)
    /// </summary>
    private Grid RenderConnectionMatrixContent(List<Device> devices, List<Connection> connections)
    {
        if (devices.Count == 0)
        {
            return new Grid()
                .AddColumn()
                .AddRow(new Markup("[dim]No devices discovered yet[/]"));
        }

        var topDevices = devices
            .OrderByDescending(d => d.LastSeen)
            .Take(10)
            .ToList();

        var connectionLookup = new Dictionary<(string, string), long>();
        foreach (var conn in connections)
        {
            var key = (conn.SourceDevice.MACAddress, conn.DestinationDevice.MACAddress);
            if (connectionLookup.ContainsKey(key))
                connectionLookup[key] += conn.RecentPacketCount;
            else
                connectionLookup[key] = conn.RecentPacketCount;
        }

        var table = new Table()
            .BorderColor(Color.Grey37)
            .Border(TableBorder.Markdown);

        table.AddColumn(new TableColumn("[bold]Device[/]").Width(15));

        foreach (var device in topDevices)
        {
            var shortName = GetShortDeviceName(device, 8);
            table.AddColumn(new TableColumn($"[dim]{shortName}[/]").Width(8).Centered());
        }

        foreach (var srcDevice in topDevices)
        {
            var rowCells = new List<string>();
            var srcName = GetShortDeviceName(srcDevice, 15);
            rowCells.Add($"[cyan]{srcName}[/]");

            foreach (var dstDevice in topDevices)
            {
                if (srcDevice.MACAddress == dstDevice.MACAddress)
                {
                    rowCells.Add("[dim]-[/]");
                }
                else
                {
                    var key1 = (srcDevice.MACAddress, dstDevice.MACAddress);
                    var key2 = (dstDevice.MACAddress, srcDevice.MACAddress);

                    long totalPackets = 0;
                    if (connectionLookup.ContainsKey(key1))
                        totalPackets += connectionLookup[key1];
                    if (connectionLookup.ContainsKey(key2))
                        totalPackets += connectionLookup[key2];

                    rowCells.Add(FormatConnectionStrength(totalPackets));
                }
            }

            table.AddRow(rowCells.ToArray());
        }

        var grid = new Grid()
            .AddColumn()
            .AddRow(table)
            .AddRow(new Text(""))  // Spacer
            .AddRow(new Markup($"[dim]Rate (packets/30s): [grey37]░[/] 1-2  [yellow]▒[/] 3-10  [orange1]▓[/] 11-30  [red]█[/] 30+ | ({topDevices.Count} devices)[/]"));

        return grid;
    }

    /// <summary>
    /// Get short device name for display in compact spaces
    /// </summary>
    private string GetShortDeviceName(Device device, int maxLength)
    {
        string name;

        if (!string.IsNullOrEmpty(device.Hostname))
            name = device.Hostname;
        else if (!string.IsNullOrEmpty(device.Vendor))
            name = device.Vendor;
        else if (!string.IsNullOrEmpty(device.IPAddress))
            name = device.IPAddress;
        else
            name = device.MACAddress.Substring(0, Math.Min(8, device.MACAddress.Length));

        if (name.Length > maxLength)
            name = name.Substring(0, maxLength - 2) + "..";

        return name;
    }

    /// <summary>
    /// IDisposable implementation - ensures cleanup happens.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Cleanup();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion

}

/// <summary>
/// Configuration for excluded devices (IPs, hostnames, MACs that should be filtered out)
/// </summary>
public class ExclusionsConfig
{
    public List<string> ExcludedIPs { get; set; } = new();
    public List<string> ExcludedHostnames { get; set; } = new();
    public List<string> ExcludedMACs { get; set; } = new();
}
