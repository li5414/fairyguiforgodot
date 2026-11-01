// Ported from FairyGUI-unity: Assets/Examples/TypingEffect/TypingEffectMain.cs
using Godot;
using FairyGUI;

public partial class TypingEffectMain : Node
{
    GComponent _mainView;
    TypingEffect _te1;
    TypingEffect _te2;

    // [Godot] Awake + Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        // [Godot] Application.targetFrameRate / Stage.onKeyDown(Escape) are handled by DemoMain.
        _mainView = DemoUtil.CreateMainView("TypingEffect");

        _te1 = new TypingEffect(_mainView.GetChild("n2").asTextField);
        _te1.Start();
        Timers.inst.StartCoroutine(_te1.Print(0.050f));

        _te2 = new TypingEffect(_mainView.GetChild("n3").asTextField);
        _te2.Start();
        Timers.inst.Add(0.050f, 0, PrintText);
    }

    // [Godot] OnDestroy. Cancel() makes the running Print coroutine end (Print() returns false once cancelled).
    public override void _ExitTree()
    {
        if (DemoUtil.IsQuitting)
            return;

        Timers.inst.Remove(PrintText);
        _te1?.Cancel();
        _te2?.Cancel();
        _te1 = _te2 = null;

        if (_mainView != null)
        {
            _mainView.Dispose();
            _mainView = null;
        }
    }

    void PrintText(object param)
    {
        if (_te2 == null || !_te2.Print())
        {
            Timers.inst.Remove(PrintText);
        }
    }
}
