// Ported from FairyGUI-unity: Assets/Examples/Transition/TransitionMain.cs
using Godot;
using FairyGUI;

public partial class TransitionMain : Node
{
    GComponent _mainView;
    GGroup _btnGroup;
    GComponent _g1;
    GComponent _g2;
    GComponent _g3;
    GComponent _g4;
    GComponent _g5;
    GComponent _g6;

    float _startValue;
    float _endValue;

    // [Godot] Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        // [Godot] Application.targetFrameRate / Stage.onKeyDown(Escape) are handled by DemoMain.
        _mainView = DemoUtil.CreateMainView("Transition");

        _btnGroup = _mainView.GetChild("g0").asGroup;

        _g1 = UIPackage.CreateObject("Transition", "BOSS").asCom;
        _g2 = UIPackage.CreateObject("Transition", "BOSS_SKILL").asCom;
        _g3 = UIPackage.CreateObject("Transition", "TRAP").asCom;

        _g4 = UIPackage.CreateObject("Transition", "GoodHit").asCom;
        _g5 = UIPackage.CreateObject("Transition", "PowerUp").asCom;
        _g5.GetTransition("t0").SetHook("play_num_now", __playNum);

        _g6 = UIPackage.CreateObject("Transition", "PathDemo").asCom;

        _mainView.GetChild("btn0").onClick.Add(() => { __play(_g1); });
        _mainView.GetChild("btn1").onClick.Add(() => { __play(_g2); });
        _mainView.GetChild("btn2").onClick.Add(() => { __play(_g3); });
        _mainView.GetChild("btn3").onClick.Add(__play4);
        _mainView.GetChild("btn4").onClick.Add(__play5);
        _mainView.GetChild("btn5").onClick.Add(() => { __play(_g6); });
    }

    // [Godot] OnDestroy. The effect components are not children of the main view, so dispose them explicitly
    // (Dispose also stops their transitions and removes them from GRoot if they are playing).
    public override void _ExitTree()
    {
        if (DemoUtil.IsQuitting)
            return;

        if (_g5 != null)
            GTween.Kill(_g5);

        GComponent[] all = new GComponent[] { _g1, _g2, _g3, _g4, _g5, _g6, _mainView };
        foreach (GComponent com in all)
        {
            if (com != null)
                com.Dispose();
        }
        _g1 = _g2 = _g3 = _g4 = _g5 = _g6 = null;
        _mainView = null;
    }

    void __play(GComponent target)
    {
        _btnGroup.visible = false;
        GRoot.inst.AddChild(target);
        Transition t = target.GetTransition("t0");

        t.Play(() =>
        {
            _btnGroup.visible = true;
            GRoot.inst.RemoveChild(target);
        });
    }

    void __play4()
    {
        _btnGroup.visible = false;
        _g4.x = GRoot.inst.width - _g4.width - 20;
        _g4.y = 100;
        GRoot.inst.AddChild(_g4);
        Transition t = _g4.GetTransition("t0");
        t.Play(3, 0, () =>
        {
            _btnGroup.visible = true;
            GRoot.inst.RemoveChild(_g4);
        });
    }

    void __play5()
    {
        _btnGroup.visible = false;
        _g5.x = 20;
        _g5.y = GRoot.inst.height - _g5.height - 100;
        GRoot.inst.AddChild(_g5);
        Transition t = _g5.GetTransition("t0");
        _startValue = 10000;
        int add = DemoUtil.Range(1000, 3000);
        _endValue = _startValue + add;
        _g5.GetChild("value").text = "" + _startValue;
        _g5.GetChild("add_value").text = "" + add;
        t.Play(() =>
        {
            _btnGroup.visible = true;
            GRoot.inst.RemoveChild(_g5);
        });
    }

    void __playNum()
    {
        // [Godot] SetTarget(_g5) added so the tween is killed with the component when the example is closed.
        GTween.To(_startValue, _endValue, 0.3f).SetEase(EaseType.Linear).SetTarget(_g5)
            .OnUpdate((GTweener tweener) => { _g5.GetChild("value").text = "" + UMath.Floor(tweener.value.x); });

    }
}
