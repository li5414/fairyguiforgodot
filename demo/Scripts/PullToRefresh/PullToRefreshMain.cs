// Ported from FairyGUI-unity: Assets/Examples/PullToRefresh/PullToRefreshMain.cs
using Godot;
using FairyGUI;

public partial class PullToRefreshMain : Node
{
    GComponent _mainView;
    GList _list1;
    GList _list2;

    // [Godot] Awake + Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        // [Godot] Application.targetFrameRate / Stage.onKeyDown(Escape) are handled by DemoMain.
        UIObjectFactory.SetPackageItemExtension("ui://PullToRefresh/Header", typeof(ScrollPaneHeader));

        _mainView = DemoUtil.CreateMainView("PullToRefresh");

        _list1 = _mainView.GetChild("list1").asList;
        _list1.itemRenderer = RenderListItem1;
        _list1.SetVirtual();
        _list1.numItems = 1;
        _list1.scrollPane.onPullDownRelease.Add(OnPullDownToRefresh);

        _list2 = _mainView.GetChild("list2").asList;
        _list2.itemRenderer = RenderListItem2;
        _list2.SetVirtual();
        _list2.numItems = 1;
        _list2.scrollPane.onPullUpRelease.Add(OnPullUpToRefresh);
    }

    // [Godot] OnDestroy. The pending Timers callbacks check isDisposed (a Unity scene unload never let them run).
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

    void RenderListItem1(int index, GObject obj)
    {
        GButton item = obj.asButton;
        item.title = "Item " + (_list1.numItems - index - 1);
    }

    void RenderListItem2(int index, GObject obj)
    {
        GButton item = obj.asButton;
        item.title = "Item " + index;
    }

    void OnPullDownToRefresh()
    {
        ScrollPaneHeader header = (ScrollPaneHeader)_list1.scrollPane.header;
        if (header.ReadyToRefresh)
        {
            header.SetRefreshStatus(2);
            _list1.scrollPane.LockHeader(header.sourceHeight);

            //Simulate a async resquest
            Timers.inst.Add(2, 1, (object param) =>
            {
                if (_list1.isDisposed)
                    return;

                _list1.numItems += 5;

                //Refresh completed
                header.SetRefreshStatus(3);
                _list1.scrollPane.LockHeader(35);

                Timers.inst.Add(2, 1, (object param2) =>
                {
                    if (_list1.isDisposed)
                        return;

                    header.SetRefreshStatus(0);
                    _list1.scrollPane.LockHeader(0);
                });
            });
        }
    }

    void OnPullUpToRefresh()
    {
        GComponent footer = (GComponent)_list2.scrollPane.footer;

        footer.GetController("c1").selectedIndex = 1;
        _list2.scrollPane.LockFooter(footer.sourceHeight);

        //Simulate a async resquest
        Timers.inst.Add(2, 1, (object param) =>
        {
            if (_list2.isDisposed)
                return;

            _list2.numItems += 5;

            //Refresh completed
            footer.GetController("c1").selectedIndex = 0;
            _list2.scrollPane.LockFooter(0);
        });
    }
}
