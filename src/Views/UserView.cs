// Window class to display data on the client's user, including network information

using Terminal.Gui;
using Terminal.Gui.Trees;

using TLScope.src.Controllers;
using TLScope.src.Utilities;

namespace TLScope.src.Views {
    public class UserView : Window {

        private readonly TreeView _userTreeView;

        public UserView(ref NetworkController nc) : base("Client Information") {
            var ni = nc._networkInterface ?? throw new InvalidOperationException("Network interface is null");
            CanFocus = false;
            ColorScheme = Constants.TLSColorScheme;

            _userTreeView = new TreeView {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus = false,
            };

            var root = new TreeNode($"This Device ({System.Environment.MachineName})");
            var interfaceNode = new TreeNode($"Operational Status: {ni.OperationalStatus}") {
                Children = {
                    new TreeNode($"Interface Name:.....{ni.Name}"),
                    new TreeNode($"Interface Type:.....{ni.NetworkInterfaceType}"),
                    new TreeNode($"MAC Address:........{ni.GetPhysicalAddress()}"),
                    new TreeNode($"IPv4:...............{NetData.GetLocalIPAddress(ni)}"),   
                    new TreeNode($"Speed:..............{ni.Speed}"),
                    new TreeNode($"Multicast Support:..{ni.SupportsMulticast}")
                }
            };

            root.Children.Add(interfaceNode);
            _userTreeView.AddObject(root);
            _userTreeView.ExpandAll();
            Add(_userTreeView);
        }
    }
}

