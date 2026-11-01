// FairyGUI for Godot - demo entry point (not a port of a Unity file; Unity had one scene per example).
// Root script of res://demo/Scenes/Main.tscn: shows a menu of the ported examples built with GGraph/GTextField,
// runs the chosen XxxMain node as a child and offers a Back button that frees it.
using Godot;
using FairyGUI;

public partial class DemoMain : Node2D
{
    const int Columns = 4;
    const float ButtonWidth = 250;
    const float ButtonHeight = 64;
    const float GapX = 16;
    const float GapY = 14;
    const float Top = 96;

    static readonly Color BackgroundColor = new Color(0.13f, 0.14f, 0.17f, 1);
    static readonly Color ButtonColor = new Color(0.22f, 0.45f, 0.75f, 1);
    static readonly Color BackButtonColor = new Color(0.8f, 0.3f, 0.25f, 1);

    GComponent _menu;
    GComponent _backBtn;
    Node _current;

    /// <summary>
    /// Name of the running example, or null when the menu is shown.
    /// </summary>
    public string currentExample { get; private set; }

    /// <summary>
    /// The menu component (children "btn_Basics", "btn_Bag", ...). Used by tests/SmokeTest.
    /// </summary>
    public GComponent menu
    {
        get { return _menu; }
    }

    /// <summary>
    /// The Back button shown while an example runs. Used by tests/SmokeTest.
    /// </summary>
    public GComponent backButton
    {
        get { return _backBtn; }
    }

    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        // Every Unity example set Application.targetFrameRate = 60.
        Engine.MaxFps = 60;

        // Unity examples quit on Escape. Here Escape closes the running example, or quits from the menu.
        Stage.inst.onKeyDown.Add(OnKeyDown);

        BuildMenu();
        BuildBackButton();
    }

    public override void _ExitTree()
    {
        if (DemoUtil.IsQuitting)
            return;

        // The running example is a child node: its own _ExitTree has already released its UI.
        _current = null;
        currentExample = null;

        Stage.inst.onKeyDown.Remove(OnKeyDown);
        if (_menu != null)
        {
            _menu.Dispose();
            _menu = null;
        }
        if (_backBtn != null)
        {
            _backBtn.Dispose();
            _backBtn = null;
        }
    }

    void BuildMenu()
    {
        _menu = new GComponent();
        _menu.name = _menu.gameObjectName = "DemoMenu";
        _menu.MakeFullScreen();
        _menu.AddRelation(GRoot.inst, RelationType.Size);

        GGraph bg = new GGraph();
        bg.DrawRect(_menu.width, _menu.height, 0, UColor.clear, BackgroundColor);
        bg.AddRelation(_menu, RelationType.Size);
        _menu.AddChild(bg);

        float left = (DemoUtil.DesignWidth - (Columns * ButtonWidth + (Columns - 1) * GapX)) / 2;

        GTextField title = CreateText("FairyGUI for Godot - Examples", 30, UColor.white);
        title.SetXY(left, 22);
        _menu.AddChild(title);

        GTextField hint = CreateText("Click an example to run it. Esc or Back returns here, Esc on this page quits.", 16,
            new Color(0.7f, 0.7f, 0.7f, 1));
        hint.SetXY(left, 62);
        _menu.AddChild(hint);

        for (int i = 0; i < DemoRegistry.Examples.Length; i++)
        {
            DemoRegistry.Entry entry = DemoRegistry.Examples[i];
            GComponent btn = CreateButton(entry.name, ButtonWidth, ButtonHeight, ButtonColor);
            btn.name = btn.gameObjectName = "btn_" + entry.name;
            btn.SetXY(left + (i % Columns) * (ButtonWidth + GapX), Top + (i / Columns) * (ButtonHeight + GapY));
            btn.data = entry.name;
            btn.onClick.Add(OnClickExample);
            _menu.AddChild(btn);
        }

        GRoot.inst.AddChild(_menu);
    }

    void BuildBackButton()
    {
        _backBtn = CreateButton("Back", 110, 44, BackButtonColor);
        _backBtn.name = _backBtn.gameObjectName = "DemoBack";
        _backBtn.SetXY(GRoot.inst.width - _backBtn.width - 10, 10);
        _backBtn.AddRelation(GRoot.inst, RelationType.Right_Right);
        // stays above everything an example adds to GRoot (windows, popups, modal layers)
        _backBtn.sortingOrder = 100000;
        _backBtn.visible = false;
        // deferred: do not dispose the example's UI in the middle of the click dispatch
        _backBtn.onClick.Add(() => { Callable.From(CloseExample).CallDeferred(); });
        GRoot.inst.AddChild(_backBtn);
    }

    static GTextField CreateText(string text, int size, Color color)
    {
        GTextField tf = new GTextField();
        TextFormat format = tf.textFormat;
        format.size = size;
        format.color = color;
        tf.textFormat = format;
        tf.autoSize = AutoSizeType.Both;
        tf.text = text;
        return tf;
    }

    static GComponent CreateButton(string text, float width, float height, Color color)
    {
        GComponent btn = new GComponent();
        btn.SetSize(width, height);
        btn.opaque = true;

        GGraph bg = new GGraph();
        bg.DrawRoundRect(width, height, color, new float[] { 8, 8, 8, 8 });
        btn.AddChild(bg);

        GTextField label = CreateText(text, 22, UColor.white);
        label.autoSize = AutoSizeType.None;
        label.SetSize(width, height);
        label.align = AlignType.Center;
        label.verticalAlign = VertAlignType.Middle;
        label.touchable = false;
        btn.AddChild(label);

        btn.onRollOver.Add(() => { bg.alpha = 0.8f; });
        btn.onRollOut.Add(() => { bg.alpha = 1f; });
        btn.onTouchBegin.Add(() => { bg.alpha = 0.6f; });
        btn.onTouchEnd.Add(() => { bg.alpha = 1f; });
        return btn;
    }

    void OnClickExample(EventContext context)
    {
        string name = (string)((GObject)context.sender).data;
        // deferred for the same reason as the Back button
        Callable.From(() => RunExample(name)).CallDeferred();
    }

    /// <summary>
    /// Instantiates the XxxMain node of the example (its _Ready builds the UI) and shows the Back button.
    /// </summary>
    public void RunExample(string name)
    {
        CloseExample();

        foreach (DemoRegistry.Entry entry in DemoRegistry.Examples)
        {
            if (entry.name != name)
                continue;

            _menu.visible = false;
            _backBtn.visible = true;
            currentExample = name;

            _current = entry.create();
            _current.Name = name + "Example";
            AddChild(_current);
            GD.Print("[Demo] running " + name);
            return;
        }

        GD.PushWarning("[Demo] unknown example: " + name);
    }

    /// <summary>
    /// Frees the running example (its _ExitTree releases the UI) and shows the menu again.
    /// </summary>
    public void CloseExample()
    {
        if (_current == null)
            return;

        Node node = _current;
        _current = null;
        currentExample = null;

        if (GodotObject.IsInstanceValid(node))
        {
            if (node.GetParent() == this)
                RemoveChild(node); // runs the example's _ExitTree now
            node.QueueFree();
        }

        _backBtn.visible = false;
        _menu.visible = true;
    }

    void OnKeyDown(EventContext context)
    {
        if (context.inputEvent.keyCode == Key.Escape)
        {
            if (_current != null)
                Callable.From(CloseExample).CallDeferred();
            else
                GetTree().Quit();
        }
    }
}
