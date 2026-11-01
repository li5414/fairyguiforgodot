// Ported from FairyGUI-unity: Assets/Examples/HeadBar/HeadBarMain.cs
using Godot;
using FairyGUI;

public partial class HeadBarMain : Node
{
    GComponent _mainView;

    // [Godot] Start
    public override void _Ready()
    {
        DemoUtil.EnsureInit();

        // [Godot] Application.targetFrameRate / Stage.onKeyDown(Escape) are handled by DemoMain.

        // [Godot] The Unity scene had two 3D npc models, each with a world-space UIPanel child named "HeadBar".
        // Here the npcs are 2D placeholders (DemoUtil.CreateNpc) and the head bars are UIPanel nodes: they are
        // drawn in the 2D canvas and follow their npc node, like the world-space panels followed the models.
        AddChild(DemoUtil.CreateNpc("npc1", new Vector2(380, 540), new Color(0.75f, 0.45f, 0.3f, 1), true));
        AddChild(DemoUtil.CreateNpc("npc2", new Vector2(760, 540), new Color(0.35f, 0.5f, 0.75f, 1), true));

        Node2D npc = GetNode<Node2D>("npc1");
        UIPanel panel = npc.GetNode<UIPanel>("HeadBar");
        panel.ui.GetChild("name").text = "Long [color=#FFFFFF]Long[/color][img]ui://HeadBar/cool[/img] Name";
        panel.ui.GetChild("blood").asProgress.value = 75;
        panel.ui.GetChild("sign").asLoader.url = "ui://HeadBar/task";

        npc = GetNode<Node2D>("npc2");
        panel = npc.GetNode<UIPanel>("HeadBar");
        panel.ui.GetChild("name").text = "Man2";
        panel.ui.GetChild("blood").asProgress.value = 25;
        panel.ui.GetChild("sign").asLoader.url = "ui://HeadBar/fighting";

        // [Godot] No _ExitTree cleanup needed: the npc nodes are children of this node and each UIPanel disposes
        // its UI in its own _ExitTree.
    }
}
