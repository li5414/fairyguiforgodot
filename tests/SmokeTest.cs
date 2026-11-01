// FairyGUI for Godot - headless integration smoke test (not a port of a Unity file).
//
//   godot --headless --path . --import
//   godot --headless --path . res://tests/SmokeTest.tscn            (add `-- --skip-demos` to skip the example phase)
//
// Loads every package in res://demo/UI, instantiates every exported component, exercises GList virtual scrolling,
// Transition, Controller, GTween, Window/Popup and InputTextField focus + typing, then runs every ported example for a
// few frames. Each step catches and logs exceptions with the package/component name; engine errors (GD.PushError,
// unhandled exceptions in callbacks) are counted by SmokeErrorLogger. Prints a PASS/FAIL summary and quits with exit
// code 0 (pass) / 1 (fail). A per-phase budget, a global frame/time budget and a watchdog thread (exit code 3)
// make sure it never hangs.
using System;
using System.Collections;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using Godot;
using FairyGUI;
using Window = FairyGUI.Window;
using PopupMenu = FairyGUI.PopupMenu;

public partial class SmokeTest : Node
{
    const int MaxFrames = 30000;
    const double MaxSeconds = 240;
    const double PhaseSeconds = 90;
    const int PhaseFrames = 9000;
    const double WatchdogStallSeconds = 45;
    const double WatchdogQuitSeconds = 30;
    const int ComponentsPerFrame = 8;

    struct Phase
    {
        public string name;
        public Func<IEnumerator> body;

        public Phase(string name, Func<IEnumerator> body)
        {
            this.name = name;
            this.body = body;
        }
    }

    readonly List<string> _failures = new List<string>();
    readonly List<GObject> _trash = new List<GObject>();
    readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
    readonly List<Phase> _phases = new List<Phase>();

    SmokeErrorLogger _logger;
    bool _loggerAdded;
    string _context = "";
    int _checks;
    int _phaseIndex = -1;
    int _phaseFailuresAtStart;
    int _phaseErrorsAtStart;
    double _phaseStart;
    int _phaseStartFrame;
    int _frames;
    bool _finished;
    bool _skipDemos;
    DemoUtil.UIConfigSnapshot _config;
    readonly List<UIPackage> _packages = new List<UIPackage>();

    // watchdog (read by a background thread)
    static readonly Stopwatch _clock = Stopwatch.StartNew();
    static long _lastTickMs;
    static volatile bool _quitRequested;
    static long _quitRequestedMs;
    static volatile string _watchContext = "";
    System.Threading.Thread _watchdog;

    double Now
    {
        get { return _clock.Elapsed.TotalSeconds; }
    }

    // [smoke] helper: a Window that records its life cycle callbacks.
    class SmokeWindow : Window
    {
        public int inited;
        public int shown;
        public int hidden;

        protected override void OnInit()
        {
            inited++;
            this.contentPane = UIPackage.CreateObject("Basics", "WindowA").asCom;
            this.Center();
        }

        protected override void OnShown()
        {
            shown++;
        }

        protected override void OnHide()
        {
            hidden++;
        }
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        // a predictable frame rate, so the frame waits below mean roughly the same time on every machine
        Engine.MaxFps = 120;

        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--skip-demos")
                _skipDemos = true;
        }

        _logger = new SmokeErrorLogger();
        OS.AddLogger(_logger);
        _loggerAdded = true;
        _logger.active = true;

        // before anything creates the Stage: DemoUtil gives the 0x0 headless root viewport the design size
        try
        {
            DemoUtil.EnsureInit();
        }
        catch (Exception e)
        {
            Fail("DemoUtil.EnsureInit: " + e);
        }

        System.Threading.Interlocked.Exchange(ref _lastTickMs, _clock.ElapsedMilliseconds);
        _watchdog = new System.Threading.Thread(WatchdogLoop);
        _watchdog.IsBackground = true;
        _watchdog.Name = "SmokeTestWatchdog";
        _watchdog.Start();

        _phases.Add(new Phase("Init", PhaseInit));
        _phases.Add(new Phase("Packages", PhasePackages));
        _phases.Add(new Phase("Components", PhaseComponents));
        _phases.Add(new Phase("VirtualList", PhaseVirtualList));
        _phases.Add(new Phase("Transition", PhaseTransition));
        _phases.Add(new Phase("Controller", PhaseController));
        _phases.Add(new Phase("Tween", PhaseTween));
        _phases.Add(new Phase("Window", PhaseWindow));
        _phases.Add(new Phase("Input", PhaseInput));
        if (!_skipDemos)
            _phases.Add(new Phase("Demos", PhaseDemos));

        GD.Print("[SMOKE] FairyGUI for Godot smoke test, " + _phases.Count + " phases, Godot "
            + (string)Engine.GetVersionInfo()["string"]);
        NextPhase();
    }

    public override void _ExitTree()
    {
        // the test node can leave the tree before Finish (e.g. the window is closed): never leave the logger behind
        RemoveLogger();
    }

    void RemoveLogger()
    {
        if (_loggerAdded)
        {
            _loggerAdded = false;
            _logger.active = false;
            OS.RemoveLogger(_logger);
        }
    }

    public override void _Process(double delta)
    {
        if (_finished)
            return;

        _frames++;
        System.Threading.Interlocked.Exchange(ref _lastTickMs, _clock.ElapsedMilliseconds);

        if (_frames > MaxFrames || Now > MaxSeconds)
        {
            Fail("global budget exceeded (" + _frames + " frames, " + Now.ToString("F1") + " s), aborting");
            Finish();
            return;
        }

        if (Now - _phaseStart > PhaseSeconds || _frames - _phaseStartFrame > PhaseFrames)
        {
            Fail("phase budget exceeded, aborting the phase");
            NextPhase();
            return;
        }

        bool running;
        try
        {
            running = Step();
        }
        catch (Exception e)
        {
            Fail("exception: " + e);
            running = false;
        }

        if (!running)
            NextPhase();
    }

    // One step of the current phase: runs until the next yield (nested IEnumerators are run inline).
    bool Step()
    {
        while (_stack.Count > 0)
        {
            IEnumerator top = _stack.Peek();
            if (!top.MoveNext())
            {
                _stack.Pop();
                continue;
            }

            if (top.Current is IEnumerator nested)
            {
                _stack.Push(nested);
                continue;
            }

            return true; // yield null: wait one frame
        }
        return false;
    }

    void NextPhase()
    {
        if (_phaseIndex >= 0)
            EndPhase();

        _stack.Clear();
        _phaseIndex++;
        if (_phaseIndex >= _phases.Count)
        {
            Finish();
            return;
        }

        Phase phase = _phases[_phaseIndex];
        SetContext(phase.name);
        _phaseStart = Now;
        _phaseStartFrame = _frames;
        _phaseFailuresAtStart = _failures.Count;
        _phaseErrorsAtStart = _logger.errorCount;
        _config = new DemoUtil.UIConfigSnapshot();
        GD.Print("[SMOKE] phase " + phase.name + " ...");
        try
        {
            _stack.Push(phase.body());
        }
        catch (Exception e)
        {
            Fail("exception: " + e);
        }
    }

    void EndPhase()
    {
        Phase phase = _phases[_phaseIndex];
        SetContext(phase.name + "/cleanup");

        Try("hide popups", () => { if (GRoot.inst.hasAnyPopup) GRoot.inst.HidePopup(); });
        Try("close modal wait", () => GRoot.inst.CloseModalWait());
        Try("close windows", () => GRoot.inst.CloseAllWindows());
        for (int i = _trash.Count - 1; i >= 0; i--)
        {
            GObject obj = _trash[i];
            Try("dispose " + DescribeObject(obj), () => { if (!obj.isDisposed) obj.Dispose(); });
        }
        _trash.Clear();
        _config?.Restore();
        _config = null;

        int failures = _failures.Count - _phaseFailuresAtStart;
        int errors = _logger.errorCount - _phaseErrorsAtStart;
        GD.Print("[SMOKE] phase " + phase.name + ": " + (failures == 0 && errors == 0 ? "ok" : "FAILED")
            + " (" + failures + " failed checks, " + errors + " engine errors, "
            + (Now - _phaseStart).ToString("F1") + " s, " + (_frames - _phaseStartFrame) + " frames)");
    }

    void Finish()
    {
        if (_finished)
            return;
        _finished = true;

        _logger.active = false;
        List<string> engineErrors = _logger.GetMessages();
        int engineErrorCount = _logger.errorCount;
        int failures = _failures.Count + engineErrorCount;

        GD.Print("");
        GD.Print("[SMOKE] ================= SUMMARY =================");
        GD.Print("[SMOKE] checks: " + _checks + ", failed checks: " + _failures.Count
            + ", engine errors: " + engineErrorCount + ", engine warnings: " + _logger.warningCount);
        foreach (string f in _failures)
            GD.PrintErr("[SMOKE] FAIL " + f);
        foreach (string e in engineErrors)
            GD.PrintErr("[SMOKE] ENGINE ERROR " + e);
        if (engineErrorCount > engineErrors.Count)
            GD.PrintErr("[SMOKE] ... " + (engineErrorCount - engineErrors.Count) + " more engine errors");
        GD.Print("[SMOKE] " + (failures == 0 ? "PASS" : "FAIL") + " (" + _frames + " frames, " + Now.ToString("F1") + " s)");

        RemoveLogger();

        System.Threading.Interlocked.Exchange(ref _quitRequestedMs, _clock.ElapsedMilliseconds);
        _quitRequested = true;
        GetTree().Quit(failures == 0 ? 0 : 1);
    }

    // Kills the process if the main loop stops ticking (a hang inside the runtime) or quitting takes too long.
    static void WatchdogLoop()
    {
        while (true)
        {
            System.Threading.Thread.Sleep(500);
            long now = _clock.ElapsedMilliseconds;
            if (_quitRequested)
            {
                if (now - System.Threading.Interlocked.Read(ref _quitRequestedMs) > WatchdogQuitSeconds * 1000)
                    ForceExit("quitting did not finish within " + WatchdogQuitSeconds + " s");
            }
            else if (now - System.Threading.Interlocked.Read(ref _lastTickMs) > WatchdogStallSeconds * 1000)
            {
                ForceExit("main loop stalled for more than " + WatchdogStallSeconds + " s in [" + _watchContext + "]");
            }
        }
    }

    static void ForceExit(string reason)
    {
        // Console, not GD.Print: the main thread may be stuck while holding engine locks.
        Console.Error.WriteLine("[SMOKE] WATCHDOG: " + reason + ". FAIL (exit code 3)");
        Console.Error.Flush();

        System.Threading.Thread killer = new System.Threading.Thread(() =>
        {
            System.Threading.Thread.Sleep(5000);
            try { System.Diagnostics.Process.GetCurrentProcess().Kill(); } catch { }
        });
        killer.IsBackground = true;
        killer.Start();

        System.Environment.Exit(3);
    }

    //------------------------------------------------------------------ helpers

    void SetContext(string context)
    {
        _context = context;
        _watchContext = context;
        if (_logger != null)
            _logger.context = context;
    }

    void Fail(string message)
    {
        string msg = "[" + _context + "] " + message;
        _failures.Add(msg);
        GD.PrintErr("[SMOKE] FAIL " + msg);
    }

    bool Check(bool condition, string what)
    {
        _checks++;
        if (!condition)
            Fail("check failed: " + what);
        return condition;
    }

    /// <summary>
    /// Runs action, logging an exception as a failure with the current context. Returns false on exception.
    /// </summary>
    bool Try(string what, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception e)
        {
            Fail(what + ": " + e);
            return false;
        }
    }

    static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++)
            yield return null;
    }

    /// <summary>
    /// Waits until condition() is true or timeoutSeconds elapsed (at least 2 frames). The result is written to
    /// result[0] (true = condition met).
    /// </summary>
    IEnumerator WaitUntil(Func<bool> condition, double timeoutSeconds, bool[] result)
    {
        double start = Now;
        int frames = 0;
        result[0] = false;
        while (true)
        {
            bool met;
            if (!EvaluateCondition(condition, out met))
                yield break;
            if (met)
            {
                result[0] = true;
                yield break;
            }
            if (Now - start > timeoutSeconds && frames >= 2)
                yield break;
            frames++;
            yield return null;
        }
    }

    bool EvaluateCondition(Func<bool> condition, out bool met)
    {
        met = false;
        try
        {
            met = condition();
            return true;
        }
        catch (Exception e)
        {
            Fail("wait condition: " + e);
            return false;
        }
    }

    static string DescribeObject(GObject obj)
    {
        if (obj == null)
            return "null";
        if (obj.packageItem != null && obj.packageItem.owner != null)
            return obj.packageItem.owner.name + "/" + obj.packageItem.name;
        return obj.GetType().Name + "(" + obj.name + ")";
    }

    T Keep<T>(T obj) where T : GObject
    {
        if (obj != null)
            _trash.Add(obj);
        return obj;
    }

    void PushMouse(Vector2 stagePos, bool? pressed)
    {
        // stage coordinates == root viewport coordinates (the stage CanvasLayer has an identity transform)
        if (pressed == null)
        {
            InputEventMouseMotion mm = new InputEventMouseMotion();
            mm.Position = stagePos;
            mm.GlobalPosition = stagePos;
            GetViewport().PushInput(mm, true);
        }
        else
        {
            InputEventMouseButton mb = new InputEventMouseButton();
            mb.ButtonIndex = MouseButton.Left;
            mb.Pressed = pressed.Value;
            mb.Position = stagePos;
            mb.GlobalPosition = stagePos;
            GetViewport().PushInput(mb, true);
        }
    }

    /// <summary>
    /// A real click through Godot input -> StageEngine._Input -> Stage hit test: move, press, (next frame) release.
    /// </summary>
    IEnumerator Click(GObject target)
    {
        Vector2 pos = Vector2.Zero;
        if (!Try("locate " + DescribeObject(target), () => { pos = target.LocalToGlobal(new Vector2(target.width / 2, target.height / 2)); }))
            yield break;

        PushMouse(pos, null);
        yield return null;
        PushMouse(pos, true);
        yield return null;
        PushMouse(pos, false);
        yield return null;
    }

    void PushKey(Key keycode, long unicode, bool pressed)
    {
        InputEventKey ev = new InputEventKey();
        ev.Keycode = keycode;
        ev.Unicode = unicode;
        ev.Pressed = pressed;
        GetViewport().PushInput(ev, true);
    }

    IEnumerator TypeText(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            Key key = Key.None;
            if (c >= 'a' && c <= 'z')
                key = (Key)(c - 'a' + 'A');
            else if (c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == ' ')
                key = (Key)c;
            PushKey(key, c, true);
            PushKey(key, 0, false);
            yield return null;
        }
    }

    static GObject ChildOf(GComponent com, string name)
    {
        return com != null && !com.isDisposed ? com.GetChild(name) : null;
    }

    void CallClick(GComponent com, string childName)
    {
        GObject obj = ChildOf(com, childName);
        if (Check(obj != null, "child '" + childName + "' exists"))
            Try("click " + childName, () => obj.onClick.Call());
    }

    //------------------------------------------------------------------ phases

    IEnumerator PhaseInit()
    {
        Try("DemoUtil.EnsureInit", DemoUtil.EnsureInit);
        Check(StageEngine.inst != null && GodotObject.IsInstanceValid(StageEngine.inst), "StageEngine autoload exists");
        Check(Stage.inst != null, "Stage.inst");
        Check(GRoot.inst != null, "GRoot.inst");
        Check(UIConfig.defaultFont == "afont", "UIConfig.defaultFont registered");
        Try("FontManager.GetFont(defaultFont)", () => Check(FontManager.GetFont(UIConfig.defaultFont) is DynamicFont, "default font resolves to the registered DynamicFont"));
        Check(UIContentScaler.scaleFactor > 0 && !float.IsNaN(UIContentScaler.scaleFactor) && !float.IsInfinity(UIContentScaler.scaleFactor),
            "content scale factor is a positive number (" + UIContentScaler.scaleFactor + ")");
        Check(GRoot.inst.width > 0 && GRoot.inst.height > 0, "GRoot has a size (" + GRoot.inst.width + "x" + GRoot.inst.height + ")");
        GD.Print("[SMOKE] stage " + Stage.inst.stageWidth + "x" + Stage.inst.stageHeight + ", GRoot "
            + GRoot.inst.width + "x" + GRoot.inst.height + ", scale " + UIContentScaler.scaleFactor);
        yield return Frames(2);
    }

    IEnumerator PhasePackages()
    {
        List<string> names = new List<string>();
        Try("list " + DemoUtil.UIPath, () =>
        {
            foreach (string file in DirAccess.GetFilesAt(DemoUtil.UIPath))
            {
                if (file.EndsWith("_fui.bytes"))
                    names.Add(file.Substring(0, file.Length - "_fui.bytes".Length));
            }
        });
        names.Sort(StringComparer.Ordinal);
        Check(names.Count > 0, "found *_fui.bytes packages in " + DemoUtil.UIPath);

        foreach (string name in names)
        {
            SetContext("Packages/" + name);
            UIPackage pkg = null;
            if (Try("AddPackage", () => { pkg = UIPackage.AddPackage(DemoUtil.UIPath + name); }))
            {
                if (Check(pkg != null, "AddPackage returned a package"))
                {
                    Check(UIPackage.GetByName(pkg.name) == pkg, "GetByName(" + pkg.name + ")");
                    if (!_packages.Contains(pkg))
                        _packages.Add(pkg);
                }
            }
            yield return null;
        }

        // all packages are loaded now, so cross-package dependencies resolve
        foreach (UIPackage pkg in _packages)
        {
            SetContext("Packages/" + pkg.name + "/LoadAllAssets");
            Try("LoadAllAssets", () => pkg.LoadAllAssets());
            yield return null;
        }
        GD.Print("[SMOKE] loaded " + _packages.Count + " packages");
    }

    IEnumerator PhaseComponents()
    {
        int total = 0;
        foreach (UIPackage pkg in _packages)
        {
            List<PackageItem> items = null;
            SetContext("Components/" + pkg.name);
            if (!Try("GetItems", () => { items = pkg.GetItems(); }) || items == null)
                continue;

            List<KeyValuePair<string, GComponent>> created = new List<KeyValuePair<string, GComponent>>();
            int n = 0;
            foreach (PackageItem pi in items)
            {
                if (!pi.exported || pi.type != PackageItemType.Component)
                    continue;

                string label = pkg.name + "/" + pi.name;
                SetContext("Components/" + label);
                GObject obj = null;
                if (Try("CreateObject", () => { obj = UIPackage.CreateObject(pkg.name, pi.name); }))
                {
                    if (Check(obj != null, "CreateObject returned an object"))
                    {
                        Keep(obj);
                        Try("GRoot.AddChild", () => GRoot.inst.AddChild(obj));
                        if (obj is GComponent com)
                            created.Add(new KeyValuePair<string, GComponent>(label, com));
                        total++;
                    }
                }

                if (++n % ComponentsPerFrame == 0)
                    yield return null;
            }

            SetContext("Components/" + pkg.name + "/frames");
            yield return Frames(10);

            foreach (KeyValuePair<string, GComponent> kv in created)
            {
                SetContext("Components/" + kv.Key + "/controllers+transitions");
                GComponent com = kv.Value;
                Try("controllers", () => ExerciseControllers(com));
                Try("play transitions", () =>
                {
                    foreach (Transition t in com.Transitions)
                        t.Play();
                });
            }
            SetContext("Components/" + pkg.name + "/frames");
            yield return Frames(5);

            foreach (KeyValuePair<string, GComponent> kv in created)
            {
                SetContext("Components/" + kv.Key + "/dispose");
                GComponent com = kv.Value;
                Try("stop transitions", () =>
                {
                    foreach (Transition t in com.Transitions)
                        t.Stop();
                });
                Try("Dispose", () => com.Dispose());
                _trash.Remove(com);
            }
            yield return null;
        }

        SetContext("Components");
        Check(total > 0, "instantiated exported components");
        GD.Print("[SMOKE] instantiated " + total + " exported components");
    }

    void ExerciseControllers(GComponent com)
    {
        foreach (Controller c in com.Controllers)
        {
            int saved = c.selectedIndex;
            for (int i = 0; i < c.pageCount; i++)
            {
                c.selectedIndex = i;
                Check(c.selectedIndex == i, c.name + ".selectedIndex = " + i);
            }
            if (saved >= 0 && saved < c.pageCount)
                c.selectedIndex = saved;
        }
    }

    IEnumerator PhaseVirtualList()
    {
        bool[] ok = new bool[1];

        GComponent view = Keep(UIPackage.CreateObject("VirtualList", "Main").asCom);
        view.MakeFullScreen();
        GRoot.inst.AddChild(view);
        GList list = view.GetChild("mailList").asList;
        if (!Check(list != null, "VirtualList/Main has mailList"))
            yield break;

        HashSet<int> rendered = new HashSet<int>();
        int renderCalls = 0;
        list.SetVirtual();
        list.itemRenderer = (int index, GObject item) =>
        {
            renderCalls++;
            rendered.Add(index);
            item.text = index + " Mail title here";
        };
        list.numItems = 1000;
        yield return Frames(3);

        Check(list.numItems == 1000, "numItems == 1000");
        Check(list.numChildren > 0 && list.numChildren < 1000, "virtual list creates only visible items (" + list.numChildren + " children)");
        Check(rendered.Contains(0), "item 0 rendered");

        list.scrollPane.ScrollBottom(false);
        yield return WaitUntil(() => rendered.Contains(999), 3, ok);
        Check(ok[0], "ScrollBottom renders the last item");

        list.ScrollToView(500, false);
        yield return WaitUntil(() => rendered.Contains(500), 3, ok);
        Check(ok[0], "ScrollToView(500) renders item 500");
        Check(list.numChildren < 1000, "still virtual after scrolling (" + list.numChildren + " children)");

        list.scrollPane.ScrollTop(false);
        yield return Frames(3);
        Check(list.scrollPane.posY == 0, "ScrollTop resets posY (" + list.scrollPane.posY + ")");

        // mouse wheel through the real input path (hit test -> ScrollPane wheel handler)
        Vector2 center = list.LocalToGlobal(new Vector2(list.width / 2, list.height / 2));
        InputEventMouseButton wheel = new InputEventMouseButton();
        wheel.ButtonIndex = MouseButton.WheelDown;
        wheel.Pressed = true;
        wheel.Factor = 1;
        wheel.Position = center;
        wheel.GlobalPosition = center;
        PushMouse(center, null);
        GetViewport().PushInput(wheel, true);
        yield return Frames(20);

        list.numItems = 10;
        yield return Frames(3);
        Check(list.numChildren <= 10, "numItems = 10 shrinks the children (" + list.numChildren + ")");

        // loop list
        GComponent loopView = Keep(UIPackage.CreateObject("LoopList", "Main").asCom);
        loopView.MakeFullScreen();
        GRoot.inst.AddChild(loopView);
        GList loop = loopView.GetChild("list").asList;
        if (Check(loop != null, "LoopList/Main has list"))
        {
            loop.SetVirtualAndLoop();
            loop.itemRenderer = (int index, GObject item) => { item.icon = UIPackage.GetItemURL("LoopList", "n" + (index + 1)); };
            loop.numItems = 5;
            yield return Frames(2);
            loop.scrollPane.SetPosX(loop.scrollPane.contentWidth * 3, false);
            yield return Frames(3);
            Check(loop.numChildren > 0, "loop list has children after scrolling far (" + loop.numChildren + ")");
            GD.Print("[SMOKE] virtual list: " + renderCalls + " render calls, loop list children " + loop.numChildren);
        }
    }

    IEnumerator PhaseTransition()
    {
        bool[] ok = new bool[1];

        GComponent boss = Keep(UIPackage.CreateObject("Transition", "BOSS").asCom);
        GRoot.inst.AddChild(boss);
        Transition t0 = boss.GetTransition("t0");
        if (Check(t0 != null, "BOSS has transition t0"))
        {
            bool completed = false;
            t0.Play(() => { completed = true; });
            Check(t0.playing, "t0.playing after Play()");
            yield return WaitUntil(() => completed, 10, ok);
            Check(ok[0], "BOSS t0 completes");
            Check(!t0.playing, "t0 not playing after completion");
        }

        GComponent powerUp = Keep(UIPackage.CreateObject("Transition", "PowerUp").asCom);
        GRoot.inst.AddChild(powerUp);
        Transition p0 = powerUp.GetTransition("t0");
        if (Check(p0 != null, "PowerUp has transition t0"))
        {
            bool hooked = false;
            bool completed = false;
            p0.SetHook("play_num_now", () => { hooked = true; });
            p0.Play(() => { completed = true; });
            yield return WaitUntil(() => completed, 10, ok);
            Check(ok[0], "PowerUp t0 completes");
            Check(hooked, "PowerUp hook play_num_now called");

            // Stop(setToComplete, processCallback)
            completed = false;
            p0.Play(() => { completed = true; });
            yield return Frames(3);
            p0.Stop(true, true);
            Check(!p0.playing, "Stop() stops the transition");
            Check(completed, "Stop(true, true) calls the complete callback");

            // times + delay
            completed = false;
            p0.Play(2, 0.1f, () => { completed = true; });
            yield return WaitUntil(() => completed, 15, ok);
            Check(ok[0], "Play(2, 0.1f) completes");
        }

        // a Transition that loops forever (Basics/Demo_Transition), stopped by hand
        GComponent demo = Keep(UIPackage.CreateObject("Basics", "Demo_Transition").asCom);
        GRoot.inst.AddChild(demo);
        Transition peng = demo.GetChild("n3").asCom.GetTransition("peng");
        peng.Play(int.MaxValue, 0, null);
        yield return Frames(10);
        Check(peng.playing, "endless transition is playing");
        peng.Stop();
        Check(!peng.playing, "endless transition stops");
    }

    IEnumerator PhaseController()
    {
        GComponent view = Keep(UIPackage.CreateObject("Basics", "Main").asCom);
        view.MakeFullScreen();
        GRoot.inst.AddChild(view);
        Controller c1 = view.GetController("c1");
        if (!Check(c1 != null, "Basics/Main has controller c1"))
            yield break;

        Check(c1.pageCount >= 2, "c1 has at least 2 pages (" + c1.pageCount + ")");
        int changed = 0;
        c1.onChanged.Add(() => { changed++; });

        int start = c1.selectedIndex;
        for (int i = 0; i < c1.pageCount; i++)
        {
            c1.selectedIndex = i;
            yield return null;
            Check(c1.selectedIndex == i, "selectedIndex = " + i);
        }
        int expected = c1.pageCount - (start == 0 ? 1 : 0);
        Check(changed == expected, "onChanged fired " + changed + " times (expected " + expected + ")");

        string page0 = c1.GetPageName(0);
        c1.selectedPage = page0;
        yield return null;
        Check(c1.selectedIndex == 0, "selectedPage = '" + page0 + "'");

        // controllers of every component were already switched in the Components phase; here gears get frames
        c1.selectedIndex = 1;
        yield return Frames(30); // gear tweens (GearXY etc.) run
        c1.selectedIndex = 0;
        yield return Frames(30);
    }

    IEnumerator PhaseTween()
    {
        bool[] ok = new bool[1];

        float last = -1;
        int updates = 0;
        bool completed = false;
        GTween.To(0, 100, 0.3f).SetEase(EaseType.Linear)
            .OnUpdate((GTweener t) => { last = t.value.x; updates++; })
            .OnComplete(() => { completed = true; });
        yield return WaitUntil(() => completed, 5, ok);
        Check(ok[0], "GTween.To completes");
        Check(UMath.Abs(last - 100) < 0.01f, "GTween.To ends at the end value (" + last + ")");
        Check(updates > 1, "OnUpdate called (" + updates + " times)");

        GGraph g = Keep(new GGraph());
        g.DrawRect(50, 50, 1, UColor.black, UColor.red);
        GRoot.inst.AddChild(g);
        completed = false;
        g.TweenMove(new Vector2(200, 150), 0.2f).OnComplete(() => { completed = true; });
        Check(GTween.IsTweening(g), "IsTweening(target) during TweenMove");
        yield return WaitUntil(() => completed, 5, ok);
        Check(ok[0], "TweenMove completes");
        Check(UMath.Abs(g.x - 200) < 0.5f && UMath.Abs(g.y - 150) < 0.5f, "TweenMove ends at (200,150): (" + g.x + "," + g.y + ")");

        g.TweenFade(0, 5f);
        yield return Frames(2);
        GTween.Kill(g, true);
        Check(UMath.Abs(g.alpha) < 0.01f, "Kill(target, complete: true) applies the end value (alpha " + g.alpha + ")");
        Check(!GTween.IsTweening(g), "not tweening after Kill");

        // a tween on a GObject that gets disposed is killed silently
        GGraph g2 = new GGraph();
        g2.DrawRect(10, 10, 0, UColor.clear, UColor.white);
        GRoot.inst.AddChild(g2);
        bool g2Completed = false;
        g2.TweenScale(new Vector2(2, 2), 0.2f).OnComplete(() => { g2Completed = true; });
        yield return null;
        g2.Dispose();
        yield return Frames(30);
        Check(!g2Completed, "tween of a disposed target does not complete");
        Check(!GTween.IsTweening(g2), "tween of a disposed target is killed");

        bool called = false;
        GTween.DelayedCall(0.1f).OnComplete(() => { called = true; });
        yield return WaitUntil(() => called, 5, ok);
        Check(ok[0], "DelayedCall fires");

        completed = false;
        float maxValue = 0;
        GTween.To(0, 1, 0.1f).SetRepeat(1, true)
            .OnUpdate((GTweener t) => { maxValue = UMath.Max(maxValue, t.value.x); })
            .OnComplete(() => { completed = true; });
        yield return WaitUntil(() => completed, 5, ok);
        Check(ok[0], "SetRepeat(1, yoyo) completes");
        Check(maxValue > 0.9f, "yoyo tween reached the end value (" + maxValue + ")");
    }

    IEnumerator PhaseWindow()
    {
        bool[] ok = new bool[1];

        SmokeWindow win = Keep(new SmokeWindow());
        win.Show();
        Check(win.inited == 1, "OnInit called once");
        Check(win.isShowing, "isShowing after Show()");
        yield return Frames(3);
        Check(win.shown >= 1, "OnShown called");

        win.Hide();
        yield return Frames(3);
        Check(!win.isShowing, "not showing after Hide()");
        Check(win.hidden >= 1, "OnHide called");

        win.modal = true;
        win.Show();
        yield return null;
        Check(GRoot.inst.hasModalWindow, "GRoot.hasModalWindow with a modal window");

        UIConfig.windowModalWaiting = "ui://ModalWaiting/WindowModalWaiting";
        UIConfig.globalModalWaiting = "ui://ModalWaiting/GlobalModalWaiting";
        win.ShowModalWait();
        yield return Frames(2);
        Check(win.modalWaiting, "Window.ShowModalWait");
        win.CloseModalWait();
        Check(!win.modalWaiting, "Window.CloseModalWait");

        GRoot.inst.ShowModalWait();
        yield return Frames(2);
        Check(GRoot.inst.modalWaiting, "GRoot.ShowModalWait");
        GRoot.inst.CloseModalWait();
        Check(!GRoot.inst.modalWaiting, "GRoot.CloseModalWait");

        win.HideImmediately();
        yield return null;
        Check(!GRoot.inst.hasModalWindow, "modal layer removed after hiding the modal window");

        // show/hide animations (Basics Window2: TweenScale + transition)
        Window2 win2 = Keep(new Window2());
        win2.Show();
        yield return WaitUntil(() => UMath.Abs(win2.scaleX - 1) < 0.001f, 5, ok);
        Check(ok[0], "Window2 show animation finishes");
        win2.Hide();
        yield return WaitUntil(() => !win2.isShowing, 5, ok);
        Check(ok[0], "Window2 hide animation hides the window");

        // popups
        GComponent popup = Keep(UIPackage.CreateObject("Basics", "Component12").asCom);
        GRoot.inst.ShowPopup(popup);
        yield return null;
        Check(GRoot.inst.hasAnyPopup && popup.parent != null, "ShowPopup");
        GRoot.inst.HidePopup();
        yield return Frames(2);
        Check(popup.parent == null, "HidePopup");

        UIConfig.popupMenu = "ui://Basics/PopupMenu";
        PopupMenu pm = new PopupMenu();
        int clicked = 0;
        pm.AddItem("Item 1", () => { clicked++; });
        pm.AddItem("Item 2", () => { clicked++; });
        pm.Show();
        yield return Frames(2);
        Check(pm.contentPane.parent != null, "PopupMenu.Show");
        pm.Hide();
        yield return Frames(2);
        Try("PopupMenu.Dispose", () => pm.Dispose());
    }

    IEnumerator PhaseInput()
    {
        bool[] ok = new bool[1];

        GTextInput input = Keep(new GTextInput());
        input.SetSize(300, 40);
        input.SetXY(100, 100);
        input.singleLine = true;
        input.text = "";
        GRoot.inst.AddChild(input);
        yield return Frames(2);

        // focus by clicking it (Godot input -> StageEngine -> Stage hit test -> SetFocus)
        yield return Click(input);
        if (!Check(input.focused, "clicking an input gives it the focus"))
        {
            input.RequestFocus();
            yield return null;
        }
        Check(input.focused, "input focused");
        Check(GRoot.inst.focus == input, "GRoot.focus is the input");

        int changes = 0;
        input.onChanged.Add(() => { changes++; });

        yield return TypeText("Hi 你好");
        Check(input.text == "Hi 你好", "typed text arrives ('" + input.text + "')");
        Check(changes > 0, "onChanged fired (" + changes + ")");

        PushKey(Key.Backspace, 0, true);
        PushKey(Key.Backspace, 0, false);
        yield return null;
        Check(input.text == "Hi 你", "Backspace deletes the last char ('" + input.text + "')");

        Try("ReplaceSelection", () => input.ReplaceSelection("!"));
        Check(input.text == "Hi 你!", "ReplaceSelection inserts at the caret ('" + input.text + "')");

        Try("Stage.InputString", () => Stage.inst.InputString("?"));
        Check(input.text == "Hi 你!?", "Stage.InputString types into the focused input ('" + input.text + "')");

        int submits = 0;
        input.onSubmit.Add(() => { submits++; });
        PushKey(Key.Enter, 0, true);
        PushKey(Key.Enter, 0, false);
        yield return null;
        Check(submits == 1, "Enter submits a single-line input (" + submits + ")");
        Check(!input.focused, "Enter releases the focus of a single-line input");

        // a click on a plain component goes through the hit test and dispatches onClick
        GComponent btn = Keep(new GComponent());
        btn.SetSize(120, 50);
        btn.SetXY(500, 100);
        btn.opaque = true;
        GGraph bg = new GGraph();
        bg.DrawRect(120, 50, 0, UColor.clear, UColor.white);
        btn.AddChild(bg);
        int clicks = 0;
        btn.onClick.Add(() => { clicks++; });
        GRoot.inst.AddChild(btn);
        yield return Frames(2);
        yield return Click(btn);
        yield return null;
        Check(clicks == 1, "real mouse click dispatches onClick (" + clicks + ")");
    }

    IEnumerator PhaseDemos()
    {
        // every ported example: instantiate its XxxMain node, poke its UI, free it, check GRoot is back to before
        foreach (DemoRegistry.Entry entry in DemoRegistry.Examples)
        {
            string name = entry.name;
            SetContext("Demos/" + name);

            int before = GRoot.inst.numChildren;
            Node node = null;
            if (!Try("create", () => { node = entry.create(); node.Name = name + "Example"; AddChild(node); }))
            {
                if (node != null && GodotObject.IsInstanceValid(node))
                    node.QueueFree();
                continue;
            }
            yield return Frames(10);

            GComponent view = null;
            Try("find view", () =>
            {
                for (int i = before; i < GRoot.inst.numChildren && view == null; i++)
                    view = GRoot.inst.GetChildAt(i) as GComponent;
            });

            yield return PokeDemo(name, view);
            yield return Frames(10);

            SetContext("Demos/" + name + "/close");
            Try("free", () =>
            {
                if (GodotObject.IsInstanceValid(node))
                {
                    if (node.GetParent() == this)
                        RemoveChild(node); // runs the example's _ExitTree now
                    node.QueueFree();
                }
            });
            yield return Frames(3);

            string leftover = "";
            Try("leftovers", () =>
            {
                for (int i = 0; i < GRoot.inst.numChildren; i++)
                {
                    if (i >= before)
                        leftover += " " + DescribeObject(GRoot.inst.GetChildAt(i));
                }
            });
            Check(GRoot.inst.numChildren == before, "closing the example leaves GRoot as before (" + before + " -> "
                + GRoot.inst.numChildren + " children:" + leftover + ")");

            // safety net so one leaking example does not break the next ones
            Try("cleanup", () =>
            {
                if (GRoot.inst.hasAnyPopup)
                    GRoot.inst.HidePopup();
                GRoot.inst.CloseModalWait();
                GRoot.inst.CloseAllWindows();
                for (int guard = 0; guard < 100 && GRoot.inst.numChildren > before; guard++)
                {
                    GObject extra = GRoot.inst.GetChildAt(GRoot.inst.numChildren - 1);
                    extra.Dispose();
                    if (extra.parent == GRoot.inst)
                        GRoot.inst.RemoveChild(extra);
                }
            });
            yield return null;
        }

        yield return PhaseDemoMenu();
    }

    // Drives the parts of an example that need clicks. onClick.Call() runs the example's handlers without the hit
    // test (real clicks are covered by the Input phase and by PhaseDemoMenu).
    IEnumerator PokeDemo(string name, GComponent view)
    {
        if (view == null)
            yield break;

        SetContext("Demos/" + name + "/poke");
        switch (name)
        {
            case "Basics":
                yield return PokeBasics(view);
                break;

            case "Bag":
                CallClick(view, "bagBtn");
                yield return Frames(40); // window show tween + threaded icon loads
                break;

            case "VirtualList":
                CallClick(view, "n6");
                yield return Frames(5);
                CallClick(view, "n8");
                yield return Frames(30);
                CallClick(view, "n7");
                yield return Frames(30);
                break;

            case "Transition":
                for (int i = 0; i < 6; i++)
                {
                    CallClick(view, "btn" + i);
                    yield return Frames(20);
                }
                break;

            case "ModalWaiting":
                CallClick(view, "n0");
                yield return Frames(5);
                Window4 win = null;
                Try("find Window4", () =>
                {
                    for (int i = 0; i < GRoot.inst.numChildren && win == null; i++)
                        win = GRoot.inst.GetChildAt(i) as Window4;
                });
                if (Check(win != null, "Window4 is shown"))
                {
                    CallClick(win.contentPane, "n1");
                    yield return Frames(3);
                    Check(win.modalWaiting, "Window4 shows its modal wait");
                }
                break;

            case "Guide":
                CallClick(view, "n2");
                yield return Frames(70); // TweenMove 0.5 s
                CallClick(view, "bagBtn");
                yield return Frames(3);
                break;

            case "Emoji":
                GTextInput input1 = ChildOf(view, "input1") as GTextInput;
                GTextInput input2 = ChildOf(view, "input2") as GTextInput;
                if (Check(input1 != null && input2 != null, "Emoji inputs exist"))
                {
                    Try("send 1", () => { input1.text = "Hello [:cool] FairyGUI"; });
                    CallClick(view, "btnSend1");
                    yield return Frames(5);
                    Try("send 2", () => { input2.text = "\U0001f600 emoji"; });
                    CallClick(view, "btnSend2");
                    yield return Frames(5);
                    CallClick(view, "btnEmoji1");
                    yield return Frames(5);
                }
                break;

            case "TurnCard":
                CallClick(view, "c0");
                CallClick(view, "c1");
                yield return Frames(110); // 0.8 s turn
                break;

            case "Filter":
                Try("sliders", () =>
                {
                    GSlider s0 = view.GetChild("s0").asSlider;
                    GSlider s4 = view.GetChild("s4").asSlider;
                    s0.value = 150;
                    s4.value = 60;
                    s0.onChanged.Call();
                });
                yield return Frames(5);
                break;

            case "TreeView":
                Try("expand", () =>
                {
                    GTree tree2 = view.GetChild("tree2").asTree;
                    tree2.ExpandAll();
                    tree2.CollapseAll();
                    tree2.ExpandAll();
                });
                yield return Frames(5);
                break;

            case "Cooldown":
            case "TypingEffect":
                yield return Frames(30); // per-frame updates / typing timers
                break;
        }
    }

    IEnumerator PokeBasics(GComponent view)
    {
        List<GObject> buttons = new List<GObject>();
        Try("find demo buttons", () =>
        {
            for (int i = 0; i < view.numChildren; i++)
            {
                GObject obj = view.GetChildAt(i);
                if (obj.group != null && obj.group.name == "btns")
                    buttons.Add(obj);
            }
        });
        Check(buttons.Count > 0, "Basics has demo buttons");

        GComponent container = ChildOf(view, "container") as GComponent;
        foreach (GObject btn in buttons)
        {
            string type = btn.name.Length > 4 ? btn.name.Substring(4) : btn.name;
            SetContext("Demos/Basics/" + type);
            Try("click", () => btn.onClick.Call());
            yield return Frames(15); // page transition

            GComponent demo = container != null && container.numChildren > 0 ? container.GetChildAt(0) as GComponent : null;
            string[] inner = null;
            switch (type)
            {
                case "Button": inner = new string[] { "n34" }; break;
                case "Text": inner = new string[] { "n25" }; break;
                case "Window": inner = new string[] { "n0", "n1" }; break;
                case "Popup": inner = new string[] { "n0", "n1" }; break;
                case "Depth": inner = new string[] { "btn0", "btn1" }; break;
            }
            if (inner != null && demo != null)
            {
                foreach (string childName in inner)
                {
                    CallClick(demo, childName);
                    yield return Frames(20);
                }
                Try("hide popups/windows", () =>
                {
                    if (GRoot.inst.hasAnyPopup)
                        GRoot.inst.HidePopup();
                    GRoot.inst.CloseAllWindows();
                });
            }
            yield return Frames(5);

            CallClick(view, "btn_Back");
            yield return Frames(10);
        }
    }

    // DemoMain end to end: real clicks on the menu and on the Back button.
    IEnumerator PhaseDemoMenu()
    {
        SetContext("Demos/DemoMain");
        bool[] ok = new bool[1];
        int before = GRoot.inst.numChildren;

        DemoMain main = null;
        if (!Try("create DemoMain", () => { main = new DemoMain(); main.Name = "DemoMain"; AddChild(main); }))
            yield break;
        Engine.MaxFps = 120; // DemoMain sets 60
        yield return Frames(3);

        GObject btn = ChildOf(main.menu, "btn_TreeView");
        if (Check(btn != null, "menu has btn_TreeView"))
        {
            yield return Click(btn);
            yield return WaitUntil(() => main.currentExample == "TreeView", 2, ok);
            Check(ok[0], "clicking the menu runs the example (" + main.currentExample + ")");
            Check(main.backButton.visible && !main.menu.visible, "Back shown, menu hidden");
            yield return Frames(10);

            yield return Click(main.backButton);
            yield return WaitUntil(() => main.currentExample == null, 2, ok);
            Check(ok[0], "clicking Back closes the example");
            Check(!main.backButton.visible && main.menu.visible, "menu shown again");
        }

        Try("run/close by code", () => { main.RunExample("Basics"); });
        yield return Frames(10);
        Try("close", () => main.CloseExample());
        yield return Frames(3);

        Try("free DemoMain", () => { RemoveChild(main); main.QueueFree(); });
        yield return Frames(3);
        Check(GRoot.inst.numChildren == before, "DemoMain releases its UI (" + before + " -> " + GRoot.inst.numChildren + ")");
    }
}
