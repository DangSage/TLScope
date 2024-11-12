using System.Collections.Concurrent;
using System.Linq;
using Terminal.Gui;
using Terminal.Gui.Trees;

using TLScope.src.Utilities;
using TLScope.src.Services;
using TLScope.src.Controllers;
using TLScope.src.Models;

namespace TLScope.src.Views {
    public class NetView : Window {
        private readonly TreeView _deviceTreeView;
        private readonly ConcurrentDictionary<string, Device> _devices;
        private readonly TreeNode _rootNode = new("Listening for Devices...");

        public NetView(ref NetworkController nc) : base("Network Information") {
            _devices = nc.GetActiveDevices();
            ColorScheme = Constants.TLSColorScheme;

            _deviceTreeView = new TreeView {
                X = 0,
                Y = 0,
                Width = Dim.Fill() - 2,
                Height = Dim.Fill() - 2,
                CanFocus = true,
            };

            Add(_deviceTreeView);
            _deviceTreeView.AddObject(_rootNode);

            NetworkService.DeviceListUpdate += (sender, e) => PopulateTreeView();
        }

        private void PopulateTreeView() {
            // Update root node text
            _rootNode.Text = $"Number of Devices: {_devices.Count}";
        
            // Create a dictionary to map device IP addresses to tree nodes
            var nodeMap = new Dictionary<string, TreeNode>();
            foreach (var node in _rootNode.Children.OfType<TreeNode>()) {
                if (node.Tag is Device device) {
                    nodeMap[device.IPAddress] = node;
                }
            }
        
            // Create a hash set of the device values
            var devices = new HashSet<Device>(_devices.Values);
        
            // Stack to manage tree nodes
            var nodeStack = new Stack<TreeNode>();
        
            // Compare the existing tree nodes with the devices
            foreach (var device in devices) {
                if (device == null) { continue; }
        
                if (nodeMap.TryGetValue(device.IPAddress, out var node)) {
                    // Update the existing node in the tree
                    node.Tag = device;
                    node.Children.Clear();
                    node.Children.Add(new TreeNode($"IP Address: {device.IPAddress}"));
                    node.Children.Add(new TreeNode($"MAC Address: {device.MACAddress}"));
                    node.Children.Add(new TreeNode($"Operating System: {device.OperatingSystem ?? "Unknown"}"));
                    node.Children.Add(new TreeNode($"Last Seen: {device.LastSeen}"));
                } else {
                    // Add new node for the device
                    var newNode = new TreeNode(device.DeviceName) { Tag = device };
                    newNode.Children.Add(new TreeNode($"IP Address: {device.IPAddress}"));
                    newNode.Children.Add(new TreeNode($"MAC Address: {device.MACAddress}"));
                    newNode.Children.Add(new TreeNode($"Operating System: {device.OperatingSystem ?? "Unknown"}"));
                    newNode.Children.Add(new TreeNode($"Last Seen: {device.LastSeen}"));
        
                    _rootNode.Children.Add(newNode);
                    nodeStack.Push(newNode);
                }
            }
        
            // Remove nodes that are no longer in the devices list
            foreach (var node in _rootNode.Children.OfType<TreeNode>().ToList()) {
                if (node.Tag is Device device && !devices.Contains(device)) {
                    _rootNode.Children.Remove(node);
                }
            }
        
            // Refresh the TreeView
            _deviceTreeView.ClearObjects();
            _deviceTreeView.AddObject(_rootNode);
            _deviceTreeView.ExpandAll();
        }
    }
}
