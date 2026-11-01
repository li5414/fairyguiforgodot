// Ported from FairyGUI-unity: Assets/Examples/Cooldown/CooldownMain.cs
using Godot;
using FairyGUI;

public partial class CooldownMain : Node
{
    GComponent _mainView;

    GButton _btn0;
    GImage _mask0;
    float _time1;

    GButton _btn1;
    GImage _mask1;
    float _time2;

    // [Godot] Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        // [Godot] Application.targetFrameRate / Stage.onKeyDown(Escape) are handled by DemoMain.
        _mainView = DemoUtil.CreateMainView("Cooldown");

        _btn0 = _mainView.GetChild("b0").asButton;
        // [Godot] "Cooldown/k0" (a Resources path) -> the imported png path; GLoader loads external icons with ResourceLoader.
        _btn0.icon = DemoUtil.IconPath + "k0.png";
        _time1 = 5;
        _mask0 = _btn0.GetChild("mask").asImage;

        _btn1 = _mainView.GetChild("b1").asButton;
        _btn1.icon = DemoUtil.IconPath + "k1.png";
        _time2 = 10;
        _mask1 = _btn1.GetChild("mask").asImage;

    }

    // [Godot] OnDestroy
    public override void _ExitTree()
    {
        if (DemoUtil.IsQuitting)
            return;

        if (_mainView != null)
        {
            _mainView.Dispose();
            _mainView = null;
        }
    }

    // [Godot] Update -> _Process; Time.deltaTime -> the delta of this frame
    public override void _Process(double delta)
    {
        if (_mainView == null)
            return;

        float deltaTime = (float)delta;

        _time1 -= deltaTime;
        if (_time1 < 0)
            _time1 = 5;
        _mask0.fillAmount = 1 - (5 - _time1) / 5f;

        _time2 -= deltaTime;
        if (_time2 < 0)
            _time2 = 10;
        _btn1.text = string.Empty + UMath.RoundToInt(_time2);
        _mask1.fillAmount = 1 - (10 - _time2) / 10f;
    }
}
