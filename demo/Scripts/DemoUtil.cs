// FairyGUI for Godot - demo helpers (not a port of a Unity file).
// Shared setup for every example: fonts, content scale, package paths, a stand-in for the Unity
// UIPanel(FitScreen.FitSize) component, UIConfig save/restore and 2D placeholders for the 3D NPCs.
using System;
using System.Collections.Generic;
using Godot;
using FairyGUI;

public static class DemoUtil
{
    public const string UIPath = "res://demo/UI/";
    public const string FontPath = "res://demo/Fonts/afont.ttf";
    public const string IconPath = "res://demo/Icons/";
    public const int DesignWidth = 1136;
    public const int DesignHeight = 640;

    static bool _inited;
    static readonly Random _random = new Random();

    /// <summary>
    /// True once FairyGUI is shutting down (window closed / SceneTree quitting). The Stage and the packages may
    /// already be disposed then, so the examples skip their UI cleanup in _ExitTree.
    /// </summary>
    public static bool IsQuitting
    {
        get { return StageEngine.beingQuit; }
    }

    /// <summary>
    /// Idempotent global setup shared by DemoMain, every XxxMain (so each one also runs on its own) and the smoke test.
    /// </summary>
    public static void EnsureInit()
    {
        if (_inited)
            return;

        // The headless display server reports a 0x0 main window, which would give a content scale factor of 0.
        // Give the root viewport the design size before the Stage reads it (only when it is empty).
        SceneTree tree = Engine.GetMainLoop() as SceneTree;
        if (tree != null && tree.Root != null)
        {
            Vector2I size = tree.Root.Size;
            if (size.X <= 0 || size.Y <= 0)
                tree.Root.Size = new Vector2I(DesignWidth, DesignHeight);
        }

        Stage.Instantiate();
        RegisterFonts();

        // Every example scene used a UIContentScaler with ScaleWithScreenSize 1136x640, MatchWidthOrHeight.
        GRoot.inst.SetContentScaleFactor(DesignWidth, DesignHeight, UIContentScaler.ScreenMatchMode.MatchWidthOrHeight);

        // set last: if a step throws, the next call retries (and reports the error again) instead of running half set up
        _inited = true;
    }

    /// <summary>
    /// Unity examples used UIConfig.defaultFont = "Microsoft YaHei" (an OS font). Here the bundled afont.ttf is the
    /// primary font and a SystemFont with common CJK families is its fallback, because afont.ttf has no CJK glyphs.
    /// </summary>
    static void RegisterFonts()
    {
        Godot.Font baseFont = null;
        if (ResourceLoader.Exists(FontPath))
            baseFont = ResourceLoader.Load<Godot.Font>(FontPath);
        if (baseFont == null && Godot.FileAccess.FileExists(FontPath))
        {
            // not imported yet (e.g. running before `godot --import`): load the raw ttf
            FontFile ff = new FontFile();
            if (ff.LoadDynamicFont(FontPath) == Error.Ok)
                baseFont = ff;
        }

        SystemFont cjk = new SystemFont();
        cjk.FontNames = new string[] {
            "Microsoft YaHei", "PingFang SC", "Hiragino Sans GB", "Noto Sans CJK SC", "Noto Sans SC",
            "Source Han Sans SC", "WenQuanYi Micro Hei", "SimHei", "Arial Unicode MS", "sans-serif" };

        Godot.Font font;
        if (baseFont != null)
        {
            FontVariation fv = new FontVariation();
            fv.BaseFont = baseFont;
            fv.Fallbacks = new Godot.Collections.Array<Godot.Font> { cjk };
            font = fv;
        }
        else
        {
            GD.PushWarning("FairyGUI demo: " + FontPath + " not found, using system fonts.");
            font = cjk;
        }

        FontManager.RegisterFont(new DynamicFont("afont", font), "afont");
        UIConfig.defaultFont = "afont";
    }

    /// <summary>
    /// Unity: UIPackage.AddPackage("UI/Xxx") (Resources path). Returns the already loaded package when present.
    /// </summary>
    public static UIPackage AddPackage(string name)
    {
        return UIPackage.AddPackage(UIPath + name);
    }

    /// <summary>
    /// Replacement for a screen-space UIPanel with fitScreen = FitSize (what every example scene used):
    /// creates the component, makes it full screen, keeps it sized with GRoot and adds it to GRoot.
    /// </summary>
    public static GComponent CreateMainView(string packageName, string componentName = "Main")
    {
        AddPackage(packageName);
        GComponent view = UIPackage.CreateObject(packageName, componentName).asCom;
        view.MakeFullScreen();
        view.AddRelation(GRoot.inst, RelationType.Size);
        GRoot.inst.AddChild(view);
        return view;
    }

    /// <summary>
    /// UnityEngine.Random.Range(int, int): max exclusive.
    /// </summary>
    public static int Range(int min, int max)
    {
        if (max <= min)
            return min;
        return _random.Next(min, max);
    }

    /// <summary>
    /// UnityEngine.Random.Range(float, float): max inclusive.
    /// </summary>
    public static float Range(float min, float max)
    {
        return min + (float)_random.NextDouble() * (max - min);
    }

    /// <summary>
    /// UnityEngine.Random.insideUnitCircle.
    /// </summary>
    public static Vector2 InsideUnitCircle()
    {
        float angle = Range(0f, UMath.PI * 2);
        float r = UMath.Sqrt(Range(0f, 1f));
        return new Vector2(UMath.Cos(angle) * r, UMath.Sin(angle) * r);
    }

    public const float NpcHeadTop = -220;

    /// <summary>
    /// 2D stand-in for the 3D npc models of the HeadBar/EmitNumbers scenes: a Node2D whose origin is at the feet.
    /// When withHeadBar is true a child UIPanel named "HeadBar" (HeadBar/HeadBar, world space) floats above the head,
    /// like the npc prefab in Unity.
    /// </summary>
    public static Node2D CreateNpc(string name, Vector2 position, Color color, bool withHeadBar)
    {
        Node2D npc = new Node2D();
        npc.Name = name;
        npc.Position = position;

        Polygon2D body = new Polygon2D();
        body.Name = "Body";
        body.Color = color;
        body.Polygon = new Vector2[] { new Vector2(-40, 0), new Vector2(-50, -160), new Vector2(50, -160), new Vector2(40, 0) };
        npc.AddChild(body);

        Polygon2D head = new Polygon2D();
        head.Name = "Head";
        head.Color = color.Lightened(0.3f);
        Vector2[] circle = new Vector2[24];
        for (int i = 0; i < circle.Length; i++)
        {
            float a = UMath.PI * 2 * i / circle.Length;
            circle[i] = new Vector2(UMath.Cos(a) * 30, -190 + UMath.Sin(a) * 30);
        }
        head.Polygon = circle;
        npc.AddChild(head);

        if (withHeadBar)
        {
            AddPackage("HeadBar");
            UIPanel panel = new UIPanel();
            panel.Name = "HeadBar";
            panel.packageName = "HeadBar";
            panel.componentName = "HeadBar";
            // cachedUISize of the Unity prefab was 145x106: center it above the head
            panel.Position = new Vector2(-72, NpcHeadTop - 106 - 10);
            npc.AddChild(panel);
        }

        return npc;
    }

    /// <summary>
    /// Saves the UIConfig fields that examples change in Awake, so leaving an example restores the defaults
    /// (in Unity each example was its own scene/process run, here they share one process).
    /// </summary>
    public sealed class UIConfigSnapshot
    {
        readonly string _defaultFont = UIConfig.defaultFont;
        readonly string _verticalScrollBar = UIConfig.verticalScrollBar;
        readonly string _horizontalScrollBar = UIConfig.horizontalScrollBar;
        readonly string _popupMenu = UIConfig.popupMenu;
        readonly string _popupMenuSeperator = UIConfig.popupMenu_seperator;
        readonly string _tooltipsWin = UIConfig.tooltipsWin;
        readonly string _globalModalWaiting = UIConfig.globalModalWaiting;
        readonly string _windowModalWaiting = UIConfig.windowModalWaiting;
        readonly string _loaderErrorSign = UIConfig.loaderErrorSign;
        readonly NAudioClip _buttonSound = UIConfig.buttonSound;
        readonly ScrollBarDisplayType _defaultScrollBarDisplay = UIConfig.defaultScrollBarDisplay;

        public void Restore()
        {
            UIConfig.defaultFont = _defaultFont;
            UIConfig.verticalScrollBar = _verticalScrollBar;
            UIConfig.horizontalScrollBar = _horizontalScrollBar;
            UIConfig.popupMenu = _popupMenu;
            UIConfig.popupMenu_seperator = _popupMenuSeperator;
            UIConfig.tooltipsWin = _tooltipsWin;
            UIConfig.globalModalWaiting = _globalModalWaiting;
            UIConfig.windowModalWaiting = _windowModalWaiting;
            UIConfig.loaderErrorSign = _loaderErrorSign;
            UIConfig.buttonSound = _buttonSound;
            UIConfig.defaultScrollBarDisplay = _defaultScrollBarDisplay;
        }
    }
}
