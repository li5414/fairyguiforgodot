// Ported from FairyGUI-unity: Assets/Scripts/UI/UIPainter.cs
using System;
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    // [Godot] Was a MonoBehaviour ([ExecuteInEditMode], [RequireComponent(MeshCollider, MeshRenderer)]) that
    // captured the UI into a RenderTexture with CaptureCamera. Now a Node that owns a SubViewport rendering the
    // FairyGUI component; the SubViewport's ViewportTexture (`texture`) is assigned to the material of `renderer`
    // (default: the parent node when it is a GeometryInstance3D, e.g. a MeshInstance3D). The container is
    // registered with Stage.RegisterPanel so it is updated every frame and counts as being on stage.
    // Input on the 3D surface is not supported: the container is never touchable (Unity used MeshColliderHitTest).
    [GlobalClass]
    public partial class UIPainter : Node, EMRenderTarget
    {
        /// <summary>
        ///
        /// </summary>
        public Container container { get; private set; }

        /// <summary>
        ///
        /// </summary>
        [Export]
        public string packageName = "";

        /// <summary>
        ///
        /// </summary>
        [Export]
        public string componentName = "";

        /// <summary>
        ///
        /// </summary>
        [Export]
        public int sortingOrder;

        // [Godot] Package asset path as used by UIPackage.AddPackage, e.g. "res://UI/Basics".
        [Export]
        string packagePath = "";
        // [Godot] renderCamera / fairyBatching removed (no capture camera; Godot batches canvas items itself).
        // [Godot] Kept for API parity only: input on the 3D surface is not supported, the container is never touchable.
        [Export]
        bool touchDisabled = false;

        /// <summary>
        /// The 3D object that shows the UI. When null, the parent node is used if it is a GeometryInstance3D.
        /// </summary>
        // [Godot] Replaces GetComponent<Renderer>() ([RequireComponent(typeof(MeshRenderer))]).
        [Export]
        public GeometryInstance3D renderer;

        GComponent _ui;
        bool _created;
        bool _captured;
        GeometryInstance3D _renderer;

        // [Godot] RenderTexture -> the ViewportTexture of the SubViewport that renders the UI.
        ViewportTexture _texture;
        SubViewport _viewport;

        // [Godot] _captureDelegate removed: the SubViewport renders every frame, no explicit capture is needed.

        /// <summary>
        /// The texture the UI is rendered to (null when no UI is created).
        /// </summary>
        // [Godot] New: Unity assigned the RenderTexture to the renderer only.
        public ViewportTexture texture
        {
            get { return _texture; }
        }

        // [Godot] OnEnable + Start. Called again after the node re-enters the tree (see _ExitTree).
        public override void _Ready()
        {
            if (this.container == null)
            {
                CreateContainer();

                if (!string.IsNullOrEmpty(packagePath) && UIPackage.GetByName(packageName) == null)
                    UIPackage.AddPackage(packagePath);
            }

            Stage.inst.RegisterPanel(this.container);

            if (!_created)
                CreateUI();
            else
                ApplyTexture(); //the UI was created through `ui` before entering the tree
        }

        // [Godot] OnDisable / OnGUI removed: they only served edit mode.

        // [Godot] OnDestroy. Runs whenever the node leaves the tree; the UI is rebuilt by _Ready if it comes back.
        public override void _ExitTree()
        {
            if (container != null)
            {
                if (_ui != null)
                {
                    _ui.Dispose();
                    _ui = null;
                }

                if (Stage.IsRegisteredPanel(container))
                    Stage.inst.UnregisterPanel(container);
                container.Dispose();
                container = null;
            }

            DestroyTexture();

            _created = false;
            RequestReady();
        }

        void CreateContainer()
        {
            // [Godot] RenderMode.WorldSpace + CaptureCamera.hiddenLayer -> the container is drawn by its own
            // SubViewport. renderCamera, fairyBatching and the MeshColliderHitTest hit area are removed.
            if (_viewport == null || !IsInstanceValid(_viewport))
            {
                _viewport = new SubViewport();
                _viewport.Name = "UIPainterViewport";
                _viewport.TransparentBg = true;
                _viewport.Disable3D = true;
                _viewport.GuiDisableInput = true;
                _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
                _viewport.Size = new Vector2I(2, 2);
                AddChild(_viewport);
            }

            this.container = new Container("UIPainter");
            _viewport.AddChild(this.container.node);
            // [Godot] input on the 3D surface is not supported: never touchable (touchDisabled is ignored)
            this.container.touchable = false;
            this.container._panelOrder = sortingOrder;
            SetSortingOrder(this.sortingOrder, true);
        }

        /// <summary>
        /// Change the sorting order of the panel in runtime.
        /// </summary>
        /// <param name="value">sorting order value</param>
        /// <param name="apply">false if you dont want the default sorting behavior.</param>
        public void SetSortingOrder(int value, bool apply)
        {
            this.sortingOrder = value;
            container._panelOrder = value;

            // [Godot] Before _Ready the container is not registered yet; RegisterPanel inserts it by _panelOrder.
            if (apply && Stage.IsRegisteredPanel(container))
                Stage.inst.ApplyPanelOrder(container);
        }

        /// <summary>
        ///
        /// </summary>
        public GComponent ui
        {
            get
            {
                if (!_created && !Engine.IsEditorHint())
                {
                    // [Godot] Unity's OnEnable had created the container already; here `ui` may be read before _Ready.
                    if (this.container == null)
                    {
                        CreateContainer();

                        if (!string.IsNullOrEmpty(packagePath) && UIPackage.GetByName(packageName) == null)
                            UIPackage.AddPackage(packagePath);
                    }

                    CreateUI();
                }

                return _ui;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public void CreateUI()
        {
            if (_ui != null)
            {
                _ui.Dispose();
                _ui = null;
                DestroyTexture();
            }

            _created = true;

            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(componentName))
                return;

            _ui = (GComponent)UIPackage.CreateObject(packageName, componentName);
            if (_ui != null)
            {
                this.container.AddChild(_ui.displayObject);
                this.container.size = _ui.size;
                // [Godot] CaptureCamera.CreateRenderTexture -> size the SubViewport; its texture is the render target.
                _viewport.Size = new Vector2I(UMath.Max(1, UMath.RoundToInt(_ui.width)), UMath.Max(1, UMath.RoundToInt(_ui.height)));
                _texture = _viewport.GetTexture();
                ApplyTexture();
                // [Godot] The capture-on-UpdateContext.OnEnd hook is removed: the SubViewport renders every frame.
            }
            else
                GD.PushError("Create " + componentName + "@" + packageName + " failed!");
        }

        // [Godot] Capture() removed: CaptureCamera.Capture is replaced by the SubViewport, which renders every frame.

        void DestroyTexture()
        {
            if (_texture != null)
            {
                // [Godot] The ViewportTexture belongs to the SubViewport; it is only detached, not destroyed.
                _texture = null;

                if (_renderer != null)
                    SetMainTexture(_renderer, null);
            }
        }

        // [Godot] _renderer = this.GetComponent<Renderer>(); _renderer.sharedMaterial.mainTexture = _texture
        void ApplyTexture()
        {
            _renderer = ResolveRenderer();
            if (_renderer != null && _texture != null)
                SetMainTexture(_renderer, _texture);
        }

        GeometryInstance3D ResolveRenderer()
        {
            if (renderer != null && IsInstanceValid(renderer))
                return renderer;

            return GetParent() as GeometryInstance3D;
        }

        // [Godot] Equivalent of renderer.sharedMaterial.mainTexture. Uses (in order) the material override, the
        // first surface material of a MeshInstance3D, or creates an unshaded transparent StandardMaterial3D.
        // BaseMaterial3D -> albedo_texture; ShaderMaterial -> shader parameter "texture_albedo".
        static void SetMainTexture(GeometryInstance3D target, Texture2D tex)
        {
            Material mat = target.MaterialOverride;
            MeshInstance3D mi = target as MeshInstance3D;
            if (mat == null && mi != null && mi.Mesh != null && mi.Mesh.GetSurfaceCount() > 0)
                mat = mi.GetActiveMaterial(0);

            if (mat == null)
            {
                if (tex == null)
                    return;

                StandardMaterial3D sm = new StandardMaterial3D();
                sm.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                sm.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                target.MaterialOverride = sm;
                mat = sm;
            }

            if (mat is BaseMaterial3D)
                ((BaseMaterial3D)mat).AlbedoTexture = tex;
            else if (mat is ShaderMaterial)
                ((ShaderMaterial)mat).SetShaderParameter("texture_albedo", tex);
        }

        #region edit mode functions

        // [Godot] CaptureInEditMode() removed: it rendered a temporary Unity camera into the RenderTexture in edit
        // mode (GL.Clear, Camera.Render). There is no edit-mode rendering in Godot.

        public void ApplyModifiedProperties(bool sortingOrderChanged)
        {
            if (sortingOrderChanged)
            {
                if (!Engine.IsEditorHint())
                    SetSortingOrder(sortingOrder, true);
                else
                    EMRenderSupport.orderChanged = true;
            }
        }

        public void OnUpdateSource(object[] data)
        {
            if (!Engine.IsEditorHint())
                return;

            this.packageName = (string)data[0];
            this.packagePath = (string)data[1];
            this.componentName = (string)data[2];

            if ((bool)data[3])
                _captured = false;
        }

        public int EM_sortingOrder
        {
            get { return sortingOrder; }
        }

        public void EM_BeforeUpdate()
        {
            if (_renderer == null)
                _renderer = ResolveRenderer();
            if (_renderer != null && _texture != null)
                SetMainTexture(_renderer, _texture);

            // [Godot] CaptureInEditMode() removed (see above).
        }

        public void EM_Update(UpdateContext context)
        {
            // [Godot] Unity: _renderer.sortingOrder = context.renderingOrder++. Godot 3D has no sorting order for
            // renderers; the rendering order slot is still consumed to keep the numbering identical.
            context.renderingOrder++;
        }

        public void EM_Reload()
        {
            _captured = false;
        }

        #endregion
    }
}
