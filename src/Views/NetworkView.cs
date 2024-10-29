using System.Collections.Concurrent;
using System.Threading;
using Terminal.Gui;
using Terminal.Gui.Trees;
using TLScope.src.Debugging;
using TLScope.src.Models;
using TLScope.src.Controllers;
using TLScope.src.Utilities;
using Microsoft.Msagl.Core.Layout;

namespace TLScope.src.Views {
    public class NetworkView : Window {
        private readonly TreeView _deviceTreeView;
        private readonly NetworkController _networkController;
        private Timer _debounceTimer;

        public NetworkView(ref NetworkController networkController) : base("Network Information") {
            _networkController = networkController ?? throw new ArgumentNullException(nameof(networkController));
            _debounceTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);
            X = 1;
            Y = 2;
            Width = Dim.Fill() - 2;
            Height = Dim.Fill() - 2;
            ColorScheme = Constants.TLSColorScheme;

            _deviceTreeView = new TreeView {
                X = 0,
                Y = 0,
                Width = Dim.Fill() - 2,
                Height = Dim.Fill() - 2,
                TreeBuilder = new DeviceTreeBuilder(ref _networkController.GetActiveDevices())
            };

            Add(_deviceTreeView);

            _networkController.DevicesUpdated += OnDevicesUpdated;
        }

        private void Debounce(Action action, int delayMilliseconds) {
            _debounceTimer?.Change(delayMilliseconds, Timeout.Infinite);
        }

        private void OnDevicesUpdated(ConcurrentDictionary<string, Device> devices) {
            Debounce(() => Application.MainLoop.Invoke(() => {
                _deviceTreeView.TreeBuilder = new DeviceTreeBuilder(ref devices);
                _deviceTreeView.SetNeedsDisplay();
                Logging.Write($"Device list updated: {devices.Count} devices.");
            }), 1000);
        }

        public class DeviceTreeBuilder : ITreeBuilder<ITreeNode> {
            private readonly ConcurrentDictionary<string, Device> _devices;

            public DeviceTreeBuilder(ref ConcurrentDictionary<string, Device> devices) {
                _devices = devices;
            }

            public bool IsExpanded(ITreeNode node) {
                return false; // Simplified: no expanded state tracking
            }

            public void SetExpanded(ITreeNode node, bool expanded) {
                // Simplified: no expanded state tracking
            }

            public IEnumerable<ITreeNode> GetChildren(ITreeNode node) {
                // No children needed
                return Enumerable.Empty<ITreeNode>();
            }

            public ITreeNode GetRoot() {
                // Display the number of devices as the root node
                return new TreeNode($"Number of Devices: {_devices.Count}");
            }

            public bool CanExpand(ITreeNode node) {
                return false; // No expandable nodes
            }

            public bool SupportsCanExpand => false;
        }
    }
}
