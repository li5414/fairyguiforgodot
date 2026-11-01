// Ported from FairyGUI-unity: Assets/Examples/VirtualList/VirtualListMain.cs
using Godot;
using FairyGUI;

public partial class VirtualListMain : Node
{
    GComponent _mainView;
    GList _list;

    // [Godot] Awake + Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        DemoUtil.AddPackage("VirtualList");
        // MailItem is the class of the Extension example (the Unity project shares it as well).
        UIObjectFactory.SetPackageItemExtension("ui://VirtualList/mailItem", typeof(MailItem));

        // [Godot] Application.targetFrameRate / Stage.onKeyDown(Escape) are handled by DemoMain.
        _mainView = DemoUtil.CreateMainView("VirtualList");
        _mainView.GetChild("n6").onClick.Add(() => { _list.AddSelection(500, true); });
        _mainView.GetChild("n7").onClick.Add(() => { _list.scrollPane.ScrollTop(); });
        _mainView.GetChild("n8").onClick.Add(() => { _list.scrollPane.ScrollBottom(); });

        _list = _mainView.GetChild("mailList").asList;
        _list.SetVirtual();

        _list.itemRenderer = RenderListItem;
        _list.numItems = 1000;
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

    void RenderListItem(int index, GObject obj)
    {
        MailItem item = (MailItem)obj;
        item.setFetched(index % 3 == 0);
        item.setRead(index % 2 == 0);
        item.setTime("5 Nov 2015 16:24:33");
        item.title = index + " Mail title here";
    }
}
