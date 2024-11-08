// Window class to display network information, including active devices and their information

using System.Collections.Concurrent;

using Terminal.Gui;

using TLScope.src.Controllers;
using TLScope.src.Utilities;
using TLScope.src.Debugging;
using TLScope.src.Models;


namespace TLScope.src.Views {
    public class NetView : Window {
        private readonly TreeView _deviceTreeView;
        private bool _isVisible;

        public NetView(ref NetworkController networkController) : base("Network Information") {
            X = 1;
            Y = 2;
            Width = Dim.Fill() - 2;
            Height = Dim.Fill() - 2;
            ColorScheme = Constants.TLSColorScheme;

            _deviceTreeView = new TreeView {
                X = 0,
                Y = 2,
                Width = Dim.Fill() - 2,
                Height = Dim.Fill() - 2,
                CanFocus = true,
                TreeBuilder = new DeviceTreeBuilder(ref networkController.GetActiveDevices())
            };

            Add(_deviceTreeView);

            // Event handlers for visibility
            VisibleChanged += OnVisibleChanged;
        }

        private void OnVisibleChanged(bool isVisible, ref NetworkController nc) {
            _isVisible = isVisible;
            if (_isVisible) {
                UpdateView(ref nc);
            }
        }

        private void OnDevicesUpdated(ConcurrentDictionary<string, Device> devices) {
            if (_isVisible) {
                Application.MainLoop.Invoke(() => {
                    _deviceTreeView.TreeBuilder = new DeviceTreeBuilder(ref devices);
                    _deviceTreeView.SetNeedsDisplay();
                    Logging.Write($"Device list updated: {devices.Count} devices.");
                    LogTreeViewContent();
                });
            }
        }

        public void UpdateView(ref NetworkController nc) {
            try {
                if (_isVisible) {
                    _deviceTreeView.TreeBuilder = new DeviceTreeBuilder(ref nc.GetActiveDevices());
                    _deviceTreeView.SetNeedsDisplay();
                    Logging.Write($"Device list updated: {nc.GetActiveDevices().Count} devices.");
                    LogTreeViewContent();
                }
            } catch (Exception ex) {
                Logging.Error("An error occurred while updating the view.", ex);
            }
        }

        private void LogTreeViewContent() {
            var rootChildren = _deviceTreeView.TreeBuilder.GetChildren(null);
            if (!rootChildren.Any()) {
                Logging.Write("TreeView Root: No devices found.");
                return;
            }

            var root = rootChildren.First();
            Logging.Write($"TreeView Root: {root.Text}");
            foreach (var child in _deviceTreeView.TreeBuilder.GetChildren(root)) {
                Logging.Write($"TreeView Child: {child.Text}");
            }
        }
    }
}
