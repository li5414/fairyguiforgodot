// Ported from FairyGUI-unity: Assets/Examples/ModalWaiting/ModalWaitingMain.cs
using System.Collections;
using Godot;
using FairyGUI;

public partial class ModalWaitingMain : Node
{
    GComponent _mainView;
    Window4 _testWin;
    bool _exited;
    DemoUtil.UIConfigSnapshot _config;

    // [Godot] Awake + Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();
        _config = new DemoUtil.UIConfigSnapshot();
        _exited = false;

        DemoUtil.AddPackage("ModalWaiting");
        UIConfig.globalModalWaiting = "ui://ModalWaiting/GlobalModalWaiting";
        UIConfig.windowModalWaiting = "ui://ModalWaiting/WindowModalWaiting";

        // [Godot] Application.targetFrameRate / Stage.onKeyDown(Escape) are handled by DemoMain.
        _mainView = DemoUtil.CreateMainView("ModalWaiting");

        _testWin = new Window4();

        _mainView.GetChild("n0").onClick.Add(() => { _testWin.Show(); });

        // [Godot] MonoBehaviour.StartCoroutine -> Timers.inst.StartCoroutine
        Timers.inst.StartCoroutine(WaitSomeTime());
    }

    // [Godot] OnDestroy. The global modal wait must not outlive the example (it would block the demo menu).
    public override void _ExitTree()
    {
        _exited = true;
        if (DemoUtil.IsQuitting)
            return;

        GRoot.inst.CloseModalWait();

        if (_testWin != null)
        {
            _testWin.HideImmediately();
            _testWin.Dispose();
            _testWin = null;
        }
        if (_mainView != null)
        {
            _mainView.Dispose();
            _mainView = null;
        }

        _config?.Restore();
    }

    IEnumerator WaitSomeTime()
    {
        GRoot.inst.ShowModalWait();

        yield return new WaitForSeconds(3);

        if (!_exited)
            GRoot.inst.CloseModalWait();
    }
}
