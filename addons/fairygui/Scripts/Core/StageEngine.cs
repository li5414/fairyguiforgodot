// Ported from FairyGUI-unity: Assets/Scripts/Core/StageEngine.cs
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    // [Godot] Was a MonoBehaviour on the Stage GameObject. Now the single driver Node of FairyGUI:
    // register it as autoload "FairyGUI" (res://addons/fairygui/Scripts/Core/StageEngine.cs); without the
    // autoload Stage.Instantiate() creates one and adds it (deferred) to SceneTree.Root.
    // It owns the CanvasLayer (layer = Stage.canvasLayer) that hosts the Stage node, drives the frame loop
    // (Unity LateUpdate) and forwards input (Unity OnGUI) and window notifications to the Stage.
    public partial class StageEngine : Node
    {
        // [Godot] Exported only so the values are visible in the Remote scene tree inspector while running.
        [Export]
        public int ObjectsOnStage;
        [Export]
        public int GraphicsOnStage;

        public static bool beingQuit;

        // [Godot] New: the engine instance (autoload or auto-created).
        public static StageEngine inst { get; private set; }

        CanvasLayer _layer;

        public StageEngine()
        {
            // [Godot] Claim the singleton as early as possible: the autoload is constructed before any user
            // script runs, so Stage.Instantiate() called from another autoload's _Ready reuses this instance.
            if (inst == null || !IsInstanceValid(inst))
                inst = this;
        }

        /// <summary>
        /// The CanvasLayer that hosts the Stage node.
        /// </summary>
        // [Godot] New.
        public CanvasLayer stageLayer
        {
            get
            {
                EnsureLayer();
                return _layer;
            }
        }

        public override void _EnterTree()
        {
            if (inst != this && inst != null && IsInstanceValid(inst))
            {
                // [Godot] A second engine (e.g. the autoload plus a manually added one). Only one may drive FairyGUI.
                GD.PushWarning("FairyGUI: more than one StageEngine in the scene tree, the extra one is disabled.");
                ProcessMode = ProcessModeEnum.Disabled;
                return;
            }
            inst = this;

            // [Godot] UI keeps working while the tree is paused, and _Process runs after the user scripts
            // (lower priority values run first), which corresponds to Unity's LateUpdate.
            ProcessMode = ProcessModeEnum.Always;
            ProcessPriority = 1000;

            EnsureLayer();
        }

        // [Godot] Start(): `useGUILayout = false` has no equivalent and is removed.

        public override void _Process(double delta)
        {
            if (inst != this || beingQuit)
                return;

            // [Godot] Fixed per-frame order (ARCHITECTURE §5). Timers/TweenManager had their own hidden
            // GameObjects in Unity; here the engine drives them.
            FrameTime.Advance(delta);
            Timers.inst.Update();
            TweenManager.Update();
            Stage.inst.InternalUpdate();

            ObjectsOnStage = Stats.ObjectCount;
            GraphicsOnStage = Stats.GraphicsCount;
        }

        // [Godot] Replaces OnGUI + the per-frame Input polling of Stage: every Godot input event goes to the Stage.
        public override void _Input(Godot.InputEvent @event)
        {
            if (inst != this || beingQuit)
                return;

            if (Stage.inst.HandleInputEvent(@event))
                GetViewport().SetInputAsHandled();
        }

        public override void _Notification(int what)
        {
            if (what == NotificationWMCloseRequest)
            {
                // [Godot] OnApplicationQuit. Unity only did this in the editor; Godot does it on every platform.
                // When auto_accept_quit is off the application may refuse to quit, so only predelete handles it.
                SceneTree tree = GetTree();
                if (inst == this && (tree == null || tree.AutoAcceptQuit))
                    OnQuit();
            }
            else if (what == NotificationPredelete)
            {
                // [Godot] Also covers SceneTree.Quit() and freeing the engine.
                if (inst == this)
                {
                    OnQuit();
                    inst = null;
                }
            }
            else if (what == NotificationWMMouseExit)
            {
                if (inst == this && !beingQuit && Stage.instantiated)
                    Stage.inst.HandleMouseExit();
            }
            // [Godot] NotificationOsImeUpdate is not forwarded: like Unity's Input.compositionString, the focused
            // InputTextField polls DisplayServer.ImeGetText() every frame while editing.
        }

        void OnQuit()
        {
            if (beingQuit)
                return;

            beingQuit = true;
            UIPackage.RemoveAllPackages();
            if (Stage.instantiated)
                Stage.inst.Dispose();
        }

        /// <summary>
        /// Put the Stage node under this engine's CanvasLayer.
        /// </summary>
        // [Godot] New. Works whether or not the engine is already inside the tree.
        internal void AttachStage(Stage stage)
        {
            EnsureLayer();

            Node2D stageNode = stage.node;
            Node oldParent = stageNode.GetParent();
            if (oldParent == _layer)
                return;

            if (oldParent != null)
                oldParent.RemoveChild(stageNode);
            _layer.AddChild(stageNode);
        }

        // [Godot] New. Adds a child (not in the tree yet) to the engine node, e.g. the sound player pool.
        internal void AttachNode(Node child)
        {
            Node oldParent = child.GetParent();
            if (oldParent == this)
                return;

            if (oldParent != null)
                oldParent.RemoveChild(child);
            AddChild(child);
        }

        // [Godot] New.
        internal void ApplyCanvasLayer(int layer)
        {
            if (_layer != null && IsInstanceValid(_layer))
                _layer.Layer = layer;
        }

        void EnsureLayer()
        {
            if (_layer != null && IsInstanceValid(_layer) && !_layer.IsQueuedForDeletion())
                return;

            // after a C# hot reload the managed fields are reset but the native child still exists
            _layer = GetNodeOrNull<CanvasLayer>("StageLayer");
            if (_layer == null)
            {
                _layer = new CanvasLayer();
                _layer.Name = "StageLayer";
                AddChild(_layer);
            }
            _layer.Layer = Stage.canvasLayer;
        }

        // [Godot] RuntimeInitializeOnLoadMethod (domain-reload reset of beingQuit) removed: Godot resets statics on assembly reload.
    }
}
