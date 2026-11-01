// Ported from FairyGUI-unity: Assets/Examples/Bag/BagMain.cs
using Godot;
using FairyGUI;

/// <summary>
/// A game bag demo, demonstrated how to customize loader to load icons not in the UI package.
/// </summary>
public partial class BagMain : Node
{
    GComponent _mainView;
    BagWindow _bagWindow;

    // [Godot] Awake + Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        //Register custom loader class
        UIObjectFactory.SetLoaderExtension(typeof(MyGLoader));

        // [Godot] Application.targetFrameRate / Stage.onKeyDown(Escape) are handled by DemoMain.
        GRoot.inst.SetContentScaleFactor(1136, 640);
        _mainView = DemoUtil.CreateMainView("Bag");

        _bagWindow = new BagWindow();
        _mainView.GetChild("bagBtn").onClick.Add(() => { _bagWindow.Show(); });
    }

    // [Godot] OnDestroy: restore the global loader extension so other examples get plain GLoaders again.
    public override void _ExitTree()
    {
        if (DemoUtil.IsQuitting)
            return;

        if (_bagWindow != null)
        {
            _bagWindow.HideImmediately(); // a modal window: removes the modal layer too
            _bagWindow.Dispose();
            _bagWindow = null;
        }
        if (_mainView != null)
        {
            _mainView.Dispose();
            _mainView = null;
        }

        UIObjectFactory.SetLoaderExtension((UIObjectFactory.GLoaderCreator)null);
        GRoot.inst.SetContentScaleFactor(DemoUtil.DesignWidth, DemoUtil.DesignHeight, UIContentScaler.ScreenMatchMode.MatchWidthOrHeight);
    }
}
