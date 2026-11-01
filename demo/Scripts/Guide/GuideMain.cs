// Ported from FairyGUI-unity: Assets/Examples/Guide/GuideMain.cs
using Godot;
using FairyGUI;

public partial class GuideMain : Node
{
    GComponent _mainView;
    GComponent _guideLayer;

    // [Godot] Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        // [Godot] Application.targetFrameRate / Stage.onKeyDown(Escape) are handled by DemoMain.
        _mainView = DemoUtil.CreateMainView("Guide");

        _guideLayer = UIPackage.CreateObject("Guide", "GuideLayer").asCom;
        _guideLayer.SetSize(GRoot.inst.width, GRoot.inst.height);
        _guideLayer.AddRelation(GRoot.inst, RelationType.Size);

        GObject bagBtn = _mainView.GetChild("bagBtn");
        bagBtn.onClick.Add(() =>
        {
            _guideLayer.RemoveFromParent();
        });

        _mainView.GetChild("n2").onClick.Add(() =>
        {
            GRoot.inst.AddChild(_guideLayer); //!!Before using TransformRect(or GlobalToLocal), the object must be added first
            Rect rect = bagBtn.TransformRect(new Rect(0, 0, bagBtn.width, bagBtn.height), _guideLayer);

            GObject window = _guideLayer.GetChild("window");
            window.size = new Vector2((int)rect.size.X, (int)rect.size.Y);
            window.TweenMove(new Vector2((int)rect.position.X, (int)rect.position.Y), 0.5f);
        });
    }

    // [Godot] OnDestroy. The guide layer is a separate GRoot child (or not on stage at all).
    public override void _ExitTree()
    {
        if (DemoUtil.IsQuitting)
            return;

        if (_guideLayer != null)
        {
            _guideLayer.Dispose();
            _guideLayer = null;
        }
        if (_mainView != null)
        {
            _mainView.Dispose();
            _mainView = null;
        }
    }
}
