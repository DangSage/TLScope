using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui.Trees;
using TLScope.src.Models;

namespace TLScope.src.Views {
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
            if (node is Device device) {
                var children = new List<ITreeNode> {
                    new TreeNode($"IP Address: {device.IPAddress}"),
                    new TreeNode($"MAC Address: {device.MACAddress}"),
                    new TreeNode($"Operating System: {device.OperatingSystem ?? "Unknown"}"),
                    new TreeNode($"Last Seen: {device.LastSeen}")
                };
                return children;
            }
            return _devices.Values.Cast<ITreeNode>();
        }

        public ITreeNode GetRoot() {
            return new TreeNode($"Number of Devices: {_devices.Count}");
        }

        public bool CanExpand(ITreeNode node) {
            return node is Device;
        }

        public bool SupportsCanExpand => true;
    }
}
