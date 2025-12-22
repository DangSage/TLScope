using Spectre.Console;
using TLScope.Models;
using QuikGraph;
using QuikGraph.Algorithms;

namespace TLScope.Utilities;

/// <summary>
/// Utility class for rendering network graph visualizations in the console
/// </summary>
public static class NetworkGraphUtility
{
    private static Dictionary<Device, (int x, int y)>? _cachedPositions = null;
    private static HashSet<string>? _cachedDeviceMACs = null;
    private static HashSet<(string, string)>? _cachedConnections = null;
    private static (int width, int height)? _cachedDimensions = null;
    private static readonly object _layoutCacheLock = new object();

    /// <summary>
    /// Renders just the ASCII network topology graph without borders or matrix
    /// </summary>
    /// <param name="devices">List of network devices</param>
    /// <param name="connections">List of connections between devices</param>
    /// <param name="currentUser">Current logged-in user to display in center</param>
    /// <param name="heightOverride">Optional height override for the graph (default is 5 for compact view)</param>
    /// <param name="windowWidth">Cached terminal width (pass to avoid Console.WindowWidth system calls)</param>
    /// <param name="isFullscreen">Whether this is fullscreen mode (uses full terminal width)</param>
    /// <returns>A Spectre.Console Grid containing just the ASCII graph</returns>
    public static Grid RenderSimpleTopology(List<Device> devices, List<Connection> connections, User? currentUser = null, int? heightOverride = null, bool showAllDevices = false, int? windowWidth = null, bool isFullscreen = false)
    {
        if (devices.Count == 0)
        {
            var grid = new Grid().AddColumn();
            grid.AddRow(new Markup("[dim]No devices discovered yet[/]"));
            return grid;
        }

        List<Device> topDevices;
        int totalDeviceCount = devices.Count;

        if (showAllDevices)
        {
            topDevices = devices
                .OrderByDescending(d => d.IsGateway || d.IsTLScopePeer)
                .ThenByDescending(d => d.IsActive)
                .ThenByDescending(d => d.LastSeen)
                .ToList();
        }
        else
        {
            var priorityDevices = devices.Where(d => d.IsGateway || d.IsTLScopePeer).ToList();
            var remainingSlots = Math.Max(0, 15 - priorityDevices.Count);

            var activeDevices = devices
                .Where(d => !d.IsGateway && !d.IsTLScopePeer && d.IsActive)
                .OrderByDescending(d => d.LastSeen)
                .Take(remainingSlots)
                .ToList();

            var inactiveDevices = devices
                .Where(d => !d.IsGateway && !d.IsTLScopePeer && !d.IsActive)
                .OrderByDescending(d => d.LastSeen)
                .Take(Math.Max(0, remainingSlots - activeDevices.Count))
                .ToList();

            topDevices = priorityDevices.Concat(activeDevices).Concat(inactiveDevices).ToList();
        }

        var connectionLookup = new Dictionary<(string, string), int>();
        var connectionTypeLookup = new Dictionary<(string, string), ConnectionType>();
        foreach (var conn in connections)
        {
            bool isImportantConnection = conn.IsTLSPeerConnection ||
                                        conn.SourceDevice.IsGateway ||
                                        conn.DestinationDevice.IsGateway ||
                                        conn.IsActive;

            if (!isImportantConnection)
                continue;

            var key = (conn.SourceDevice.MACAddress, conn.DestinationDevice.MACAddress);
            if (connectionLookup.ContainsKey(key))
            {
                connectionLookup[key] += (int)conn.PacketCount;
                if ((int)conn.Type > (int)connectionTypeLookup[key])
                    connectionTypeLookup[key] = conn.Type;
            }
            else
            {
                connectionLookup[key] = (int)conn.PacketCount;
                connectionTypeLookup[key] = conn.Type;
            }
        }

        var asciiGraph = RenderAsciiNetworkGraph(topDevices, connectionLookup, connectionTypeLookup, heightOverride, isFullscreen, windowWidth);

        var resultGrid = new Grid()
            .AddColumn();

        var activeCount = topDevices.Count(d => d.IsActive);
        var inactiveCount = topDevices.Count - activeCount;

        var activeConnectionsCount = connections.Count(c => c.SourceDevice.IsActive && c.DestinationDevice.IsActive);

        string deviceCountInfo;
        if (topDevices.Count < totalDeviceCount)
        {
            deviceCountInfo = $"[dim](showing {topDevices.Count}/{totalDeviceCount} devices: {activeCount} active, {inactiveCount} inactive, {activeConnectionsCount} connections)[/]";
        }
        else
        {
            deviceCountInfo = $"[dim]({activeCount} active, {inactiveCount} inactive, {activeConnectionsCount} connections)[/]";
        }

        var displayConfig = Models.DisplayConfiguration.Load();

        var typeLegend = "[cyan]···[/] Local  [yellow]···[/] Routed  [orange1]···[/] Internet  [green]···[/] TLS Peer";

        var deviceLegend = "[dim]◆ Gateway  ◍ Remote  ◉ Peer  ● Local  ○ Idle[/]";

        resultGrid.AddRow(new Markup($"[bold cyan]Network Constellation[/] {deviceCountInfo}"));
        resultGrid.AddRow(asciiGraph);
        resultGrid.AddRow(new Text(""));
        resultGrid.AddRow(new Markup(typeLegend));
        resultGrid.AddRow(new Markup(deviceLegend));

        return resultGrid;
    }

    /// <summary>
    /// Renders a compact directed graph showing connection strength and direction
    /// Smaller and more focused than RenderSimpleTopology, ideal for statistics view
    /// </summary>
    /// <param name="devices">List of network devices</param>
    /// <param name="connections">List of connections between devices</param>
    /// <param name="currentUser">Current logged-in user to display in center</param>
    /// <returns>A Spectre.Console Panel containing the compact directed graph</returns>
    public static Panel RenderCompactDirectedGraph(List<Device> devices, List<Connection> connections, User? currentUser = null)
    {
        if (devices.Count == 0)
        {
            return new Panel("[dim]No devices discovered yet[/]")
            {
                Header = new PanelHeader("[default]Network Topology[/]"),
                Border = BoxBorder.Ascii,
                BorderStyle = new Style(Color.Grey37),
                Expand = true
            };
        }

        var priorityDevices = devices.Where(d => d.IsGateway || d.IsTLScopePeer).ToList();
        var remainingSlots = Math.Max(0, 10 - priorityDevices.Count);

        var activeDevices = devices
            .Where(d => !d.IsGateway && !d.IsTLScopePeer && d.IsActive)
            .OrderByDescending(d => d.LastSeen)
            .Take(Math.Max(0, remainingSlots / 2))
            .ToList();

        var inactiveDevices = devices
            .Where(d => !d.IsGateway && !d.IsTLScopePeer && !d.IsActive)
            .OrderByDescending(d => d.LastSeen)
            .Take(Math.Max(0, remainingSlots - activeDevices.Count))
            .ToList();

        var topDevices = priorityDevices.Concat(activeDevices).Concat(inactiveDevices).ToList();

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

        foreach (var device in topDevices)
        {
            var deviceLabel = device.Hostname ?? device.IPAddress;
            if (deviceLabel.Length > 15)
                deviceLabel = deviceLabel.Substring(0, 12) + "...";

            var (styleSymbol, styleColor) = GetDeviceStyle(device);
            string deviceSymbol = styleSymbol.ToString();
            string deviceColor = styleColor;

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

        return new Panel(new Markup(content))
        {
            Header = new PanelHeader("[white]Network Topology (Directed)[/]"),
            Border = BoxBorder.Ascii,
            BorderStyle = new Style(Color.Grey37),
            Padding = new Padding(1, 0, 1, 0)
        };
    }

    /// <summary>
    /// Renders a connection matrix showing connection intensity between devices
    /// </summary>
    /// <param name="devices">List of network devices</param>
    /// <param name="connections">List of connections between devices</param>
    /// <returns>A Spectre.Console Panel containing the connection matrix</returns>
    public static Panel RenderConnectionMatrix(List<Device> devices, List<Connection> connections)
    {
        if (devices.Count == 0)
        {
            return new Panel("[dim]No devices discovered yet[/]")
            {
                Header = new PanelHeader("[bold cyan]Network Connection Matrix[/]"),
                Border = BoxBorder.Ascii,
                BorderStyle = new Style(Color.Blue)
            };
        }

        var priorityDevices = devices.Where(d => d.IsGateway || d.IsTLScopePeer).ToList();
        var remainingSlots = Math.Max(0, 10 - priorityDevices.Count);

        var otherDevices = devices
            .Where(d => !d.IsGateway && !d.IsTLScopePeer)
            .OrderByDescending(d => d.IsActive)
            .ThenByDescending(d => d.LastSeen)
            .Take(remainingSlots)
            .ToList();

        var topDevices = priorityDevices.Concat(otherDevices).ToList();

        var connectionLookup = new Dictionary<(string, string), int>();
        foreach (var conn in connections)
        {
            var key = (conn.SourceDevice.MACAddress, conn.DestinationDevice.MACAddress);
            if (connectionLookup.ContainsKey(key))
                connectionLookup[key] += (int)conn.PacketCount;
            else
                connectionLookup[key] = (int)conn.PacketCount;
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

                    int totalPackets = 0;
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
            .AddRow(new Markup("[dim]Intensity: [grey37]░[/] 1-5  [yellow]▒[/] 6-20  [orange1]▓[/] 21-50  [red]█[/] 51+[/]"));

        var panel = new Panel(grid)
        {
            Header = new PanelHeader($"[white]Connection Matrix[/] [white dim]({topDevices.Count} devices)[/]"),
            Border = BoxBorder.Ascii,
            BorderStyle = new Style(Color.Grey37),
            Expand = true
        };

        return panel;
    }

    /// <summary>
    /// Renders a detailed list of devices with their information
    /// </summary>
    /// <param name="devices">List of network devices</param>
    /// <returns>A Spectre.Console Panel containing the device list</returns>
    public static Panel RenderDeviceList(List<Device> devices)
    {
        if (devices.Count == 0)
        {
            return new Panel("[dim]No devices discovered yet[/]")
            {
                Header = new PanelHeader("[bold green]Active Devices[/]"),
                Border = BoxBorder.None,
                BorderStyle = new Style(Color.Green),
                Expand = true
            };
        }

        var table = new Table()
            .BorderColor(Color.Grey37)
            .Border(TableBorder.Markdown);

        table.AddColumn(new TableColumn("[bold]Hostname/Vendor[/]").Width(25));
        table.AddColumn(new TableColumn("[bold]IP Address[/]").Width(15));
        table.AddColumn(new TableColumn("[bold]MAC Address[/]").Width(17));
        table.AddColumn(new TableColumn("[bold]Last Seen[/]").Width(12));

        var sortedDevices = devices
            .OrderByDescending(d => d.LastSeen)
            .Take(15) // Show top 15 devices
            .ToList();

        foreach (var device in sortedDevices)
        {
            var hostname = string.IsNullOrEmpty(device.Hostname)
                ? (string.IsNullOrEmpty(device.Vendor) ? "[dim]Unknown[/]" : device.Vendor)
                : device.Hostname;

            if (hostname.Length > 25)
                hostname = hostname.Substring(0, 22) + "...";

            var ip = string.IsNullOrEmpty(device.IPAddress) ? "[dim]N/A[/]" : device.IPAddress;
            var mac = device.MACAddress;
            var lastSeen = FormatLastSeen(device.LastSeen);

            table.AddRow(hostname, ip, mac, lastSeen);
        }

        var panel = new Panel(table)
        {
            Header = new PanelHeader($"[bold green]Active Devices[/] [dim]({devices.Count} total)[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
            Expand = true
        };

        return panel;
    }

    /// <summary>
    /// Formats connection strength as a visual indicator using block characters
    /// </summary>
    private static string FormatConnectionStrength(int packetCount)
    {
        if (packetCount == 0)
            return "[dim]·[/]";

        if (packetCount <= 5)
            return "[grey37]░[/]";
        else if (packetCount <= 20)
            return "[yellow]▒[/]";
        else if (packetCount <= 50)
            return "[orange1]▓[/]";
        else
            return "[red]█[/]";
    }

    /// <summary>
    /// Formats the last seen timestamp as a human-readable string with color coding
    /// </summary>
    private static string FormatLastSeen(DateTime lastSeen)
    {
        var elapsed = DateTime.Now - lastSeen;

        if (elapsed.TotalSeconds < 5)
            return "[green]now[/]";
        else if (elapsed.TotalSeconds < 10)
            return $"[green]{(int)elapsed.TotalSeconds}s ago[/]";
        else if (elapsed.TotalSeconds < 30)
            return $"[yellow]{(int)elapsed.TotalSeconds}s ago[/]";
        else if (elapsed.TotalSeconds < 60)
            return $"[orange1]{(int)elapsed.TotalSeconds}s ago[/]";
        else if (elapsed.TotalMinutes < 60)
            return $"[dim]{(int)elapsed.TotalMinutes}m ago[/]";
        else
            return $"[dim]{(int)elapsed.TotalHours}h ago[/]";
    }

    /// <summary>
    /// Gets a short device name for display in compact spaces
    /// </summary>
    /// <summary>
    /// Gets device styling (symbol and color) based on device state
    /// Unified method to avoid duplication across rendering methods
    /// </summary>
    private static (char symbol, string color) GetDeviceStyle(Device device)
    {
        if (device.IsTLScopePeer)
            return ('◉', "green");

        if (device.IsGateway)
        {
            if (device.IsDefaultGateway)
                return ('◆', "yellow");  // Default gateway: yellow diamond
            else
                return ('◇', "yellow");  // Secondary gateway: hollow diamond
        }

        if (device.IsVirtualDevice || !device.IsLocal)
        {
            if (device.IsActive)
                return ('◍', "orange1");  // Remote active: orange filled circle
            else
                return ('○', "orange1");   // Remote inactive: orange hollow circle
        }

        if (device.IsActive)
            return ('●', "cyan");   // Local active: cyan filled circle
        else
            return ('○', "cyan");  // Local inactive: cyan hollow circle
    }

    /// <summary>
    /// Gets device display information (name and width) - unified method to avoid duplication
    /// </summary>
    private static (string name, double width) GetDeviceDisplayInfo(Device device, int maxLength = int.MaxValue)
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

        string displayName = name;
        if (displayName.Length > maxLength)
            displayName = displayName.Substring(0, maxLength - 2) + "..";

        double width = Math.Min(name.Length, 12) / 2.0;

        return (displayName, width);
    }

    /// <summary>
    /// Gets the short display name of a device (for backward compatibility)
    /// </summary>
    private static string GetShortDeviceName(Device device, int maxLength)
    {
        return GetDeviceDisplayInfo(device, maxLength).name;
    }

    /// <summary>
    /// Gets the approximate display width of a device's label (for backward compatibility)
    /// </summary>
    private static double GetDeviceLabelWidth(Device device)
    {
        return GetDeviceDisplayInfo(device).width;
    }

    /// <summary>
    /// Calculate Minimum Spanning Tree edges to ensure graph connectivity
    /// Returns a set of edge keys (src, dst) that are part of the MST
    /// </summary>
    private static HashSet<(string src, string dst)> CalculateMST(
        List<Device> devices,
        Dictionary<(string, string), int> connectionLookup)
    {
        var mstEdges = new HashSet<(string, string)>();

        if (devices.Count < 2)
            return mstEdges;

        try
        {
            var graph = new QuikGraph.UndirectedGraph<string, QuikGraph.TaggedEdge<string, int>>();

            foreach (var device in devices.Where(d => d.IsActive))
            {
                graph.AddVertex(device.MACAddress);
            }

            foreach (var ((src, dst), packets) in connectionLookup)
            {
                if (!graph.ContainsVertex(src) || !graph.ContainsVertex(dst))
                    continue;

                double weight = 1.0 / (packets + 1.0);

                var edge = new QuikGraph.TaggedEdge<string, int>(src, dst, (int)(weight * 1000));
                graph.AddEdge(edge);
            }

            var edgeCost = new Dictionary<QuikGraph.TaggedEdge<string, int>, double>();
            foreach (var edge in graph.Edges)
            {
                edgeCost[edge] = edge.Tag;
            }

            var mst = graph.MinimumSpanningTreePrim(e => edgeCost[e]);

            foreach (var edge in mst)
            {
                mstEdges.Add((edge.Source, edge.Target));
                mstEdges.Add((edge.Target, edge.Source));
            }
        }
        catch (Exception)
        {
        }

        return mstEdges;
    }

    /// <summary>
    /// Renders an ASCII vector graph showing network topology with nodes and connections
    /// </summary>
    /// <param name="devices">List of network devices</param>
    /// <param name="connectionLookup">Dictionary of connections between devices</param>
    /// <param name="heightOverride">Optional height override for the graph (default is 5 for compact view)</param>
    /// <param name="isFullscreen">Whether this is fullscreen mode (uses full terminal width)</param>
    private static Markup RenderAsciiNetworkGraph(
        List<Device> devices,
        Dictionary<(string, string), int> connectionLookup,
        Dictionary<(string, string), ConnectionType> connectionTypeLookup,
        int? heightOverride = null,
        bool isFullscreen = false,
        int? windowWidth = null)
    {
        if (devices.Count == 0)
            return new Markup("[dim]No devices to visualize[/]");

        int terminalWidth = windowWidth ?? Console.WindowWidth;

        int width = isFullscreen
            ? (int)(terminalWidth * 0.80)
            : (int)(terminalWidth * 0.40);

        int defaultHeight = width / 2;
        int height = heightOverride ?? Math.Clamp(defaultHeight, 15, 40);

        var displayConfig = Models.DisplayConfiguration.Load();

        var nodePositions = CalculateForceDirectedLayout(devices, connectionLookup, width, height);

        char[,] field = InitializeField(width, height);
        int[,] strengthField = new int[height, width];  // Track packet count for each edge pixel
        ConnectionType[,] typeField = new ConnectionType[height, width];  // Track connection type for coloring

        var nodeBufferZones = CreateNodeBufferZones(nodePositions, devices, width, height);

        DrawEdges(field, strengthField, typeField, nodePositions, connectionLookup, connectionTypeLookup, devices, displayConfig, nodeBufferZones);

        DrawNodes(field, nodePositions, devices);

        var styledLines = FieldToStyledLines(field, strengthField, typeField, nodePositions, devices);

        return new Markup(string.Join("\n", styledLines));
    }

    /// <summary>
    /// Calculate node positions using force-directed layout
    /// Optimized to avoid halting with large device counts
    /// </summary>
    private static Dictionary<Device, (int x, int y)> CalculateForceDirectedLayout(
        List<Device> devices,
        Dictionary<(string, string), int> connectionLookup,
        int width,
        int height)
    {
        if (devices.Count == 0)
            return new Dictionary<Device, (int x, int y)>();

        lock (_layoutCacheLock)
        {
            var currentDeviceMACs = devices.Select(d => d.MACAddress).ToHashSet();
            var currentConnections = connectionLookup.Keys.ToHashSet();
            var currentDimensions = (width, height);

            if (_cachedPositions != null &&
                _cachedDeviceMACs != null &&
                _cachedConnections != null &&
                _cachedDimensions != null &&
                _cachedDeviceMACs.SetEquals(currentDeviceMACs) &&
                _cachedConnections.SetEquals(currentConnections) &&
                _cachedDimensions.Value == currentDimensions)
            {
                return _cachedPositions;
            }
        }

        var positions = new Dictionary<Device, (double x, double y)>();
        var velocities = new Dictionary<Device, (double vx, double vy)>();

        int centerX = width / 2;
        int centerY = height / 2;

        double radius = Math.Min(width / 6.0, height * 0.7);

        for (int i = 0; i < devices.Count; i++)
        {
            double angle = (2 * Math.PI * i) / devices.Count;
            positions[devices[i]] = (
                centerX + radius * Math.Cos(angle),
                centerY + radius * Math.Sin(angle) * 0.5
            );
            velocities[devices[i]] = (0, 0);
        }

        var deviceByMAC = devices.ToDictionary(d => d.MACAddress);

        double repulsionStrength = 15.0;
        double attractionStrength = 0.05;
        double damping = 0.85;
        int iterations = 30;  // Reduced from 80 for better performance (still produces stable layouts)
        double minDistance = 2.5;

        for (int iter = 0; iter < iterations; iter++)
        {
            var forces = new Dictionary<Device, (double fx, double fy)>();

            foreach (var device in devices)
                forces[device] = (0, 0);

            for (int i = 0; i < devices.Count; i++)
            {
                for (int j = i + 1; j < devices.Count; j++)
                {
                    var d1 = devices[i];
                    var d2 = devices[j];

                    var dx = positions[d2].x - positions[d1].x;
                    var dy = positions[d2].y - positions[d1].y;
                    var distSq = dx * dx + dy * dy + 0.01;
                    var dist = Math.Sqrt(distSq);

                    var force = repulsionStrength / distSq;
                    if (dist < minDistance)
                        force *= 2.0;

                    var fx = (dx / dist) * force;
                    var fy = (dy / dist) * force;

                    forces[d1] = (forces[d1].fx - fx, forces[d1].fy - fy);
                    forces[d2] = (forces[d2].fx + fx, forces[d2].fy + fy);
                }
            }

            foreach (var ((srcMAC, dstMAC), packetCount) in connectionLookup)
            {
                if (!deviceByMAC.TryGetValue(srcMAC, out var d1) ||
                    !deviceByMAC.TryGetValue(dstMAC, out var d2))
                    continue;

                var dx = positions[d2].x - positions[d1].x;
                var dy = positions[d2].y - positions[d1].y;
                var dist = Math.Sqrt(dx * dx + dy * dy + 0.01);

                double connectionWeight = 1.0 + Math.Log10(1 + packetCount) * 0.15;
                var force = dist * attractionStrength * connectionWeight;
                var fx = (dx / dist) * force;
                var fy = (dy / dist) * force;

                forces[d1] = (forces[d1].fx + fx, forces[d1].fy + fy);
                forces[d2] = (forces[d2].fx - fx, forces[d2].fy - fy);
            }

            var gateways = devices.Where(d => d.IsGateway).ToList();
            if (gateways.Any())
            {
                double gatewayGravity = 0.8;

                foreach (var device in devices)
                {
                    if (device.IsGateway) continue;

                    foreach (var gateway in gateways)
                    {
                        var dx = positions[gateway].x - positions[device].x;
                        var dy = positions[gateway].y - positions[device].y;
                        var dist = Math.Sqrt(dx * dx + dy * dy + 0.01);

                        double gravityMult = gateway.IsDefaultGateway ? 1.3 : 1.0;
                        var force = gatewayGravity * gravityMult;
                        var fx = (dx / dist) * force;
                        var fy = (dy / dist) * force;

                        forces[device] = (forces[device].fx + fx, forces[device].fy + fy);
                    }
                }
            }

            foreach (var device in devices)
            {
                var v = velocities[device];
                var f = forces[device];

                v.vx = (v.vx + f.fx) * damping;
                v.vy = (v.vy + f.fy) * damping;
                velocities[device] = v;

                var p = positions[device];
                p.x += v.vx;
                p.y += v.vy;

                p.x = Math.Clamp(p.x, 2, width - 2);
                p.y = Math.Clamp(p.y, 1, height - 1);

                positions[device] = p;
            }
        }

        var result = new Dictionary<Device, (int x, int y)>();
        foreach (var kvp in positions)
        {
            result[kvp.Key] = ((int)Math.Round(kvp.Value.x), (int)Math.Round(kvp.Value.y));
        }

        lock (_layoutCacheLock)
        {
            _cachedPositions = result;
            _cachedDeviceMACs = devices.Select(d => d.MACAddress).ToHashSet();
            _cachedConnections = connectionLookup.Keys.ToHashSet();
            _cachedDimensions = (width, height);
        }

        return result;
    }


    /// <summary>
    /// Initialize empty character field
    /// </summary>
    private static char[,] InitializeField(int width, int height)
    {
        var field = new char[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                field[y, x] = ' ';
        return field;
    }

    /// <summary>
    /// Create a set of positions that should be kept clear (only exact node positions)
    /// Edges can touch nodes but not overwrite them
    /// </summary>
    private static HashSet<(int x, int y)> CreateNodeBufferZones(
        Dictionary<Device, (int x, int y)> nodePositions,
        List<Device> devices,
        int width,
        int height)
    {
        var bufferZones = new HashSet<(int x, int y)>();

        foreach (var device in devices)
        {
            var (x, y) = nodePositions[device];
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                bufferZones.Add((x, y));
            }
        }

        return bufferZones;
    }

    /// <summary>
    /// Draw edges between connected nodes using Bresenham's line algorithm
    /// Uses multi-layer rendering: strong edges (background) → medium → weak (foreground)
    /// Filters edges based on DisplayConfiguration (MST, importance, etc.)
    /// </summary>
    private static void DrawEdges(
        char[,] field,
        int[,] strengthField,
        ConnectionType[,] typeField,
        Dictionary<Device, (int x, int y)> nodePositions,
        Dictionary<(string, string), int> connectionLookup,
        Dictionary<(string, string), ConnectionType> connectionTypeLookup,
        List<Device> devices,
        DisplayConfiguration displayConfig,
        HashSet<(int x, int y)>? nodeBufferZones = null)
    {
        var mstEdges = CalculateMST(devices, connectionLookup);

        var edges = new List<(string srcMAC, string dstMAC, int x1, int y1, int x2, int y2, int packets, ConnectionType type)>();

        foreach (var src in devices)
        {
            foreach (var dst in devices)
            {
                if (src == dst) continue;

                var key = (src.MACAddress, dst.MACAddress);
                if (!connectionLookup.ContainsKey(key)) continue;

                int packets = connectionLookup[key];

                bool shouldShow = ShouldShowEdge(
                    src, dst, packets, displayConfig, mstEdges);

                if (!shouldShow)
                    continue;

                var (x1, y1) = nodePositions[src];
                var (x2, y2) = nodePositions[dst];

                var connType = connectionTypeLookup.ContainsKey(key)
                    ? connectionTypeLookup[key]
                    : ConnectionType.Unknown;

                edges.Add((src.MACAddress, dst.MACAddress, x1, y1, x2, y2, packets, connType));
            }
        }

        edges.Sort((a, b) => b.packets.CompareTo(a.packets));


        foreach (var edge in edges.Where(e => e.packets > 50))
        {
            DrawLine(field, strengthField, typeField, edge.x1, edge.y1, edge.x2, edge.y2, edge.packets, edge.type, displayConfig.UseAsciiConnections, nodeBufferZones);
        }

        foreach (var edge in edges.Where(e => e.packets > 10 && e.packets <= 50))
        {
            DrawLine(field, strengthField, typeField, edge.x1, edge.y1, edge.x2, edge.y2, edge.packets, edge.type, displayConfig.UseAsciiConnections, nodeBufferZones);
        }

        foreach (var edge in edges.Where(e => e.packets <= 10))
        {
            DrawLine(field, strengthField, typeField, edge.x1, edge.y1, edge.x2, edge.y2, edge.packets, edge.type, displayConfig.UseAsciiConnections, nodeBufferZones);
        }
    }

    /// <summary>
    /// Determines whether an edge should be displayed based on configuration
    /// </summary>
    private static bool ShouldShowEdge(
        Device src,
        Device dst,
        int packets,
        DisplayConfiguration config,
        HashSet<(string, string)> mstEdges)
    {
        bool isImportantConnection = src.IsGateway || dst.IsGateway ||
                                     src.IsTLScopePeer || dst.IsTLScopePeer;

        if (isImportantConnection)
            return true;

        if (mstEdges.Contains((src.MACAddress, dst.MACAddress)))
            return true;

        if (packets >= config.MinEdgeStrengthToShow)
            return true;

        return false;
    }

    /// <summary>
    /// Determine if we should draw a character at the given position based on connection strength
    /// Weak: wide spacing (every 3rd), Medium: moderate spacing (every 2nd), Strong: tight spacing (every position)
    /// </summary>
    private static bool ShouldDrawDotConnection(int currentX, int currentY, int startX, int startY, int strength)
    {
        int distance = Math.Abs(currentX - startX) + Math.Abs(currentY - startY);

        return strength switch
        {
            0 => distance % 3 == 0,  // Weak: every 3rd position
            1 => distance % 2 == 0,  // Medium: every 2nd position
            2 => true,               // Strong: every position (continuous)
            _ => true
        };
    }

    /// <summary>
    /// Draw a line using Bresenham's algorithm with angle-aware characters
    /// </summary>
    private static void DrawLine(char[,] field, int[,] strengthField, ConnectionType[,] typeField, int x1, int y1, int x2, int y2, int packetCount, ConnectionType connType, bool useAscii, HashSet<(int x, int y)>? nodeBufferZones = null)
    {
        int height = field.GetLength(0);
        int width = field.GetLength(1);

        int startX = x1;
        int startY = y1;

        int dx = Math.Abs(x2 - x1);
        int dy = Math.Abs(y2 - y1);
        int sx = x1 < x2 ? 1 : -1;
        int sy = y1 < y2 ? 1 : -1;
        int err = dx - dy;

        int prevX = x1;
        int prevY = y1;

        int strength = packetCount <= 10 ? 0 : (packetCount <= 50 ? 1 : 2);

        while (true)
        {
            char ch = GetConnectionChar(prevX, prevY, x1, y1, packetCount, useAscii);

            if (y1 >= 0 && y1 < height && x1 >= 0 && x1 < width)
            {
                bool inBufferZone = nodeBufferZones != null && nodeBufferZones.Contains((x1, y1));

                bool shouldDraw = !inBufferZone && field[y1, x1] == ' ' &&
                                  ShouldDrawDotConnection(x1, y1, startX, startY, strength);

                if (shouldDraw)
                {
                    field[y1, x1] = ch;
                    strengthField[y1, x1] = packetCount;  // Store packet count for styling
                    typeField[y1, x1] = connType;  // Store connection type for coloring
                }
            }

            if (x1 == x2 && y1 == y2) break;

            prevX = x1;
            prevY = y1;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x1 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y1 += sy;
            }
        }
    }

    /// <summary>
    /// Draw nodes as symbols
    /// </summary>
    private static void DrawNodes(
        char[,] field,
        Dictionary<Device, (int x, int y)> nodePositions,
        List<Device> devices)
    {
        int height = field.GetLength(0);
        int width = field.GetLength(1);

        foreach (var device in devices)
        {
            var (x, y) = nodePositions[device];

            if (y >= 0 && y < height && x >= 0 && x < width)
            {
                var (symbol, _) = GetDeviceStyle(device);
                field[y, x] = symbol;
            }
        }
    }

    /// <summary>
    /// Convert field to styled lines with Spectre.Console color markup
    /// </summary>
    private static List<string> FieldToStyledLines(
        char[,] field,
        int[,] strengthField,
        ConnectionType[,] typeField,
        Dictionary<Device, (int x, int y)> nodePositions,
        List<Device> devices)
    {
        int height = field.GetLength(0);
        int width = field.GetLength(1);
        var lines = new List<string>();

        for (int y = 0; y < height; y++)
        {
            var line = new System.Text.StringBuilder();
            for (int x = 0; x < width; x++)
            {
                char ch = field[y, x];

                var nodeAtPos = devices.FirstOrDefault(d =>
                {
                    var pos = nodePositions[d];
                    return pos.x == x && pos.y == y;
                });

                if (nodeAtPos != null)
                {
                    var (_, color) = GetDeviceStyle(nodeAtPos);
                    line.Append($"[{color}]{ch}[/]");
                }
                else if (ch != ' ')
                {
                    int packetCount = strengthField[y, x];
                    ConnectionType connType = typeField[y, x];

                    string color = connType switch
                    {
                        ConnectionType.DirectL2 => "cyan",       // Local network connections
                        ConnectionType.RoutedL3 => "yellow",     // Routed through local gateway
                        ConnectionType.Internet => "orange1",    // Internet connections
                        ConnectionType.TLSPeer => "green",       // TLScope peer connections
                        _ => "grey"                              // Unknown connections
                    };

                    string boldModifier = packetCount > 50 ? " bold" : "";

                    string dimModifier = packetCount <= 10 ? " dim" : "";

                    line.Append($"[{color}{boldModifier}{dimModifier}]{ch}[/]");
                }
                else
                {
                    line.Append(ch);
                }
            }
            lines.Add(line.ToString());
        }

        return lines;
    }

    /// <summary>
    /// Get connection character and strength based on packet count
    /// Returns a tuple of (character, strength) where strength is 0=weak, 1=medium, 2=strong
    /// All connections use ellipsis/dot characters, differentiated by spacing and styling
    /// </summary>
    private static (char ch, int strength) GetConnectionCharWithStrength(int x1, int y1, int x2, int y2, int packetCount, bool useAscii)
    {
        int strength = packetCount <= 10 ? 0 : (packetCount <= 50 ? 1 : 2);


        if (useAscii)
        {
            return ('.', strength);
        }
        else
        {
            if (strength == 2)
                return ('•', 2);  // U+2022 bullet for strong connections
            else
                return ('·', strength);  // U+00B7 middle dot for weak/medium
        }
    }

    /// <summary>
    /// Get connection character based on angle, strength, and display mode
    /// </summary>
    private static char GetConnectionChar(int x1, int y1, int x2, int y2, int packetCount, bool useAscii)
    {
        var (ch, _) = GetConnectionCharWithStrength(x1, y1, x2, y2, packetCount, useAscii);
        return ch;
    }
}
