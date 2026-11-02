// FairyGUI for Godot - visual comparison tooling, Godot side (Unity counterpart:
// tools/visual-compare/unity/UnityCaptureRunner.cs; both must produce the same file names).
// Runs one example from DemoRegistry in a window, waits, saves a PNG of the viewport, and quits.
//   godot --path . --resolution 1136x640 res://tests/CaptureRunner.tscn -- --example=Basics --out=<dir> [--wait=3] [--subdemos]
// --subdemos also opens every sub-demo (buttons of group "btns", as in Basics) and saves one PNG each.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using FairyGUI;
using Godot;

public partial class CaptureRunner : Node
{
    string _example = "Basics";
    string _out = "user://shots";
    double _wait = 3;
    bool _subdemos;

    public override void _Ready()
    {
        foreach (string a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--example=")) _example = a.Substring(10).Trim('"');
            else if (a.StartsWith("--out=")) _out = a.Substring(6).Trim('"');
            else if (a.StartsWith("--wait=")) double.TryParse(a.Substring(7), NumberStyles.Float, CultureInfo.InvariantCulture, out _wait);
            else if (a == "--subdemos") _subdemos = true;
        }
        Run();
    }

    async void Run()
    {
        try
        {
            DemoUtil.EnsureInit();
            // Same default font on both sides (Unity's Basics example uses it; the others inherit it).
            UIConfig.defaultFont = "Microsoft YaHei";

            Func<Node> create = null;
            foreach (DemoRegistry.Entry e in DemoRegistry.Examples)
                if (string.Equals(e.name, _example, StringComparison.OrdinalIgnoreCase))
                    create = e.create;
            if (create == null)
            {
                GD.PrintErr("[CAPTURE] unknown example: " + _example);
                GetTree().Quit(2);
                return;
            }

            string dir = _out.StartsWith("res://") || _out.StartsWith("user://") ? ProjectSettings.GlobalizePath(_out) : _out;
            DirAccess.MakeDirRecursiveAbsolute(dir);

            AddChild(create());
            await Seconds(_wait);
            await Capture(dir, _example);

            if (_subdemos)
            {
                GComponent view = FindMainView();
                if (view == null)
                    GD.PrintErr("[CAPTURE] no view with a 'btns' group");
                else
                {
                    var buttons = new List<GObject>();
                    for (int i = 0; i < view.numChildren; i++)
                    {
                        GObject c = view.GetChildAt(i);
                        if (c.group != null && c.group.name == "btns")
                            buttons.Add(c);
                    }
                    buttons.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                    GObject back = view.GetChild("btn_Back");
                    foreach (GObject b in buttons)
                    {
                        b.onClick.Call();
                        await Seconds(1.5);
                        await Capture(dir, _example + "__" + b.name.Substring(4));
                        if (back != null && back.visible)
                            back.onClick.Call();
                        await Seconds(0.3);
                    }
                }
            }

            GD.Print("[CAPTURE] done");
            GetTree().Quit(0);
        }
        catch (Exception e)
        {
            GD.PrintErr("[CAPTURE] " + e);
            GetTree().Quit(1);
        }
    }

    async Task Seconds(double s)
    {
        ulong end = Time.GetTicksMsec() + (ulong)(s * 1000);
        while (Time.GetTicksMsec() < end)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    async Task Capture(string dir, string name)
    {
        // two frames so everything changed so far has been drawn into the viewport texture
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Godot.Image img = GetViewport().GetTexture().GetImage();
        string path = System.IO.Path.Combine(dir, Sanitize(name) + ".png");
        Godot.Error err = img.SavePng(path);
        GD.Print("[CAPTURE] " + path + " " + img.GetWidth() + "x" + img.GetHeight() + (err == Godot.Error.Ok ? "" : " ERROR " + err));
    }

    static GComponent FindMainView()
    {
        GRoot root = GRoot.inst;
        for (int i = 0; i < root.numChildren; i++)
        {
            GComponent com = root.GetChildAt(i) as GComponent;
            if (com == null)
                continue;
            for (int j = 0; j < com.numChildren; j++)
            {
                GObject c = com.GetChildAt(j);
                if (c.group != null && c.group.name == "btns")
                    return com;
            }
        }
        return null;
    }

    static string Sanitize(string s)
    {
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }
}
