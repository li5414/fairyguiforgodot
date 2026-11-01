// Ported from FairyGUI-unity: Assets/Scripts/Core/Container.cs
using System;
using System.Collections;
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    // [Godot] Node structure.
    // - The display list order is the Godot child order: every child's node (or its painting host) is a child of
    //   this container's node, in the same order as _children (AddChild/MoveChild/RemoveChild, no ZIndex).
    // - clipRect: RenderingServer.CanvasItemSetCustomRect + CanvasItemSetClip on node's canvas item (axis aligned in
    //   screen space, also clips this container's own graphics). clipSoftness is not supported.
    // - mask (normal): Godot needs the mask to be the parent of the masked content, so an internal node is added:
    //     node
    //       MaskRoot (internal, identity, no material)
    //         <mask node>            ClipChildren = Only, UseParentMaterial (the default clip shader is used)
    //           MaskContent (internal back child, transform = inverse of the mask node transform)
    //             <other children, in display list order>
    // - mask (reversed): Godot has no inverted ClipChildren. The content is grouped and the mask erases it:
    //     node
    //       MaskRoot (internal, CanvasGroup Transparent)
    //         <other children, in display list order>
    //         MaskEraser (shader blend_mul, COLOR = 1 - mask alpha)
    //           <mask node>          UseParentMaterial
    // - The mask's alpha is applied to its SelfModulate (not Modulate) so it never fades the masked content.
    // - CanvasGroup (painting mode) and ClipChildren both use the backbuffer; nesting them may not render correctly.
    public class Container : DisplayObject
    {
        /// <summary>
        ///
        /// </summary>
        public RenderMode renderMode;

        /// <summary>
        /// [Godot] Informational: the canvas transform of the node already includes any Camera2D.
        /// </summary>
        public Camera2D renderCamera;

        /// <summary>
        ///
        /// </summary>
        public bool opaque;

        /// <summary>
        /// [Godot] Not supported, clipping is always hard.
        /// </summary>
        public Vector4? clipSoftness;

        /// <summary>
        ///
        /// </summary>
        public IHitTest hitArea;

        /// <summary>
        ///
        /// </summary>
        public bool touchChildren;

        /// <summary>
        ///
        /// </summary>
        public event Action onUpdate;

        /// <summary>
        /// [Godot] Changed from a field to a property: changing it rebuilds the mask node structure.
        /// </summary>
        public bool reversedMask
        {
            get { return _reversedMask; }
            set
            {
                if (_reversedMask != value)
                {
                    _reversedMask = value;
                    UpdateMaskStructure();
                }
            }
        }

        List<DisplayObject> _children;
        DisplayObject _mask;
        Rect? _clipRect;
        bool _reversedMask;

        internal int _panelOrder;
        internal DisplayObject _lastFocus;

        // [Godot] mask node structure (see the class comment)
        internal bool _maskApplied;
        internal DisplayObject _maskAppliedTo;
        bool _maskReversedApplied;
        Node2D _maskRoot;
        Node2D _maskContent;
        Node2D _maskEraser;
        static ShaderMaterial _maskEraseMaterial;

        /// <summary>
        ///
        /// </summary>
        public Container()
            : base()
        {
            CreateGameObject("Container");
            Init();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="gameObjectName"></param>
        public Container(string gameObjectName)
            : base()
        {
            CreateGameObject(gameObjectName);
            Init();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="attachTarget">[Godot] Node2D instead of GameObject (see DisplayObject.SetGameObject).</param>
        public Container(Node2D attachTarget)
            : base()
        {
            SetGameObject(attachTarget);
            Init();
        }

        void Init()
        {
            _children = new List<DisplayObject>();
            touchChildren = true;
        }

        /// <summary>
        ///
        /// </summary>
        public int numChildren
        {
            get { return _children.Count; }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="child"></param>
        /// <returns></returns>
        public DisplayObject AddChild(DisplayObject child)
        {
            AddChildAt(child, _children.Count);
            return child;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="child"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public DisplayObject AddChildAt(DisplayObject child, int index)
        {
            int count = _children.Count;
            if (index >= 0 && index <= count)
            {
                if (child.parent == this)
                {
                    SetChildIndex(child, index);
                }
                else
                {
                    child.RemoveFromParent();
                    if (index == count)
                        _children.Add(child);
                    else
                        _children.Insert(index, child);
                    child.InternalSetParent(this);

                    // [Godot] the mask may have been assigned before it was added
                    if (child == _mask)
                        UpdateMaskStructure();

                    if (stage != null)
                    {
                        if (child is Container)
                            child.BroadcastEvent("onAddedToStage", null);
                        else
                            child.DispatchEvent("onAddedToStage", null);
                    }

                    InvalidateBatchingState(true);
                }
                return child;
            }
            else
            {
                throw new Exception("Invalid child index");
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="child"></param>
        /// <returns></returns>
        public bool Contains(DisplayObject child)
        {
            return _children.Contains(child);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public DisplayObject GetChildAt(int index)
        {
            return _children[index];
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public DisplayObject GetChild(string name)
        {
            int cnt = _children.Count;
            for (int i = 0; i < cnt; ++i)
            {
                if (_children[i].name == name)
                    return _children[i];
            }

            return null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public DisplayObject[] GetChildren()
        {
            return _children.ToArray();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="child"></param>
        /// <returns></returns>
        public int GetChildIndex(DisplayObject child)
        {
            return _children.IndexOf(child);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="child"></param>
        /// <returns></returns>
        public DisplayObject RemoveChild(DisplayObject child)
        {
            return RemoveChild(child, false);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="child"></param>
        /// <param name="dispose"></param>
        /// <returns></returns>
        public DisplayObject RemoveChild(DisplayObject child, bool dispose)
        {
            if (child.parent != this)
                throw new Exception("obj is not a child");

            int i = _children.IndexOf(child);
            if (i >= 0)
                return RemoveChildAt(i, dispose);
            else
                return null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public DisplayObject RemoveChildAt(int index)
        {
            return RemoveChildAt(index, false);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="index"></param>
        /// <param name="dispose"></param>
        /// <returns></returns>
        public DisplayObject RemoveChildAt(int index, bool dispose)
        {
            if (index >= 0 && index < _children.Count)
            {
                DisplayObject child = _children[index];

                if (stage != null && (child._flags & Flags.Disposed) == 0)
                {
                    if (child is Container)
                    {
                        child.BroadcastEvent("onRemovedFromStage", null);
                        if (child == Stage.inst.focus || ((Container)child).IsAncestorOf(Stage.inst.focus))
                            Stage.inst._OnFocusRemoving(this);
                    }
                    else
                    {
                        child.DispatchEvent("onRemovedFromStage", null);
                        if (child == Stage.inst.focus)
                            Stage.inst._OnFocusRemoving(this);
                    }
                }
                _children.Remove(child);
                // [Godot] removing the mask tears down the mask node structure first: the masked content lives
                // under the mask's node and must not leave (or be freed) with it.
                if (child == _maskAppliedTo)
                    UpdateMaskStructure();
                InvalidateBatchingState(true);
                if (!dispose)
                    child.InternalSetParent(null);
                else
                    child.Dispose();

                return child;
            }
            else
                throw new Exception("Invalid child index");
        }

        /// <summary>
        ///
        /// </summary>
        public void RemoveChildren()
        {
            RemoveChildren(0, int.MaxValue, false);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="beginIndex"></param>
        /// <param name="endIndex"></param>
        /// <param name="dispose"></param>
        public void RemoveChildren(int beginIndex, int endIndex, bool dispose)
        {
            if (endIndex < 0 || endIndex >= numChildren)
                endIndex = numChildren - 1;

            for (int i = beginIndex; i <= endIndex; ++i)
                RemoveChildAt(beginIndex, dispose);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="child"></param>
        /// <param name="index"></param>
        public void SetChildIndex(DisplayObject child, int index)
        {
            int oldIndex = _children.IndexOf(child);
            if (oldIndex == index) return;
            if (oldIndex == -1) throw new ArgumentException("Not a child of this container");
            _children.RemoveAt(oldIndex);
            if (index >= _children.Count)
                _children.Add(child);
            else
                _children.Insert(index, child);
            AttachChildNode(child); // [Godot] mirror the order in the node tree
            InvalidateBatchingState(true);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="child1"></param>
        /// <param name="child2"></param>
        public void SwapChildren(DisplayObject child1, DisplayObject child2)
        {
            int index1 = _children.IndexOf(child1);
            int index2 = _children.IndexOf(child2);
            if (index1 == -1 || index2 == -1)
                throw new Exception("Not a child of this container");
            SwapChildrenAt(index1, index2);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="index1"></param>
        /// <param name="index2"></param>
        public void SwapChildrenAt(int index1, int index2)
        {
            DisplayObject obj1 = _children[index1];
            DisplayObject obj2 = _children[index2];
            _children[index1] = obj2;
            _children[index2] = obj1;
            SyncChildrenOrder(); // [Godot]
            InvalidateBatchingState(true);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="indice"></param>
        /// <param name="objs"></param>
        public void ChangeChildrenOrder(IList<int> indice, IList<DisplayObject> objs)
        {
            int cnt = objs.Count;
            for (int i = 0; i < cnt; i++)
            {
                DisplayObject obj = objs[i];
                if (obj.parent != this)
                    throw new Exception("Not a child of this container");

                _children[indice[i]] = obj;
            }
            SyncChildrenOrder(); // [Godot]
            InvalidateBatchingState(true);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public IEnumerator<DisplayObject> GetDescendants(bool backward)
        {
            return new DescendantsEnumerator(this, backward);
        }

        /// <summary>
        ///
        /// </summary>
        public Rect? clipRect
        {
            get { return _clipRect; }
            set
            {
                if (_clipRect != value)
                {
                    _clipRect = value;
                    ApplyClipRect(); // [Godot]
                    UpdateBatchingFlags();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public DisplayObject mask
        {
            get { return _mask; }
            set
            {
                if (_mask != value)
                {
                    _mask = value;
                    _flags |= Flags.BatchingRequested;
                    UpdateBatchingFlags();
                    UpdateMaskStructure(); // [Godot]
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public void CreateGraphics()
        {
            if (graphics == null)
            {
                graphics = new NGraphics(this.node);
                graphics.texture = NTexture.Empty;
            }
        }

        public override Rect GetBounds(DisplayObject targetSpace)
        {
            if (_clipRect != null)
                return TransformRect((Rect)_clipRect, targetSpace);

            int count = _children.Count;

            Rect rect;
            if (count == 0)
            {
                Vector2 v = TransformPoint(Vector2.Zero, targetSpace);
                rect = Rect.MinMaxRect(v.X, v.Y, 0, 0);
            }
            else if (count == 1)
            {
                rect = _children[0].GetBounds(targetSpace);
            }
            else
            {
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;

                for (int i = 0; i < count; ++i)
                {
                    rect = _children[i].GetBounds(targetSpace);
                    minX = minX < rect.xMin ? minX : rect.xMin;
                    maxX = maxX > rect.xMax ? maxX : rect.xMax;
                    minY = minY < rect.yMin ? minY : rect.yMin;
                    maxY = maxY > rect.yMax ? maxY : rect.yMax;
                }

                rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            }

            return rect;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        // [Godot] Godot 2D needs no camera for the UI: the canvas transform (CanvasLayer/Camera2D) is already part of
        // node.GetGlobalTransformWithCanvas(). Returns renderCamera, or the viewport's current Camera2D, or null.
        public Camera2D GetRenderCamera()
        {
            if (renderMode == RenderMode.ScreenSpaceOverlay)
                return null;
            else
            {
                Camera2D cam = this.renderCamera;
                if (cam == null && node != null && GodotObject.IsInstanceValid(node) && node.IsInsideTree())
                    cam = node.GetViewport().GetCamera2D();
                return cam;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="stagePoint"></param>
        /// <param name="forTouch"></param>
        /// <param name="displayIndex"></param>
        /// <returns></returns>
        public DisplayObject HitTest(Vector2 stagePoint, bool forTouch)
        {
            if (node == null || !GodotObject.IsInstanceValid(node))
            {
                if (this is Stage)
                    return this;
                else
                    return null;
            }

            // [Godot] stagePoint is in viewport coordinates (y down, no flip, single display: no Display.RelativeMouseAt).
            // The world point is the stage point converted to canvas global space, same as GlobalToLocal.
            HitTestContext.screenPoint = new Vector3(stagePoint.X, stagePoint.Y, 0);
            Stage stageObj = this as Stage;
            if (stageObj == null)
                stageObj = Stage.inst;
            HitTestContext.worldPoint = stageObj.LocalToWorld(HitTestContext.screenPoint);
            HitTestContext.direction = UVec.back;
            HitTestContext.forTouch = forTouch;

            DisplayObject ret = HitTest();
            if (ret != null)
                return ret;
            else if (this is Stage)
                return this;
            else
                return null;
        }

        override protected DisplayObject HitTest()
        {
            if ((_flags & Flags.UserGameObject) != 0 && (node == null || !GodotObject.IsInstanceValid(node) || !node.IsVisibleInTree()))
                return null;

            if (this.scaleX == 0 || this.scaleY == 0)
                return null;

            Vector3 savedWorldPoint = HitTestContext.worldPoint;
            Vector3 savedDirection = HitTestContext.direction;
            DisplayObject target;

            // [Godot] Unity re-projected the screen point with the render camera for ScreenSpaceCamera/WorldSpace
            // containers and rejected other displays. Here the canvas transform of each node already includes any
            // Camera2D/CanvasLayer, so WorldToLocal works the same for every render mode.

            target = HitTest_Container();

            HitTestContext.worldPoint = savedWorldPoint;
            HitTestContext.direction = savedDirection;

            return target;
        }

        DisplayObject HitTest_Container()
        {
            Vector2 localPoint = WorldToLocal(HitTestContext.worldPoint, HitTestContext.direction).ToV2();
            // [Godot] no vertex matrix (skew/perspective are part of the node transform), so the world point stays valid.

            if (hitArea != null)
            {
                if (!hitArea.HitTest(_contentRect, localPoint))
                    return null;

                if (hitArea is MeshColliderHitTest)
                    localPoint = ((MeshColliderHitTest)hitArea).lastHit;
            }
            else
            {
                if (_clipRect != null && !((Rect)_clipRect).Contains(localPoint))
                    return null;
            }

            if (_mask != null)
            {
                DisplayObject tmp = _mask.InternalHitTestMask();
                if (!reversedMask && tmp == null || reversedMask && tmp != null)
                    return null;
            }

            DisplayObject target = null;
            if (touchChildren)
            {
                int count = _children.Count;
                for (int i = count - 1; i >= 0; --i) // front to back!
                {
                    DisplayObject child = _children[i];
                    if ((child._flags & Flags.GameObjectDisposed) != 0)
                    {
                        child.DisplayDisposedWarning();
                        continue;
                    }

                    if (child == _mask || (child._flags & Flags.TouchDisabled) != 0)
                        continue;

                    target = child.InternalHitTest();
                    if (target != null)
                        break;
                }
            }

            if (target == null && opaque && (hitArea != null || _contentRect.Contains(localPoint)))
                target = this;

            return target;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool IsAncestorOf(DisplayObject obj)
        {
            if (obj == null)
                return false;

            Container p = obj.parent;
            while (p != null)
            {
                if (p == this)
                    return true;

                p = p.parent;
            }
            return false;
        }

        /// <summary>
        ///
        /// </summary>
        // [Godot] Kept for API compatibility. Godot batches 2D draw calls itself, so FairyGUI batching does nothing.
        public bool fairyBatching
        {
            get { return (_flags & Flags.FairyBatching) != 0; }
            set
            {
                bool oldValue = (_flags & Flags.FairyBatching) != 0;
                if (oldValue != value)
                {
                    if (value)
                        _flags |= Flags.FairyBatching;
                    else
                        _flags &= ~Flags.FairyBatching;
                    UpdateBatchingFlags();
                }
            }
        }

        internal void UpdateBatchingFlags()
        {
            bool oldValue = (_flags & Flags.BatchingRoot) != 0;
            bool newValue = (_flags & Flags.FairyBatching) != 0 || _clipRect != null || _mask != null || _paintingMode > 0;
            if (newValue)
                _flags |= Flags.BatchingRoot;
            else
                _flags &= ~Flags.BatchingRoot;
            if (oldValue != newValue)
            {
                if (newValue)
                    _flags |= Flags.BatchingRequested;

                InvalidateBatchingState();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="childrenChanged"></param>
        public void InvalidateBatchingState(bool childrenChanged)
        {
            if (childrenChanged && (_flags & Flags.BatchingRoot) != 0)
                _flags |= Flags.BatchingRequested;
            else
            {
                Container p = this.parent;
                while (p != null)
                {
                    if ((p._flags & Flags.BatchingRoot) != 0)
                    {
                        p._flags |= Flags.BatchingRequested;
                        break;
                    }

                    p = p.parent;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value"></param>
        public void SetChildrenLayer(int value)
        {
            int cnt = _children.Count;
            for (int i = 0; i < cnt; i++)
            {
                DisplayObject child = _children[i];
                // [Godot] there is no separate painting layer, painting-mode children get the value directly
                child._SetLayerDirect(value);
                if ((child is Container) && child._paintingMode == 0)
                    ((Container)child).SetChildrenLayer(value);
            }
        }

        override public void Update(UpdateContext context)
        {
            if ((_flags & Flags.UserGameObject) != 0 && (node == null || !GodotObject.IsInstanceValid(node) || !node.IsVisibleInTree()))
                return;

            base.Update(context);

            if (_paintingMode != 0)
            {
                if ((_flags & Flags.CacheAsBitmap) != 0 && _paintingInfo.flag == 2)
                {
                    if (onUpdate != null)
                        onUpdate();
                    return;
                }

                context.EnterPaintingMode();
            }

            if (_mask != null)
            {
                // [Godot] no stencil pre-pass (_PreUpdateMask): the mask is drawn by the node structure.
                context.EnterClipping(this.id, reversedMask);
            }
            else if (_clipRect != null)
                context.EnterClipping(this.id, this.TransformRect((Rect)_clipRect, null), clipSoftness);

            float savedAlpha = context.alpha;
            context.alpha *= this.alpha;
            bool savedGrayed = context.grayed;
            context.grayed = context.grayed || this.grayed;

            if ((_flags & Flags.FairyBatching) != 0)
                context.batchingDepth++;

            // [Godot] Unity had a separate path for batching (DoFairyBatching reordered the rendering orders). Godot
            // draws in node order and batches by itself, so the normal path is used in both cases.
            if (_mask != null)
                _mask.SetRenderingOrder(context, false);

            int cnt = _children.Count;
            for (int i = 0; i < cnt; i++)
            {
                DisplayObject child = _children[i];
                if ((child._flags & Flags.GameObjectDisposed) != 0)
                {
                    child.DisplayDisposedWarning();
                    continue;
                }

                if (child.visible)
                {
                    if (child != _mask) //if not a mask
                        child.SetRenderingOrder(context, false);

                    child.Update(context);
                }
            }

            // [Godot] no stencil eraser order for the mask.

            if ((_flags & Flags.FairyBatching) != 0)
                context.batchingDepth--;

            context.alpha = savedAlpha;
            context.grayed = savedGrayed;

            if (_clipRect != null || _mask != null)
                context.LeaveClipping();

            if (_paintingMode != 0)
            {
                context.LeavePaintingMode();
                UpdateContext.OnEnd += _paintingInfo.captureDelegate;
            }

            if (onUpdate != null)
                onUpdate();
        }

        public override void Dispose()
        {
            if ((_flags & Flags.Disposed) != 0)
                return;

            base.Dispose(); //Destroy GameObject tree first, avoid destroying each seperately;
            // [Godot] the children are disposed in DisposeChildren(), called by base.Dispose() before the node is
            // freed (freeing a Godot node frees its whole subtree, the children must clean up first).
        }

        internal override void DisposeChildren()
        {
            // [Godot] the masked content is under the mask's node; restore the plain structure so that disposing
            // the mask does not free its siblings' nodes.
            if (_maskApplied)
                TeardownMaskStructure();

            int numChildren = _children.Count;
            for (int i = numChildren - 1; i >= 0; --i)
            {
                DisplayObject obj = _children[i];
                obj.InternalSetParent(null); //Avoid RemoveParent call
                obj.Dispose();
            }
        }

        /// <summary>
        /// If true, when the container is focused, tab navigation is lock inside it.
        /// </summary>
        public bool tabStopChildren
        {
            get { return (_flags & Flags.TabStopChildren) != 0; }
            set
            {
                if (value)
                    _flags |= Flags.TabStopChildren;
                else
                    _flags &= ~Flags.TabStopChildren;
            }
        }

        struct DescendantsEnumerator : IEnumerator<DisplayObject>
        {
            Container _root;
            Container _com;
            DisplayObject _current;
            int _index;
            bool _forward;

            public DescendantsEnumerator(Container root, bool backward)
            {
                _root = root;
                _com = _root;
                _current = null;
                _forward = !backward;
                if (_forward)
                    _index = 0;
                else
                    _index = _com._children.Count - 1;
            }

            public DisplayObject Current
            {
                get { return _current; }
            }

            object IEnumerator.Current
            {
                get { return _current; }
            }

            public bool MoveNext()
            {
                if (_forward)
                {
                    if (_index >= _com._children.Count)
                    {
                        if (_com == _root)
                        {
                            _current = null;
                            return false;
                        }

                        _current = _com;
                        _com = _com.parent;
                        _index = _com.GetChildIndex(_current) + 1;
                        return true;
                    }
                    else
                    {
                        DisplayObject obj = _com._children[_index];
                        if (obj is Container)
                        {
                            _com = (Container)obj;
                            _index = 0;
                            return MoveNext();
                        }
                        _index++;
                        _current = obj;
                        return true;
                    }
                }
                else
                {
                    if (_index < 0)
                    {
                        if (_com == _root)
                        {
                            _current = null;
                            return false;
                        }

                        _current = _com;
                        _com = _com.parent;
                        _index = _com.GetChildIndex(_current) - 1;
                        return true;
                    }
                    else
                    {
                        DisplayObject obj = _com._children[_index];
                        if (obj is Container)
                        {
                            _com = (Container)obj;
                            _index = _com._children.Count - 1;
                            return MoveNext();
                        }
                        _index--;
                        _current = obj;
                        return true;
                    }
                }
            }

            public void Reset()
            {
                _com = _root;
                _current = null;
                _index = 0;
            }

            public void Dispose()
            {
            }
        }

        // ------------------------------------------------------------------------------------------------
        // [Godot] node tree helpers (no Unity counterpart)
        // ------------------------------------------------------------------------------------------------

        void ApplyClipRect()
        {
            if (node == null || !GodotObject.IsInstanceValid(node))
                return;

            Rid rid = node.GetCanvasItem();
            if (_clipRect != null)
            {
                RenderingServer.CanvasItemSetCustomRect(rid, true, (Rect2)(Rect)_clipRect);
                RenderingServer.CanvasItemSetClip(rid, true);
            }
            else
            {
                RenderingServer.CanvasItemSetClip(rid, false);
                RenderingServer.CanvasItemSetCustomRect(rid, false, new Rect2());
            }
        }

        /// <summary>
        /// The node under which the children (except an applied mask) are attached.
        /// </summary>
        Node2D GetChildrenHost()
        {
            if (_maskApplied)
                return _maskReversedApplied ? _maskRoot : _maskContent;
            return node;
        }

        static void ReparentNode(Node n, Node newParent)
        {
            Node p = n.GetParent();
            if (p == newParent)
                return;
            if (p != null)
                p.RemoveChild(n);
            newParent.AddChild(n);
        }

        /// <summary>
        /// [Godot] Puts the child's outer node (node or painting host) under the right host node, at the position that
        /// matches the display list order. Called by DisplayObject.UpdateHierarchy and SetChildIndex.
        /// </summary>
        internal void AttachChildNode(DisplayObject child)
        {
            if ((child._flags & Flags.UserGameObject) != 0 || node == null)
                return;

            Node2D outer = child.GetOuterNode();
            if (outer == null || !GodotObject.IsInstanceValid(outer))
                return;

            if (_maskApplied && child == _maskAppliedTo)
            {
                ReparentNode(outer, _maskReversedApplied ? _maskEraser : _maskRoot);
                return;
            }

            Node2D host = GetChildrenHost();
            ReparentNode(outer, host);
            PlaceChildNode(child, outer, host);
        }

        bool IsOrderedIn(DisplayObject obj, Node host, out Node2D outer)
        {
            outer = null;
            if (obj == _maskAppliedTo && _maskApplied || (obj._flags & Flags.UserGameObject) != 0)
                return false;
            outer = obj.GetOuterNode();
            return outer != null && GodotObject.IsInstanceValid(outer) && outer.GetParent() == host;
        }

        /// <summary>
        /// [Godot] Moves `outer` right after the previous sibling (or before the next one) that lives in the same host.
        /// Foreign Godot nodes in the host are left where they are.
        /// </summary>
        void PlaceChildNode(DisplayObject child, Node2D outer, Node2D host)
        {
            int index = _children.IndexOf(child);
            if (index < 0)
                return;

            int cur = outer.GetIndex();
            Node2D other;
            for (int i = index - 1; i >= 0; i--)
            {
                if (IsOrderedIn(_children[i], host, out other))
                {
                    int ai = other.GetIndex();
                    int target = cur < ai ? ai : ai + 1;
                    if (target != cur)
                        host.MoveChild(outer, target);
                    return;
                }
            }

            int cnt = _children.Count;
            for (int i = index + 1; i < cnt; i++)
            {
                if (IsOrderedIn(_children[i], host, out other))
                {
                    int bi = other.GetIndex();
                    int target = cur < bi ? bi - 1 : bi;
                    if (target != cur)
                        host.MoveChild(outer, target);
                    return;
                }
            }
        }

        /// <summary>
        /// [Godot] Reorders the nodes of all children to match _children (after swaps and reorders).
        /// </summary>
        void SyncChildrenOrder()
        {
            Node2D host = GetChildrenHost();
            if (host == null)
                return;

            Node2D prev = null;
            int cnt = _children.Count;
            for (int i = 0; i < cnt; i++)
            {
                Node2D outer;
                if (!IsOrderedIn(_children[i], host, out outer))
                    continue;

                if (prev != null)
                {
                    int cur = outer.GetIndex();
                    int ai = prev.GetIndex();
                    if (cur != ai + 1)
                        host.MoveChild(outer, cur < ai ? ai : ai + 1);
                }
                prev = outer;
            }
        }

        /// <summary>
        /// [Godot] Builds or tears down the mask node structure (see the class comment) when mask, reversedMask or
        /// the mask's membership changes. The mask is only applied while it is a child of this container.
        /// </summary>
        void UpdateMaskStructure()
        {
            if ((_flags & Flags.Disposed) != 0 || node == null || !GodotObject.IsInstanceValid(node))
                return;

            bool want = _mask != null && _children.Contains(_mask)
                && (_mask._flags & (Flags.Disposed | Flags.UserGameObject | Flags.GameObjectDisposed)) == 0
                && _mask.node != null && GodotObject.IsInstanceValid(_mask.node);

            if (want == _maskApplied && (!want || (_maskAppliedTo == _mask && _maskReversedApplied == _reversedMask)))
                return;

            if (_maskApplied)
                TeardownMaskStructure();

            if (!want)
                return;

            DisplayObject m = _mask;
            _maskApplied = true;
            _maskAppliedTo = m;
            _maskReversedApplied = _reversedMask;

            _maskRoot = new Node2D();
            _maskRoot.Name = "MaskRoot";
            node.AddChild(_maskRoot, false, Node.InternalMode.Back);

            Node2D maskOuter = m.GetOuterNode();
            if (_maskReversedApplied)
            {
                // content is grouped, then the mask shape multiplies it by (1 - mask alpha)
                RenderingServer.CanvasItemSetCanvasGroupMode(_maskRoot.GetCanvasItem(),
                    RenderingServer.CanvasGroupMode.Transparent, 5, true, 0, false);
                _maskEraser = new Node2D();
                _maskEraser.Name = "MaskEraser";
                _maskEraser.Material = GetMaskEraseMaterial();
                _maskRoot.AddChild(_maskEraser, false, Node.InternalMode.Back);
                ReparentNode(maskOuter, _maskEraser);
                m.node.UseParentMaterial = true;
            }
            else
            {
                ReparentNode(maskOuter, _maskRoot);
                m.node.ClipChildren = CanvasItem.ClipChildrenMode.Only;
                m.node.UseParentMaterial = true; //MaskRoot has no material: the default clip shader is used
                _maskContent = new Node2D();
                _maskContent.Name = "MaskContent";
                m.node.AddChild(_maskContent, false, Node.InternalMode.Back);
                SyncMaskContentTransform();
            }

            Node2D host = GetChildrenHost();
            int cnt = _children.Count;
            for (int i = 0; i < cnt; i++)
            {
                DisplayObject child = _children[i];
                if (child == m || (child._flags & Flags.UserGameObject) != 0)
                    continue;
                Node2D outer = child.GetOuterNode();
                if (outer != null && GodotObject.IsInstanceValid(outer) && outer.GetParent() == node)
                    ReparentNode(outer, host);
            }

            m.ApplyAlpha();
        }

        void TeardownMaskStructure()
        {
            DisplayObject m = _maskAppliedTo;
            _maskApplied = false;
            _maskAppliedTo = null;

            // children back to node, in display list order
            int cnt = _children.Count;
            for (int i = 0; i < cnt; i++)
            {
                DisplayObject child = _children[i];
                if (child == m || (child._flags & Flags.UserGameObject) != 0)
                    continue;
                Node2D outer = child.GetOuterNode();
                if (outer == null || !GodotObject.IsInstanceValid(outer))
                    continue;
                Node p = outer.GetParent();
                if (p != null && (p == _maskContent || p == _maskRoot))
                    ReparentNode(outer, node);
            }

            if (m != null && m.node != null && GodotObject.IsInstanceValid(m.node))
            {
                m.node.ClipChildren = CanvasItem.ClipChildrenMode.Disabled;
                m.node.UseParentMaterial = false;

                Node2D maskOuter = m.GetOuterNode();
                Node p = maskOuter.GetParent();
                if (p != null && (p == _maskRoot || p == _maskEraser))
                {
                    p.RemoveChild(maskOuter);
                    // still a child of this container: back to the normal place, otherwise it stays detached
                    if (_children.Contains(m) && (m._flags & Flags.Disposed) == 0)
                        node.AddChild(maskOuter);
                }
            }

            // MaskContent is a child of the mask node; detach it before freeing so it is freed with MaskRoot's tree
            if (_maskContent != null && GodotObject.IsInstanceValid(_maskContent))
            {
                Node p = _maskContent.GetParent();
                if (p != null)
                    p.RemoveChild(_maskContent);
                FreeNode(_maskContent);
            }
            FreeNode(_maskRoot); //also frees MaskEraser
            _maskRoot = null;
            _maskContent = null;
            _maskEraser = null;

            SyncChildrenOrder();

            if (m != null)
                m.ApplyAlpha();
        }

        /// <summary>
        /// [Godot] MaskContent is a child of the mask node; its transform cancels the mask's own transform so the
        /// masked children keep the container's coordinate space.
        /// </summary>
        internal void SyncMaskContentTransform()
        {
            if (!_maskApplied || _maskReversedApplied || _maskContent == null || _maskAppliedTo == null)
                return;

            DisplayObjectNode mn = _maskAppliedTo.node;
            if (mn == null || !GodotObject.IsInstanceValid(mn))
                return;

            Transform2D t = mn.Transform;
            float det = t.Determinant();
            if (det == 0 || float.IsNaN(det) || float.IsInfinity(det))
                _maskContent.Transform = Transform2D.Identity;
            else
                _maskContent.Transform = t.AffineInverse();
        }

        static ShaderMaterial GetMaskEraseMaterial()
        {
            if (_maskEraseMaterial == null)
            {
                Shader shader = new Shader();
                shader.Code = "// FairyGUI reversed mask eraser\n"
                    + "shader_type canvas_item;\n"
                    + "render_mode unshaded, blend_mul;\n"
                    + "void fragment() {\n"
                    + "\tfloat k = 1.0 - COLOR.a;\n"
                    + "\tCOLOR = vec4(k, k, k, k);\n"
                    + "}\n";
                _maskEraseMaterial = new ShaderMaterial();
                _maskEraseMaterial.Shader = shader;
            }
            return _maskEraseMaterial;
        }
    }


    public class BatchElement
    {
        public Material material;
        public float[] bounds;
        public IBatchable owner;
        public bool isRoot;
        public bool breakBatch;

        public BatchElement(IBatchable owner, float[] bounds)
        {
            this.owner = owner;
            this.bounds = bounds ?? new float[4];
        }
    }

    /// <summary>
    ///
    /// </summary>
    public interface IBatchable
    {
        void SetRenderingOrder(UpdateContext context, bool inBatch);
    }

    /// <summary>
    /// [Godot] Replaces UnityEngine.RenderMode (Container.renderMode). Informational in Godot: every mode uses the
    /// node's canvas transform. UIPanel containers use WorldSpace, UIPainter uses WorldSpace with MeshColliderHitTest.
    /// </summary>
    public enum RenderMode
    {
        ScreenSpaceOverlay,
        ScreenSpaceCamera,
        WorldSpace
    }
}
