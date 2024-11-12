#r "nuget: Terminal.Gui, 1.17.1.0"
#r "/home/khai/Documents/TLScope/bin/Debug/net8.0/TLScope.dll" // Update this path to the actual location of the compiled DLL

using System;
using System.Collections.Generic;


using TLScope.src.Models;
using Terminal.Gui;
using Terminal.Gui.Trees;

// Initialize the application
Application.Init();
var top = Application.Top;

// Create a window
var win = new Window("Device Tree View") {
    X = 0,
    Y = 1, // Leave one row for the menu
    Width = Dim.Fill(),
    Height = Dim.Fill(),
    CanFocus = false,
    ColorScheme = new ColorScheme {
        Normal = Terminal.Gui.Attribute.Make(Color.White, Color.Black),
        Focus = Terminal.Gui.Attribute.Make(Color.White, Color.DarkGray),
        HotNormal = Terminal.Gui.Attribute.Make(Color.White, Color.DarkGray),
        HotFocus = Terminal.Gui.Attribute.Make(Color.White, Color.DarkGray),
    },
};

// Create a list of Device objects
var devices = new List<Device> {
    new Device {
        DeviceName = "Device 1",
        IPAddress = "192.168.1.1",
        MACAddress = "00:11:22:33:44:55",
        OperatingSystem = "Linux",
        LastSeen = DateTime.UtcNow
    },
    new Device {
        DeviceName = "Device 2",
        IPAddress = "192.168.1.2",
        MACAddress = "66:77:88:99:AA:BB",
        OperatingSystem = "Windows",
        LastSeen = DateTime.UtcNow
    }
};

// Create a TreeView and manually add the nodes
var tree = new TreeView {
    X = 0,
    Y = 0,
    Width = Dim.Fill(),
    Height = Dim.Fill(),
    CanFocus = true,
    ColorScheme = new ColorScheme {
        Normal = Terminal.Gui.Attribute.Make(Color.White, Color.Black),
        Focus = Terminal.Gui.Attribute.Make(Color.White, Color.DarkGray),
        HotNormal = Terminal.Gui.Attribute.Make(Color.White, Color.DarkGray),
        HotFocus = Terminal.Gui.Attribute.Make(Color.White, Color.DarkGray),
    }
};

// Create the root node
var rootNode = new TreeNode("Devices");

// Add device nodes to the root node
foreach (var device in devices) {
    var deviceNode = new TreeNode(device.DeviceName) {
        Tag = device
    };

    // Add child nodes for device details
    deviceNode.Children.Add(new TreeNode($"IP Address: {device.IPAddress}"));
    deviceNode.Children.Add(new TreeNode($"MAC Address: {device.MACAddress}"));
    deviceNode.Children.Add(new TreeNode($"Operating System: {device.OperatingSystem ?? "Unknown"}"));
    deviceNode.Children.Add(new TreeNode($"Last Seen: {device.LastSeen}"));

    rootNode.Children.Add(deviceNode);
}

// Add the root node to the TreeView
tree.AddObject(rootNode);

// Add the TreeView to the window
win.Add(tree);

// Add the window to the top-level container
top.Add(win);

// Add a menu bar with a Quit option
top.Add(new MenuBar(new MenuBarItem[] {
    new MenuBarItem("_File", new MenuItem[] {
        new MenuItem("_Quit", "", () => Application.RequestStop(), null, null, Key.Q | Key.CtrlMask)
    })
}));

// Run the application
Application.Run();
Application.Shutdown();