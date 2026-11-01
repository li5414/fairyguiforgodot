// Ported from FairyGUI-unity: Assets/Examples/Extension/ExtensionMain.cs
using Godot;
using FairyGUI;

public partial class ExtensionMain : Node
{
    GComponent _mainView;
    GList _list;

    // [Godot] Awake + Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        DemoUtil.AddPackage("Extension");
        UIObjectFactory.SetPackageItemExtension("ui://Extension/mailItem", typeof(MailItem));

        // [Godot] Application.targetFrameRate / Stage.onKeyDown(Escape) are handled by DemoMain.
        _mainView = DemoUtil.CreateMainView("Extension");

        _list = _mainView.GetChild("mailList").asList;
        for (int i = 0; i < 10; i++)
        {
            MailItem item = (MailItem)_list.AddItemFromPool();
            item.setFetched(i % 3 == 0);
            item.setRead(i % 2 == 0);
            item.setTime("5 Nov 2015 16:24:33");
            item.title = "Mail title here";
        }

        _list.EnsureBoundsCorrect();
        float delay = 0f;
        for (int i = 0; i < 10; i++)
        {
            MailItem item = (MailItem)_list.GetChildAt(i);
            if (_list.IsChildInView(item))
            {
                item.PlayEffect(delay);
                delay += 0.2f;
            }
            else
                break;
        }
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
}
