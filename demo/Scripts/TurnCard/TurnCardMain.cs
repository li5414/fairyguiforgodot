// Ported from FairyGUI-unity: Assets/Examples/TurnCard/TurnCardMain.cs
using Godot;
using FairyGUI;

public partial class TurnCardMain : Node
{
    GComponent _mainView;
    Card _c0;
    Card _c1;

    // [Godot] Awake + Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        // [Godot] Application.targetFrameRate / Stage.onKeyDown(Escape) are handled by DemoMain.
        DemoUtil.AddPackage("TurnCard");
        UIObjectFactory.SetPackageItemExtension("ui://TurnCard/CardComponent", typeof(Card));

        _mainView = DemoUtil.CreateMainView("TurnCard");

        _c0 = (Card)_mainView.GetChild("c0");

        _c1 = (Card)_mainView.GetChild("c1");
        _c1.SetPerspective();

        _c0.onClick.Add(__clickCard);
        _c1.onClick.Add(__clickCard);
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

    void __clickCard(EventContext context)
    {
        Card card = (Card)context.sender;
        card.Turn();
    }
}
