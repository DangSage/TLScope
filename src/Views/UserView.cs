// Window class to display data on the client's user, including network information

using System.Net.NetworkInformation;

using Terminal.Gui;
using Terminal.Gui.Trees;

using TLScope.src.Controllers;
using TLScope.src.Models;
using TLScope.src.Utilities;

namespace TLScope.src.Views {
    public class UserView : Window {

        private readonly TreeView _userTreeView;

        public UserView(NetworkInterface ni) : base("Client Information") {
            // take up half of the screen
            X = Pos.Percent(66)+1;
            Y = 2;
            Width = Dim.Percent(33) - 3;
            Height = Dim.Percent(50) - 3;
            CanFocus = false;
            ColorScheme = Constants.TLSColorScheme;

            _userTreeView = new TreeView {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus = false,
            };

            var root = new TreeNode("This Device");
            var interfaceNode = new TreeNode($"Operational Status: {ni.OperationalStatus}") {
                Children = {
                    new TreeNode($"Interface Name:        {ni.Name}"),
                    new TreeNode($"Interface Type:        {ni.NetworkInterfaceType}"),
                    new TreeNode($"MAC Address:           {ni.GetPhysicalAddress()}"),
                    new TreeNode($"IPv4 Statistics:       {ni.GetIPv4Statistics()}"),
                    new TreeNode($"Speed:                 {ni.Speed}"),
                    new TreeNode($"Supports Multicast:    {ni.SupportsMulticast}")
                }
            };

            root.Children.Add(interfaceNode);
            _userTreeView.AddObject(root);
            _userTreeView.ExpandAll();
            Add(_userTreeView);
        }
    }
}

