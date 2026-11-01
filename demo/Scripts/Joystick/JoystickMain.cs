// Ported from FairyGUI-unity: Assets/Examples/Joystick/JoystickMain.cs
using Godot;
using FairyGUI;

public partial class JoystickMain : Node
{
    GComponent _mainView;
    GTextField _text;
    JoystickModule _joystick;

    // [Godot] Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        // [Godot] Application.targetFrameRate / Stage.onKeyDown(Escape) are handled by DemoMain.
        _mainView = DemoUtil.CreateMainView("Joystick");

        _text = _mainView.GetChild("n9").asTextField;

        _joystick = new JoystickModule(_mainView);
        _joystick.onMove.Add(__joystickMove);
        _joystick.onEnd.Add(__joystickEnd);
    }

    // [Godot] OnDestroy. Disposing the view removes the touch listeners and kills the button tween.
    public override void _ExitTree()
    {
        if (DemoUtil.IsQuitting)
            return;

        if (_joystick != null)
        {
            _joystick.RemoveEventListeners();
            _joystick = null;
        }
        if (_mainView != null)
        {
            _mainView.Dispose();
            _mainView = null;
        }
    }

    void __joystickMove(EventContext context)
    {
        float degree = (float)context.data;
        _text.text = "" + degree;
    }

    void __joystickEnd()
    {
        _text.text = "";
    }
}
