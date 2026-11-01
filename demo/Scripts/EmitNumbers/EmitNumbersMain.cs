// Ported from FairyGUI-unity: Assets/Examples/EmitNumbers/EmitNumbersMain.cs
using System.Collections;
using Godot;
using FairyGUI;

public partial class EmitNumbersMain : Node
{
    Node2D _npc1; // [Godot] Transform -> Node2D
    Node2D _npc2;
    bool _finished;

    // [Godot] Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        // [Godot] Application.targetFrameRate / Stage.onKeyDown(Escape) are handled by DemoMain.

        // [Godot] The scene's UIPanel did not load EmitNumbers (fonts/critical sign are used through ui:// URLs).
        DemoUtil.AddPackage("EmitNumbers");

        // [Godot] GameObject.Find("npc1"/"npc2") -> 2D npc placeholders created here (with their HeadBar panels,
        // as in the Unity scene).
        AddChild(DemoUtil.CreateNpc("npc1", new Vector2(380, 560), new Color(0.75f, 0.45f, 0.3f, 1), true));
        AddChild(DemoUtil.CreateNpc("npc2", new Vector2(760, 560), new Color(0.35f, 0.5f, 0.75f, 1), true));
        _npc1 = GetNode<Node2D>("npc1");
        _npc2 = GetNode<Node2D>("npc2");

        _finished = false;
        // [Godot] MonoBehaviour.StartCoroutine -> Timers.inst.StartCoroutine
        Timers.inst.StartCoroutine(RunTest());
    }

    // [Godot] OnDisable -> _ExitTree. Also releases the EmitManager view (Unity unloaded it with the scene).
    public override void _ExitTree()
    {
        _finished = true;
        if (DemoUtil.IsQuitting)
            return;

        EmitManager.Release();
    }

    IEnumerator RunTest()
    {
        while (!_finished)
        {
            EmitManager.inst.Emit(_npc1, 0, DemoUtil.Range(100, 100000), DemoUtil.Range(0, 10) == 5);
            EmitManager.inst.Emit(_npc2, 1, DemoUtil.Range(100, 100000), DemoUtil.Range(0, 10) == 5);
            yield return new WaitForSeconds(0.3f);
        }
    }
}
