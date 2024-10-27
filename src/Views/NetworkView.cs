using System.Collections.Concurrent;
using Terminal.Gui;
using Terminal.Gui.Trees;
using TLScope.src.Debugging;
using TLScope.src.Models;
using TLScope.src.Controllers;
using TLScope.src.Utilities;

namespace TLScope.src.Views {
    public class NetworkView : Window {
        private readonly TreeView _deviceTreeView;
        private readonly NetworkController _networkController;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private DateTime _lastUpdate = DateTime.MinValue; // Track the last update time

        public NetworkView(NetworkController networkController) : base("Network Information") {
            _networkController = networkController ?? throw new ArgumentNullException(nameof(networkController));
            X = 0;
            Y = 0;
            Width = Dim.Fill();
            Height = Dim.Fill();
            ColorScheme = Constants.TLSColorScheme;

            _deviceTreeView = new TreeView {
                X = 3,
                Y = 3,
                Width = Dim.Fill() - 4,
                Height = Dim.Fill() - 4,
            };
            Add(_deviceTreeView);

            _networkController.DevicesUpdated += OnDevicesUpdated;

            Task.Run(async () => {
                while (!_cancellationTokenSource.Token.IsCancellationRequested) {
                    Application.MainLoop.Invoke(() => UpdateDeviceList());
                    await Task.Delay(5000);
                }
            });
        }

        private void OnDevicesUpdated(ConcurrentDictionary<string, Device> devices) {
            Application.MainLoop.Invoke(() => UpdateDeviceList(devices));
        }

        public void UpdateDeviceList(ConcurrentDictionary<string, Device>? devices = null) {
            devices ??= _networkController.GetActiveDevices();
            if (devices.Count == 0) { return; }
            // Throttle UI updates to once per second
            if ((DateTime.Now - _lastUpdate).TotalSeconds < 1) {
                return;
            }

            _lastUpdate = DateTime.Now;

            // Add logging to capture the state of the device list
            Logging.Write($"Updating device list: {devices.Count} devices.");

            try {
                // Ensure the TreeBuilder is correctly initialized
                _deviceTreeView.TreeBuilder = new DeviceTreeBuilder(devices);
                _deviceTreeView.SetNeedsDisplay();
                Logging.Write("DeviceTreeBuilder built.");
            } catch (IndexOutOfRangeException ex) {
                Logging.Error("An error occurred while updating the device list.", ex);
                // Add additional logging to capture the state
                Logging.Write($"Exception Details: {ex.Message}");
                Logging.Write($"Stack Trace: {ex.StackTrace}");
            } catch (Exception ex) {
                Logging.Error("An unexpected error occurred while updating the device list.", ex);
            } finally {
                Application.Refresh();
                Logging.Write("Device list updated successfully.");
            }
        }

        public class DeviceTreeBuilder : ITreeBuilder<ITreeNode> {
            private readonly ConcurrentDictionary<string, Device> _devices;
            private readonly HashSet<string> _expandedNodes;

            public DeviceTreeBuilder(ConcurrentDictionary<string, Device> devices) {
                _devices = devices;
                _expandedNodes = new HashSet<string>();
            }

            public bool IsExpanded(ITreeNode node) {
                return _expandedNodes.Contains(node.Text);
            }

            public void SetExpanded(ITreeNode node, bool expanded) {
                if (expanded) {
                    _expandedNodes.Add(node.Text);
                } else {
                    _expandedNodes.Remove(node.Text);
                }
            }

            public IEnumerable<ITreeNode> GetChildren(ITreeNode node) {
                if (node is DeviceTreeNode deviceNode) {
                    var device = deviceNode.Device;
                    var children = new List<ITreeNode> {
                        new TreeNode($"IP Address: {device.IPAddress}"),
                        new TreeNode($"MAC Address: {device.MACAddress}"),
                        new TreeNode($"Operating System: {device.OperatingSystem ?? "Unknown"}"),
                        new TreeNode($"Last Seen: {device.LastSeen:u}")
                    };
                    return children;
                } else if (node.Text == "Root") {
                    return _devices.Values.Select(device => new DeviceTreeNode(device));
                }
                return Enumerable.Empty<ITreeNode>();
            }

            public ITreeNode GetRoot() {
                return new TreeNode("Root");
            }

            public bool CanExpand(ITreeNode node) {
                return node is DeviceTreeNode || node.Text == "Root";
            }

            public bool SupportsCanExpand => true;
        }

        public class DeviceTreeNode : TreeNode {
            public Device Device { get; }

            public DeviceTreeNode(Device device) : base(device?.DeviceName ?? "Unknown Device") {
                Device = device ?? throw new ArgumentNullException(nameof(device));
            }
        }
    }
}
