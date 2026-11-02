// FairyGUI for Godot - demo helpers (not a port of a Unity file).
// The list of ported examples, shared by DemoMain (menu) and tests/SmokeTest (runs each one briefly).
using System;
using Godot;

public static class DemoRegistry
{
    public struct Entry
    {
        public string name;
        public Func<Node> create;
        /// <summary>
        /// Background (clear color) of the Unity example scene's Main Camera.
        /// </summary>
        public Color background;

        public Entry(string name, Func<Node> create) : this(name, create, DemoUtil.DefaultBackground)
        {
        }

        public Entry(string name, Func<Node> create, Color background)
        {
            this.name = name;
            this.create = create;
            this.background = background;
        }
    }

    // HeadBar, EmitNumbers and Curve used a skybox camera in Unity; a flat color stands in for it.
    static readonly Color SkyBlue = new Color(0.19215687f, 0.3019608f, 0.4745098f);
    static readonly Color CurveGray = new Color(0.6102941f, 0.6102941f, 0.6102941f);

    public static readonly Entry[] Examples = new Entry[]
    {
        new Entry("Basics", () => new BasicsMain()),
        new Entry("Bag", () => new BagMain()),
        new Entry("VirtualList", () => new VirtualListMain()),
        new Entry("LoopList", () => new LoopListMain()),
        new Entry("ScrollPane", () => new ScrollPaneMain()),
        new Entry("TreeView", () => new TreeViewMain()),
        new Entry("Transition", () => new TransitionMain()),
        new Entry("PullToRefresh", () => new PullToRefreshMain()),
        new Entry("ModalWaiting", () => new ModalWaitingMain()),
        new Entry("Joystick", () => new JoystickMain()),
        new Entry("Cooldown", () => new CooldownMain()),
        new Entry("HitTest", () => new HitTestMain()),
        new Entry("Emoji", () => new EmojiMain()),
        new Entry("TypingEffect", () => new TypingEffectMain()),
        new Entry("Guide", () => new GuideMain()),
        new Entry("HeadBar", () => new HeadBarMain(), SkyBlue),
        new Entry("CutScene", () => new CutSceneMain()),
        new Entry("EmitNumbers", () => new EmitNumbersMain(), SkyBlue),
        new Entry("Extension", () => new ExtensionMain()),
        new Entry("Filter", () => new FilterMain()),
        new Entry("Curve", () => new CurveMain(), CurveGray),
        new Entry("TurnCard", () => new TurnCardMain()),
    };
}
