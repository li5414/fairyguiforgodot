// Ported from FairyGUI-unity: Assets/Examples/CutScene/CutSceneMain.cs
using System.Collections;
using Godot;
using FairyGUI;

/// <summary>
/// Demonstrated the simple flow of a game.
/// </summary>
public partial class CutSceneMain : Node
{
    // [Godot] Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        // [Godot] Application.targetFrameRate / Stage.onKeyDown(Escape) are handled by DemoMain.
        DemoUtil.AddPackage("CutScene");

        LevelManager.inst.Init();
        LevelManager.inst.LoadLevel("scene1");
    }

    // [Godot] OnDestroy
    public override void _ExitTree()
    {
        if (DemoUtil.IsQuitting)
            return;

        LevelManager.inst.Release();
    }
}
