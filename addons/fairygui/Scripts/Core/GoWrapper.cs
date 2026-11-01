// Ported from FairyGUI-unity: Assets/Scripts/Core/GoWrapper.cs
using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

namespace FairyGUI
{
    /// <summary>
    /// GoWrapper is class for wrapping common gameobject into UI display list.
    /// </summary>
    /// <remarks>
    /// [Godot] Wraps an arbitrary Godot node (Node2D, Control, an instantiated PackedScene...) by making it a child of
    /// this object's node. It then follows the FairyGUI transform, visibility, Modulate (alpha) and clip rect of its
    /// ancestors like any CanvasItem. Node3D content cannot be drawn in 2D: wrap a SubViewportContainer instead.
    /// Draw order is the node tree order, so the Unity sorting-order bookkeeping is not needed.
    /// </remarks>
    public class GoWrapper : DisplayObject
    {
        [Obsolete("No need to manually set this flag anymore, coz it will be handled automatically.")]
        public bool supportStencil;

        public event Action<UpdateContext> onUpdate;
        public Action<Dictionary<Material, Material>> customCloneMaterials;
        public Action customRecoverMaterials;

        protected Node _wrapTarget;
        protected List<RendererInfo> _renderers;
        protected Dictionary<Material, Material> _materialsBackup;
        protected Control _canvas; // [Godot] Unity: Canvas. A wrapped Control plays the role of a uGUI canvas.
        protected bool _cloneMaterial;
        protected bool _shouldCloneMaterial;

        protected struct RendererInfo
        {
            public CanvasItem renderer; // [Godot] Unity: Renderer
            public Material[] materials; // [Godot] a CanvasItem has one material: materials.Length is 1
            public int sortingOrder; // [Godot] CanvasItem.ZIndex
        }

        protected static List<CanvasItem> helperTransformList = new List<CanvasItem>();

        /// <summary>
        ///
        /// </summary>
        public GoWrapper()
        {
            _renderers = new List<RendererInfo>();
            _materialsBackup = new Dictionary<Material, Material>();

            CreateGameObject("GoWrapper");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="go">包装对象。</param>
        public GoWrapper(Node go) : this()
        {
            SetWrapTarget(go, false);
        }

        /// <summary>
        /// 设置包装对象。注意如果原来有包装对象，设置新的包装对象后，原来的包装对象只会被删除引用，但不会被销毁。
        /// 对象包含的所有材质不会被复制，如果材质已经是公用的，这可能影响到其他对象。如果希望自动复制，改为使用SetWrapTarget(target, true)设置。
        /// </summary>
        public Node wrapTarget
        {
            get { return _wrapTarget; }
            set { SetWrapTarget(value, false); }
        }

        [Obsolete("setWrapTarget is deprecated. Use SetWrapTarget instead.")]
        public void setWrapTarget(Node target, bool cloneMaterial)
        {
            SetWrapTarget(target, cloneMaterial);
        }

        /// <summary>
        ///  设置包装对象。注意如果原来有包装对象，设置新的包装对象后，原来的包装对象只会被删除引用，但不会被销毁。
        /// </summary>
        /// <param name="target"></param>
        /// <param name="cloneMaterial">如果true，则复制材质，否则直接使用sharedMaterial。</param>
        public void SetWrapTarget(Node target, bool cloneMaterial)
        {
            InvalidateBatchingState();

            RecoverMaterials();

            _cloneMaterial = cloneMaterial;
            if (_wrapTarget != null && GodotObject.IsInstanceValid(_wrapTarget))
            {
                // [Godot] Unity: _wrapTarget.transform.SetParent(null, false). The old target is detached, not destroyed.
                Node oldParent = _wrapTarget.GetParent();
                if (oldParent != null)
                    oldParent.RemoveChild(_wrapTarget);
            }

            _canvas = null;
            _wrapTarget = target;
            _shouldCloneMaterial = false;
            _renderers.Clear();

            if (_wrapTarget != null)
            {
                // [Godot] Unity: _wrapTarget.transform.SetParent(this.cachedTransform, false)
                Node p = _wrapTarget.GetParent();
                if (p != node)
                {
                    if (p != null)
                        p.RemoveChild(_wrapTarget);
                    node.AddChild(_wrapTarget);
                }

                _canvas = _wrapTarget as Control;
                if (_canvas != null)
                {
                    // [Godot] Unity switches the Canvas to world space and puts its top-left corner at the origin.
                    // A Control under a Node2D ignores anchors and uses its own position/size.
                    _canvas.Position = Vector2.Zero;
                    this.SetSize(_canvas.Size.X, _canvas.Size.Y);
                }
                else
                {
                    CacheRenderers();
                    this.SetSize(0, 0);
                }

                SetGoLayers(this.layer);
            }
        }

        override internal void _SetLayerDirect(int value)
        {
            base._SetLayerDirect(value); // [Godot] Unity: gameObject.layer = value
            if (_wrapTarget != null)//这个if是为了在GoWrapper里使用模糊效果
                SetGoLayers(value);
        }

        /// <summary>
        /// GoWrapper will cache all renderers of your gameobject on constructor.
        /// If your gameobject change laterly, call this function to update the cache.
        /// GoWrapper会在构造函数里查询你的gameobject所有的Renderer并保存。如果你的gameobject
        /// 后续发生了改变，调用这个函数通知GoWrapper重新查询和保存。
        /// </summary>
        /// <remarks>
        /// [Godot] The cache holds every CanvasItem of the wrapped subtree. It is only used to clone/recover materials
        /// (SetWrapTarget(target, true)); clipping and draw order need no per-renderer work in Godot.
        /// </remarks>
        public void CacheRenderers()
        {
            if (_canvas != null)
                return;

            RecoverMaterials();
            _renderers.Clear();

            // [Godot] Unity: _wrapTarget.GetComponentsInChildren<Renderer>(true)
            CollectCanvasItems(_wrapTarget, helperTransformList);

            int cnt = helperTransformList.Count;
            _renderers.Capacity = cnt;
            for (int i = 0; i < cnt; i++)
            {
                CanvasItem r = helperTransformList[i];
                Material[] mats = new Material[] { r.Material };
                RendererInfo ri = new RendererInfo()
                {
                    renderer = r,
                    materials = mats,
                    sortingOrder = r.ZIndex
                };
                _renderers.Add(ri);

                // [Godot] Unity moves MeshRenderer materials to the transparent render queue (3000) so they render like
                // UI. Godot draws CanvasItems in tree order, there is no render queue.
            }
            helperTransformList.Clear();
            _renderers.Sort((RendererInfo c1, RendererInfo c2) =>
            {
                return c1.sortingOrder - c2.sortingOrder;
            });

            _shouldCloneMaterial = _cloneMaterial;
        }

        static void CollectCanvasItems(Node root, List<CanvasItem> result)
        {
            if (root == null || !GodotObject.IsInstanceValid(root))
                return;

            if (root is CanvasItem)
                result.Add((CanvasItem)root);

            int cnt = root.GetChildCount(true);
            for (int i = 0; i < cnt; i++)
                CollectCanvasItems(root.GetChild(i, true), result);
        }

        void CloneMaterials()
        {
            _shouldCloneMaterial = false;

            int cnt = _renderers.Count;
            for (int i = 0; i < cnt; i++)
            {
                RendererInfo ri = _renderers[i];
                Material[] mats = ri.materials;
                if (mats == null)
                    continue;

                // [Godot] Unity also moves MeshRenderer materials to render queue 3000; Godot has no render queue.

                int mcnt = mats.Length;
                for (int j = 0; j < mcnt; j++)
                {
                    Material mat = mats[j];
                    if (mat == null)
                        continue;

                    //确保相同的材质不会复制两次
                    Material newMat;
                    if (!_materialsBackup.TryGetValue(mat, out newMat))
                    {
                        newMat = (Material)mat.Duplicate();
                        _materialsBackup[mat] = newMat;
                    }
                    mats[j] = newMat;
                }

                if (customCloneMaterials != null)
                    customCloneMaterials.Invoke(_materialsBackup);
                else if (ri.renderer != null && GodotObject.IsInstanceValid(ri.renderer))
                    ri.renderer.Material = mats[0]; // [Godot] Unity: renderer.sharedMaterials = mats
            }
        }

        void RecoverMaterials()
        {
            if (_materialsBackup.Count == 0)
                return;

            int cnt = _renderers.Count;
            for (int i = 0; i < cnt; i++)
            {
                RendererInfo ri = _renderers[i];
                if (ri.renderer == null || !GodotObject.IsInstanceValid(ri.renderer))
                    continue;

                Material[] mats = ri.materials;
                if (mats == null)
                    continue;

                int mcnt = mats.Length;
                for (int j = 0; j < mcnt; j++)
                {
                    Material mat = mats[j];

                    foreach (KeyValuePair<Material, Material> kv in _materialsBackup)
                    {
                        if (kv.Value == mat)
                            mats[j] = kv.Key;
                    }
                }

                if (customRecoverMaterials != null)
                    customRecoverMaterials.Invoke();
                else
                    ri.renderer.Material = mats[0]; // [Godot] Unity: renderer.sharedMaterials = mats
            }

            // [Godot] Unity destroys the cloned materials (DestroyImmediate). Godot materials are reference counted.
            _materialsBackup.Clear();
        }

        public override void SetRenderingOrder(UpdateContext context, bool inBatch)
        {
            base.SetRenderingOrder(context, inBatch);

            // [Godot] Unity assigns sortingOrder to the canvas / every renderer, reserving one rendering order per distinct
            // original sortingOrder. Godot draws the wrapped nodes in tree order (their own ZIndex still applies), so
            // only the rendering order counter is advanced the same way.
            if (_canvas == null)
            {
                int cnt = _renderers.Count;
                for (int i = 0; i < cnt; i++)
                {
                    RendererInfo ri = _renderers[i];
                    if (ri.renderer != null)
                    {
                        if (i != 0 && _renderers[i].sortingOrder != _renderers[i - 1].sortingOrder)
                            context.renderingOrder++;
                    }
                }
            }
        }

        public override BatchElement AddToBatch(List<BatchElement> batchElements, bool force)
        {
            if (this._wrapTarget != null)
            {
                BatchElement batchElement = base.AddToBatch(batchElements, true);
                batchElement.breakBatch = true;
                return batchElement;
            }
            else
                return null;
        }

        override protected bool SetLayer(int value, bool fromParent)
        {
            if (base.SetLayer(value, fromParent))
            {
                SetGoLayers(value);
                return true;
            }
            else
                return false;
        }

        protected void SetGoLayers(int layer)
        {
            if (_wrapTarget == null)
                return;

            // [Godot] Unity sets gameObject.layer on the whole wrapped hierarchy so the camera that renders the UI (or
            // the capture camera in painting mode) sees it. Godot 2D has no camera culling layers: DisplayObject.layer
            // is informational and painting mode groups nodes in place, so the wrapped nodes (and their own
            // VisibilityLayer settings) are left unchanged.
        }

        override public void Update(UpdateContext context)
        {
            if (onUpdate != null)
                onUpdate(context);

            if (_shouldCloneMaterial)
                CloneMaterials();

            ApplyClipping(context);

            base.Update(context);
        }

        virtual protected void ApplyClipping(UpdateContext context)
        {
            // [Godot] Unity writes the clip box / stencil state of the context into every material of the wrapped
            // renderers. In Godot the clip rect of an ancestor (RenderingServer.CanvasItemSetClip) and clip_children
            // masks already apply to every descendant CanvasItem, including the wrapped nodes, so no material needs to
            // change. Renderers that were freed by their owner are dropped from the cache.
            int cnt = _renderers.Count;
            for (int i = cnt - 1; i >= 0; i--)
            {
                CanvasItem renderer = _renderers[i].renderer;
                if (renderer == null || !GodotObject.IsInstanceValid(renderer))
                    _renderers.RemoveAt(i);
            }
        }

        public override void Dispose()
        {
            if ((_flags & Flags.Disposed) != 0)
                return;

            if (_wrapTarget != null)
            {
                // [Godot] Unity: Object.Destroy(_wrapTarget)
                if (GodotObject.IsInstanceValid(_wrapTarget))
                {
                    if (_wrapTarget.IsInsideTree())
                        _wrapTarget.QueueFree();
                    else
                        _wrapTarget.Free();
                }
                _wrapTarget = null;

                if (_materialsBackup.Count > 0)
                { //如果有备份，说明材质是复制出来的，应该删除
                    // [Godot] cloned materials are reference counted: dropping the references releases them
                    _materialsBackup.Clear();
                }
            }

            _renderers = null;
            _materialsBackup = null;
            _canvas = null;
            customCloneMaterials = null;
            customRecoverMaterials = null;

            base.Dispose();
        }
    }
}
