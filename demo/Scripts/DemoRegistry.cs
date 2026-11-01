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

        public Entry(string name, Func<Node> create)
        {
            this.name = name;
            this.create = create;
        }
    }

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
        new Entry("HeadBar", () => new HeadBarMain()),
        new Entry("CutScene", () => new CutSceneMain()),
        new Entry("EmitNumbers", () => new EmitNumbersMain()),
        new Entry("Extension", () => new ExtensionMain()),
        new Entry("Filter", () => new FilterMain()),
        new Entry("Curve", () => new CurveMain()),
        new Entry("TurnCard", () => new TurnCardMain()),
    };
}
