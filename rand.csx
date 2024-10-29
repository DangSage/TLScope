#r "nuget: Terminal.Gui, 1.17.1.0"

using System;
using Terminal.Gui;
using Terminal.Gui.Trees;

using TLScope.src.Utilities;
using TLScope.src.Views;
using TLScope.src.Models;

// Create a simple tree view with a single node
Application.Init();
var top = Application.Top;

var win = new Window("Tree View") {
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

// build a tree using the tree builder in network view
var tree = new TreeView {
    X = 0,
    Y = 0,
    Width = Dim.Fill(),
    Height = Dim.Fill(),
    ColorScheme = new ColorScheme {
        Normal = Terminal.Gui.Attribute.Make(Color.White, Color.Black),
        Focus = Terminal.Gui.Attribute.Make(Color.White, Color.DarkGray),
        HotNormal = Terminal.Gui.Attribute.Make(Color.White, Color.DarkGray),
        HotFocus = Terminal.Gui.Attribute.Make(Color.White, Color.DarkGray),
    }
};

var root = new TreeNode("Root");
var child1 = new TreeNode("Child 1");
var child2 = new TreeNode("Child 2");

root.Add(child1);
root.Add(child2);

tree.AddNode(root);

win.Add(tree);

top.Add(win);
top.Add(new MenuBar(new MenuBarItem[] {
    new MenuBarItem("_File", new MenuItem[] {
        new MenuItem("_Quit", "", () => Application.RequestStop(), null, null, Key.Q | Key.CtrlMask)
    })
}));

Application.Run();
Application.Shutdown();