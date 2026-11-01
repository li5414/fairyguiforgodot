// Ported from FairyGUI-unity: Assets/Examples/Curve/CurveMain.cs
using Godot;
using FairyGUI;

// [Godot] In Unity the Curve/Main component was rendered by a UIPainter onto a curved 3D mesh (aomian.FBX).
// Godot's UIPainter renders to a ViewportTexture for 3D materials but does not support input on the 3D surface,
// and this demo has no 3D scene, so the component is shown flat on GRoot. The list logic is unchanged.
public partial class CurveMain : Node
{
    GComponent _mainView;
    GList _list;

    // [Godot] Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        // [Godot] Application.targetFrameRate / Stage.onKeyDown(Escape) are handled by DemoMain.

        // [Godot] this.GetComponent<UIPainter>().ui -> a flat full-screen view (see the class comment).
        _mainView = DemoUtil.CreateMainView("Curve");

        _list = _mainView.GetChild("list").asList;
        _list.SetVirtualAndLoop();
        _list.itemRenderer = RenderListItem;
        _list.numItems = 5;
        _list.scrollPane.onScroll.Add(DoSpecialEffect);

        DoSpecialEffect();
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

    void DoSpecialEffect()
    {
        //change the scale according to the distance to middle
        float midX = _list.scrollPane.posX + _list.viewWidth / 2;
        int cnt = _list.numChildren;
        for (int i = 0; i < cnt; i++)
        {
            GObject obj = _list.GetChildAt(i);
            float dist = UMath.Abs(midX - obj.x - obj.width / 2);
            if (dist > obj.width) //no intersection
                obj.SetScale(1, 1);
            else
            {
                float ss = 1 + (1 - dist / obj.width) * 0.24f;
                obj.SetScale(ss, ss);
            }
        }
    }

    void RenderListItem(int index, GObject obj)
    {
        GButton item = (GButton)obj;
        item.SetPivot(0.5f, 0.5f);
        item.icon = UIPackage.GetItemURL("Curve", "n" + (index + 1));
    }
}
