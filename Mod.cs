// Developer tools: drive and inspect the running game from outside it, for writing mods
// without watching the window (and for automated checks).
//
// MODDEV_DIR=<dir> switches it on. Write commands, one per line, to <dir>/cmd; they are
// run on the next poll (4 a second), the file is removed, and <dir>/done is written
// with one line per command. Commands:
//
//   shot <name>             <dir>/<name>.png of the main window
//   tree <name> [depth]     <dir>/<name>.txt: the scene tree with types, text and sizes
//   press <node>            press a button (emits its "pressed" signal)
//   call <node> <method>    call a method with no arguments on a node
//                           <node> is a path from the root, or a name (the first visible
//                           node called that), or name:n for the nth
//   click <x> <y>           a left click at window coordinates
//   key <name>              press and release a key (Godot key name, e.g. Escape)
//   size <w> <h>            resize the window
//   quit
using System;
using System.IO;
using System.Linq;
using System.Text;
using Godot;

namespace DevTools;

public sealed class DevToolsMod : Umitoo.Mod
{
    private static string s_dir = "";
    private static SceneTree s_tree = null!;

    public override void Ready()
    {
        s_dir = System.Environment.GetEnvironmentVariable("MODDEV_DIR") ?? "";
        if (s_dir == "") return;
        Directory.CreateDirectory(s_dir);
        s_tree = (SceneTree)Engine.GetMainLoop();
        var timer = new Timer { WaitTime = 0.25, Autostart = true, Name = "ModDevTools", ProcessMode = Node.ProcessModeEnum.Always };
        timer.Timeout += Poll;
        s_tree.Root.AddChild(timer);
        File.WriteAllText(Path.Combine(s_dir, "done"), "ready\n");
    }

    private static void Poll()
    {
        string file = Path.Combine(s_dir, "cmd");
        if (!File.Exists(file)) return;
        string[] lines = File.ReadAllLines(file);
        File.Delete(file);
        var report = new StringBuilder();
        foreach (string line in lines.Where(l => l.Trim() != ""))
        {
            try { report.AppendLine(line + " -> " + Run(line.Trim())); }
            catch (Exception e) { report.AppendLine(line + " -> ERROR " + e.Message); }
        }
        File.WriteAllText(Path.Combine(s_dir, "done"), report.ToString());
    }

    private static string Run(string line)
    {
        string[] p = line.Split(' ', 2);
        string arg = p.Length > 1 ? p[1] : "";
        switch (p[0])
        {
            case "shot":
                s_tree.Root.GetTexture().GetImage().SavePng(Path.Combine(s_dir, arg + ".png"));
                return "ok";
            case "tree":
            {
                string[] a = arg.Split(' ');
                int depth = a.Length > 1 ? int.Parse(a[1]) : 99;
                var text = new StringBuilder();
                Dump(s_tree.Root, 0, depth, text);
                File.WriteAllText(Path.Combine(s_dir, a[0] + ".txt"), text.ToString());
                return "ok";
            }
            case "press":
            {
                if (Find(arg) is not BaseButton button) return "no visible button with that path or name";
                button.EmitSignal(BaseButton.SignalName.Pressed);
                return "ok";
            }
            case "call":
            {
                string[] a = arg.Split(' ');
                if (Find(a[0]) is not { } node) return "no visible node with that path or name";
                return "ok " + node.Call(a[1]);
            }
            case "click":
            {
                string[] a = arg.Split(' ');
                var at = new Vector2(float.Parse(a[0]), float.Parse(a[1]));
                foreach (bool pressed in new[] { true, false })
                    Input.ParseInputEvent(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = pressed });
                return "ok";
            }
            case "key":
            {
                Key key = OS.FindKeycodeFromString(arg);
                foreach (bool pressed in new[] { true, false })
                    Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed });
                return "ok";
            }
            case "size":
            {
                string[] a = arg.Split(' ');
                s_tree.Root.Size = new Vector2I(int.Parse(a[0]), int.Parse(a[1]));
                return "ok";
            }
            case "quit":
                s_tree.Quit();
                return "ok";
        }
        return "unknown command";
    }

    // A node by path from the root, or by name: the first visible node called that, or
    // the nth with "name:n".
    private static Node? Find(string what)
    {
        if (what.Contains('/')) return s_tree.Root.GetNodeOrNull(what);
        string[] parts = what.Split(':');
        var found = new System.Collections.Generic.List<Node>();
        Collect(s_tree.Root, parts[0], found);
        int index = parts.Length > 1 ? int.Parse(parts[1]) - 1 : 0;
        return index < found.Count ? found[index] : null;
    }

    private static void Collect(Node node, string name, System.Collections.Generic.List<Node> found)
    {
        if (node is CanvasItem { Visible: false }) return;
        if (node.Name == name) found.Add(node);
        foreach (Node child in node.GetChildren()) Collect(child, name, found);
    }

    private static void Dump(Node node, int level, int maxDepth, StringBuilder text)
    {
        if (node is CanvasItem { Visible: false }) return;
        text.Append(' ', level * 2).Append(node.Name).Append(" (").Append(node.GetType().Name).Append(')');
        if (node is Control c) text.Append($" [{(int)c.GlobalPosition.X},{(int)c.GlobalPosition.Y} {(int)c.Size.X}x{(int)c.Size.Y}]");
        string? label = node switch { Button b => b.Text, Label l => l.Text, LineEdit e => e.Text, _ => null };
        if (!string.IsNullOrEmpty(label)) text.Append(" \"").Append(label.Replace("\n", " ")).Append('"');
        text.AppendLine();
        if (level >= maxDepth) return;
        foreach (Node child in node.GetChildren()) Dump(child, level + 1, maxDepth, text);
    }
}
