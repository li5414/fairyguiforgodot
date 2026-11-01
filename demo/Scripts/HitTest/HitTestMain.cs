// Ported from FairyGUI-unity: Assets/Examples/HitTest/HitTestMain.cs
using Godot;
using FairyGUI;

public partial class HitTestMain : Node
{
    // [Godot] Transform cube -> a 2D square standing in for the 3D cube of the Unity scene.
    Polygon2D cube;
    GComponent _mainView;

    const float CubeSize = 160;

    // [Godot] Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        // [Godot] Application.targetFrameRate is handled by DemoMain.

        // [Godot] The scene object found with GameObject.Find("Cube") is created here; it is drawn under the
        // FairyGUI stage layer, like the 3D cube was rendered under the UI camera.
        cube = new Polygon2D();
        cube.Name = "Cube";
        cube.Color = new Color(0.35f, 0.55f, 0.95f, 1);
        cube.Polygon = new Vector2[] {
            new Vector2(-CubeSize / 2, -CubeSize / 2), new Vector2(CubeSize / 2, -CubeSize / 2),
            new Vector2(CubeSize / 2, CubeSize / 2), new Vector2(-CubeSize / 2, CubeSize / 2) };
        cube.Position = new Vector2(DemoUtil.DesignWidth / 2, DemoUtil.DesignHeight / 2);
        cube.Rotation = UMath.PI / 8;
        AddChild(cube);

        // [Godot] The UIPanel of the scene (HitTest/Main) was not referenced by the Unity script but was part of it.
        _mainView = DemoUtil.CreateMainView("HitTest");

        Stage.inst.onTouchBegin.Add(OnTouchBegin);
    }

    // [Godot] OnDestroy
    public override void _ExitTree()
    {
        if (DemoUtil.IsQuitting)
            return;

        Stage.inst.onTouchBegin.Remove(OnTouchBegin);
        if (_mainView != null)
        {
            _mainView.Dispose();
            _mainView = null;
        }
    }

    void OnTouchBegin()
    {
        if (!Stage.isTouchOnUI)
        {
            // [Godot] Camera.ScreenPointToRay + Physics.Raycast -> a 2D point test against the square. Stage touch
            // positions are viewport coordinates (y down), the same space as the canvas of this node.
            Vector2 pt = cube.GetGlobalTransformWithCanvas().AffineInverse() * Stage.inst.touchPosition;
            if (UMath.Abs(pt.X) <= CubeSize / 2 && UMath.Abs(pt.Y) <= CubeSize / 2)
            {
                GD.Print("Hit the cube");
            }
        }
    }

    // [Godot] OnKeyDown (never registered in the Unity script) removed: Escape is handled by DemoMain.
}
