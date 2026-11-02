// Ported from FairyGUI-unity: Assets/Scripts/Core/DisplayObject.cs
using System;
using System.Text;
using Godot;
using FairyGUI.Utils;
using System.Collections.Generic;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    // [Godot] Each DisplayObject owns a DisplayObjectNode (Node2D) in `node`; it replaces Unity's gameObject and
    // cachedTransform. x/y/pivot/rotation/scale/skew are composed into one Transform2D assigned to node.Transform.
    // FairyGUI's coordinate convention (y down, clockwise degrees) is the same as Godot 2D, so there is no y flip
    // and no -rotation anywhere in this file.
    public class DisplayObject : EventDispatcher, IBatchable
    {
        /// <summary>
        ///
        /// </summary>
        public string name;

        /// <summary>
        ///
        /// </summary>
        public Container parent { get; private set; }

        /// <summary>
        /// [Godot] Replaces gameObject and cachedTransform.
        /// </summary>
        public DisplayObjectNode node { get; protected set; }

        /// <summary>
        ///
        /// </summary>
        public NGraphics graphics { get; protected set; }

        /// <summary>
        ///
        /// </summary>
        public NGraphics paintingGraphics { get; protected set; }

        /// <summary>
        ///
        /// </summary>
        public event Action onPaint;

        /// <summary>
        ///
        /// </summary>
        public GObject gOwner;

        /// <summary>
        ///
        /// </summary>
        public uint id;

        bool _visible;
        bool _touchable;
        Vector2 _pivot;
        Vector3 _pivotOffset;
        Vector3 _rotation; //由于万向锁，单独旋转一个轴是会影响到其他轴的，所以这里需要单独保存
        Vector2 _skew;
        int _renderingOrder;
        float _alpha;
        bool _grayed;
        BlendMode _blendMode;
        IFilter _filter;
        Node _home;
        string _cursor;
        bool _perspective;
        int _focalLength;
        Vector3 _pixelPerfectAdjustment;
        int _checkPixelPerfect;

        // [Godot] Unity kept these in cachedTransform.localPosition/localScale. _position is in FairyGUI
        // coordinates (no y flip); _rotation.z is the FairyGUI rotation (clockwise degrees, no negation).
        Vector3 _position;
        Vector2 _scale;
        Transform2D _appliedTransform;
        bool _ownsAttachedNode; //SetGameObject wrapped a foreign Node2D with a node created by us
        bool _selfModulateAlpha; //alpha is currently written to node.SelfModulate (active mask)
        Node2D _paintingHost; //CanvasGroup host inserted between node and its parent in painting mode
        bool _paintingHostActive;
        internal int _layer;

        EventListener _onClick;
        EventListener _onRightClick;
        EventListener _onTouchBegin;
        EventListener _onTouchMove;
        EventListener _onTouchEnd;
        EventListener _onRollOver;
        EventListener _onRollOut;
        EventListener _onMouseWheel;
        EventListener _onAddedToStage;
        EventListener _onRemovedFromStage;
        EventListener _onKeyDown;
        EventListener _onClickLink;
        EventListener _onFocusIn;
        EventListener _onFocusOut;

        protected internal int _paintingMode; //1-滤镜，2-blendMode，4-transformMatrix, 8-cacheAsBitmap
        protected internal PaintingInfo _paintingInfo;
        protected Rect _contentRect;
        protected internal Flags _flags;

        internal static uint _gInstanceCounter;

        public DisplayObject()
        {
            id = _gInstanceCounter++;

            _alpha = 1;
            _visible = true;
            _touchable = true;
            _blendMode = BlendMode.Normal;
            _focalLength = 2000;
            _flags |= Flags.OutlineChanged;
            if (UIConfig.makePixelPerfect)
                _flags |= Flags.PixelPerfect;

            _scale = Vector2.One;
            _appliedTransform = Transform2D.Identity;
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onClick
        {
            get { return _onClick ?? (_onClick = new EventListener(this, "onClick")); }
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onRightClick
        {
            get { return _onRightClick ?? (_onRightClick = new EventListener(this, "onRightClick")); }
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onTouchBegin
        {
            get { return _onTouchBegin ?? (_onTouchBegin = new EventListener(this, "onTouchBegin")); }
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onTouchMove
        {
            get { return _onTouchMove ?? (_onTouchMove = new EventListener(this, "onTouchMove")); }
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onTouchEnd
        {
            get { return _onTouchEnd ?? (_onTouchEnd = new EventListener(this, "onTouchEnd")); }
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onRollOver
        {
            get { return _onRollOver ?? (_onRollOver = new EventListener(this, "onRollOver")); }
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onRollOut
        {
            get { return _onRollOut ?? (_onRollOut = new EventListener(this, "onRollOut")); }
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onMouseWheel
        {
            get { return _onMouseWheel ?? (_onMouseWheel = new EventListener(this, "onMouseWheel")); }
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onAddedToStage
        {
            get { return _onAddedToStage ?? (_onAddedToStage = new EventListener(this, "onAddedToStage")); }
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onRemovedFromStage
        {
            get { return _onRemovedFromStage ?? (_onRemovedFromStage = new EventListener(this, "onRemovedFromStage")); }
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onKeyDown
        {
            get { return _onKeyDown ?? (_onKeyDown = new EventListener(this, "onKeyDown")); }
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onClickLink
        {
            get { return _onClickLink ?? (_onClickLink = new EventListener(this, "onClickLink")); }
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onFocusIn
        {
            get { return _onFocusIn ?? (_onFocusIn = new EventListener(this, "onFocusIn")); }
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onFocusOut
        {
            get { return _onFocusOut ?? (_onFocusOut = new EventListener(this, "onFocusOut")); }

        }

        protected void CreateGameObject(string gameObjectName)
        {
            // [Godot] Creates the DisplayObjectNode. Like the inactive Unity GameObject, it starts hidden and
            // becomes visible when it is added to a container (or, for a root, in Update).
            node = new DisplayObjectNode { owner = this, Name = DisplayObjectNode.ValidateName(gameObjectName) };
            node.Visible = false;
        }

        protected void SetGameObject(Node2D gameObject)
        {
            // [Godot] Attaches this object to an existing node (Unity: an existing GameObject). A DisplayObjectNode
            // is adopted directly. Any other Node2D gets a new DisplayObjectNode child, so that _Draw can be
            // forwarded and the user's own transform is left untouched. FairyGUI never reparents or frees the
            // user's node (Flags.UserGameObject).
            DisplayObjectNode target = gameObject as DisplayObjectNode;
            if (target != null)
            {
                if (target.owner != null && target.owner != this)
                    GD.PushWarning("FairyGUI: the DisplayObjectNode is already owned by another DisplayObject.");
                target.owner = this;
                this.node = target;
                _appliedTransform = target.Transform;
                _position = new Vector3(_appliedTransform.Origin.X, _appliedTransform.Origin.Y, 0);
                _scale = _appliedTransform.Scale;
                _rotation = new Vector3(0, 0, _appliedTransform.Rotation * UMath.Rad2Deg);
            }
            else
            {
                this.node = new DisplayObjectNode { owner = this, Name = DisplayObjectNode.ValidateName(gameObject.Name) };
                gameObject.AddChild(this.node);
                _ownsAttachedNode = true;
            }

            _flags |= Flags.UserGameObject;
        }

        protected void DestroyGameObject()
        {
            if ((_flags & Flags.UserGameObject) == 0 && node != null)
            {
                FreeNode(node);
                node = null;
            }
            else if (_ownsAttachedNode && node != null)
            {
                // [Godot] the wrapper node created by SetGameObject is ours, the node it is attached to is not.
                FreeNode(node);
                node = null;
                _ownsAttachedNode = false;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public float alpha
        {
            get { return _alpha; }
            set
            {
                _alpha = value;
                // [Godot] alpha goes to node.Modulate.a; Godot multiplies Modulate down the subtree, which
                // replaces the alpha accumulation of UpdateContext for rendering.
                ApplyAlpha();
            }
        }

        /// <summary>
        ///
        /// </summary>
        public bool grayed
        {
            get { return _grayed; }
            set { _grayed = value; }
        }

        /// <summary>
        ///
        /// </summary>
        public bool visible
        {
            get { return _visible; }
            set
            {
                if (_visible != value)
                {
                    _visible = value;
                    _flags |= Flags.OutlineChanged;
                    if (parent != null && _visible)
                    {
                        SetNodeActive(true);
                        InvalidateBatchingState();
                        if (this is Container)
                            ((Container)this).InvalidateBatchingState(true);
                    }
                    else if (parent == null && _visible && (_flags & Flags.UserGameObject) == 0 && !IsParkedAtHome())
                    {
                        // [Godot] A root object (e.g. the Stage, a registered panel root) is shown again.
                        // Unity's code deactivated it here, which only worked because nothing hides the Stage.
                        SetNodeActive(true);
                    }
                    else
                        SetNodeActive(false);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public float x
        {
            get { return _position.X; }
            set
            {
                SetPosition(value, _position.Y, _position.Z);
            }
        }

        /// <summary>
        ///
        /// </summary>
        public float y
        {
            get { return _position.Y; }
            set
            {
                SetPosition(_position.X, value, _position.Z);
            }
        }

        /// <summary>
        ///
        /// </summary>
        public float z
        {
            get { return _position.Z; }
            set
            {
                // [Godot] z has no visual effect in Godot 2D (no depth/perspective); it is only stored.
                SetPosition(_position.X, _position.Y, value);
            }
        }

        /// <summary>
        ///
        /// </summary>
        public Vector2 xy
        {
            get { return new Vector2(this.x, this.y); }
            set { SetPosition(value.X, value.Y, _position.Z); }
        }

        /// <summary>
        ///
        /// </summary>
        public Vector3 position
        {
            get { return new Vector3(this.x, this.y, this.z); }
            set { SetPosition(value.X, value.Y, value.Z); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="xv"></param>
        /// <param name="yv"></param>
        public void SetXY(float xv, float yv)
        {
            SetPosition(xv, yv, _position.Z);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="xv"></param>
        /// <param name="yv"></param>
        /// <param name="zv"></param>
        public void SetPosition(float xv, float yv, float zv)
        {
            Vector3 v = new Vector3();
            v.X = xv;
            v.Y = yv; // [Godot] no y flip
            v.Z = zv;
            if (v != _position)
            {
                _position = v;
                ApplyTransform();
                _flags |= Flags.OutlineChanged;
                if ((_flags & Flags.PixelPerfect) != 0)
                {
                    //总在下一帧再完成PixelPerfect，这样当物体在连续运动时，不会因为PixelPerfect而发生抖动。
                    _checkPixelPerfect = FrameTime.frameCount;
                    _pixelPerfectAdjustment = Vector3.Zero;
                }
            }
        }

        /// <summary>
        /// If the object position is align by pixel
        /// </summary>
        public bool pixelPerfect
        {
            get { return (_flags & Flags.PixelPerfect) != 0; }
            set
            {
                if (value)
                    _flags |= Flags.PixelPerfect;
                else
                    _flags &= ~Flags.PixelPerfect;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public float width
        {
            get
            {
                EnsureSizeCorrect();
                return _contentRect.width;
            }
            set
            {
                if (!UMath.Approximately(value, _contentRect.width))
                {
                    _contentRect.width = value;
                    _flags |= Flags.WidthChanged;
                    _flags &= ~Flags.HeightChanged;
                    OnSizeChanged();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public float height
        {
            get
            {
                EnsureSizeCorrect();
                return _contentRect.height;
            }
            set
            {
                if (!UMath.Approximately(value, _contentRect.height))
                {
                    _contentRect.height = value;
                    _flags &= ~Flags.WidthChanged;
                    _flags |= Flags.HeightChanged;
                    OnSizeChanged();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public Vector2 size
        {
            get
            {
                EnsureSizeCorrect();
                return _contentRect.size;
            }
            set
            {
                SetSize(value.X, value.Y);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="wv"></param>
        /// <param name="hv"></param>
        public void SetSize(float wv, float hv)
        {
            if (!UMath.Approximately(wv, _contentRect.width))
                _flags |= Flags.WidthChanged;
            else
                _flags &= ~Flags.WidthChanged;
            if (!UMath.Approximately(hv, _contentRect.height))
                _flags |= Flags.HeightChanged;
            else
                _flags &= ~Flags.HeightChanged;

            if ((_flags & Flags.WidthChanged) != 0 || (_flags & Flags.HeightChanged) != 0)
            {
                _contentRect.width = wv;
                _contentRect.height = hv;
                OnSizeChanged();
            }
        }

        virtual public void EnsureSizeCorrect()
        {
        }

        virtual protected void OnSizeChanged()
        {
            ApplyPivot();

            if (_paintingInfo != null)
                _paintingInfo.flag = 1;
            if (graphics != null)
                graphics.contentRect = _contentRect;
            _flags |= Flags.OutlineChanged;
        }

        /// <summary>
        ///
        /// </summary>
        public float scaleX
        {
            get { return _scale.X; }
            set
            {
                Vector2 v = _scale;
                v.X = ValidateScale(value);
                _scale = v;
                _flags |= Flags.OutlineChanged;
                ApplyPivot();
            }
        }

        /// <summary>
        ///
        /// </summary>
        public float scaleY
        {
            get { return _scale.Y; }
            set
            {
                Vector2 v = _scale;
                v.Y = ValidateScale(value);
                _scale = v;
                _flags |= Flags.OutlineChanged;
                ApplyPivot();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="xv"></param>
        /// <param name="yv"></param>
        public void SetScale(float xv, float yv)
        {
            Vector2 v = new Vector2();
            v.X = ValidateScale(xv);
            v.Y = ValidateScale(yv);
            _scale = v;
            _flags |= Flags.OutlineChanged;
            ApplyPivot();
        }

        /// <summary>
        /// 在scale过小情况（极端情况=0），当使用Transform的坐标变换时，变换到世界，再从世界变换到本地，会由于精度问题造成结果错误。
        /// 这种错误会导致Batching错误，因为Batching会使用缓存的outline。
        /// 这里限制一下scale的最小值作为当前解决方案。
        /// 这个方案并不完美，因为限制了本地scale值并不能保证对世界scale不会过小。
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private float ValidateScale(float value)
        {
            if (value >= 0 && value < 0.001f)
                value = 0.001f;
            else if (value < 0 && value > -0.001f)
                value = -0.001f;
            return value;
        }

        /// <summary>
        ///
        /// </summary>
        public Vector2 scale
        {
            get { return _scale; }
            set
            {
                SetScale(value.X, value.Y);
            }
        }

        /// <summary>
        ///
        /// </summary>
        public float rotation
        {
            get
            {
                //和Unity默认的旋转方向相反
                // [Godot] Godot rotates clockwise like FairyGUI, so the value is stored as is.
                return _rotation.Z;
            }
            set
            {
                _rotation.Z = value;
                _flags |= Flags.OutlineChanged;
                if (_perspective)
                    UpdateTransformMatrix();
                else
                    ApplyPivot();
            }
        }

        /// <summary>
        ///
        /// </summary>
        public float rotationX
        {
            get
            {
                return _rotation.X;
            }
            set
            {
                // [Godot] approximation: no 3D in Godot 2D, rotationX/rotationY are applied as the orthographic
                // projection of the rotation (y shrinks by cos(rotationX), x by cos(rotationY)) around the pivot.
                _rotation.X = value;
                _flags |= Flags.OutlineChanged;
                if (_perspective)
                    UpdateTransformMatrix();
                else
                    ApplyPivot();
            }
        }

        /// <summary>
        ///
        /// </summary>
        public float rotationY
        {
            get
            {
                return _rotation.Y;
            }
            set
            {
                // [Godot] approximation, see rotationX.
                _rotation.Y = value;
                _flags |= Flags.OutlineChanged;
                if (_perspective)
                    UpdateTransformMatrix();
                else
                    ApplyPivot();
            }
        }

        /// <summary>
        ///
        /// </summary>
        public Vector2 skew
        {
            get { return _skew; }
            set
            {
                _skew = value;
                _flags |= Flags.OutlineChanged;

                if (Engine.IsEditorHint()) //编辑期间不支持！！
                    return;

                UpdateTransformMatrix();
            }
        }

        /// <summary>
        /// 当对象处于ScreenSpace，也就是使用正交相机渲染时，对象虽然可以绕X轴或者Y轴旋转，但没有透视效果。设置perspective，可以模拟出透视效果。
        /// </summary>
        // [Godot] approximation: Godot 2D has no perspective divide, so perspective only changes the order in which
        // rotation and scale are composed (like Unity's vertex matrix); rotationX/Y stay an affine approximation.
        public bool perspective
        {
            get
            {
                return _perspective;
            }
            set
            {
                if (_perspective != value)
                {
                    _perspective = value;
                    //屏蔽Unity自身的旋转变换
                    // [Godot] ComputeTransform reads _perspective to pick the composition.

                    ApplyPivot();
                    UpdateTransformMatrix();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public int focalLength
        {
            get { return _focalLength; }
            set
            {
                if (value <= 0)
                    value = 1;

                // [Godot] stored only: there is no perspective projection to apply it to.
                _focalLength = value;
                if (_perspective)
                    UpdateTransformMatrix();
            }
        }

        void UpdateTransformMatrix()
        {
            // [Godot] Unity built a vertex matrix (skew + perspective rotation) for the mesh and, for containers,
            // switched to painting mode 4 to apply it. Here skew and rotation are part of node.Transform, which
            // applies to the whole subtree, so no vertex matrix and no painting mode are needed.
            ApplyTransform();

            if (_paintingMode > 0)
                _paintingInfo.flag = 1;

            _flags |= Flags.OutlineChanged;
        }

        /// <summary>
        ///
        /// </summary>
        public Vector2 pivot
        {
            get { return _pivot; }
            set
            {
                // [Godot] no y flip
                Vector3 deltaPivot = new Vector3((value.X - _pivot.X) * _contentRect.width, (value.Y - _pivot.Y) * _contentRect.height, 0);
                Vector3 oldOffset = _pivotOffset;

                _pivot = value;
                UpdatePivotOffset();
                Vector3 v = _position;
                v += oldOffset - _pivotOffset + deltaPivot;
                _position = v;
                ApplyTransform();
                _flags |= Flags.OutlineChanged;
            }
        }

        void UpdatePivotOffset()
        {
            float px = _pivot.X * _contentRect.width;
            float py = _pivot.Y * _contentRect.height;

            //注意这里不用处理skew，因为在顶点变换里有对pivot的处理
            // [Godot] _pivotOffset = (rotation * scale) applied to the pivot, in FairyGUI coordinates (y down).
            // In perspective mode the rotation belongs to the vertex matrix in Unity, so only scale is used.
            float m00, m01, m10, m11, m20, m21;
            if (_perspective)
            {
                m00 = 1; m01 = 0; m10 = 0; m11 = 1; m20 = 0; m21 = 0;
            }
            else
                GetRotationMatrix(out m00, out m01, out m10, out m11, out m20, out m21);

            float sx = px * _scale.X;
            float sy = py * _scale.Y;
            _pivotOffset = new Vector3(m00 * sx + m01 * sy, m10 * sx + m11 * sy, m20 * sx + m21 * sy);
        }

        void ApplyPivot()
        {
            if (_pivot.X != 0 || _pivot.Y != 0)
            {
                Vector3 oldOffset = _pivotOffset;

                UpdatePivotOffset();
                Vector3 v = _position;

                if ((_flags & Flags.PixelPerfect) != 0)
                {
                    v -= _pixelPerfectAdjustment;
                    _checkPixelPerfect = FrameTime.frameCount;
                    _pixelPerfectAdjustment = Vector3.Zero;
                }

                v += oldOffset - _pivotOffset;
                _position = v;
                _flags |= Flags.OutlineChanged;
            }

            // [Godot] rotation/scale/size changes reach the node here (Unity changed the Transform directly).
            ApplyTransform();
        }

        /// <summary>
        /// This is the pivot position
        /// </summary>
        public Vector3 location
        {
            get
            {
                Vector3 pos = this.position;
                pos.X += _pivotOffset.X;
                pos.Y += _pivotOffset.Y; // [Godot] no y flip
                pos.Z += _pivotOffset.Z;
                return pos;
            }

            set
            {
                this.SetPosition(value.X - _pivotOffset.X, value.Y - _pivotOffset.Y, value.Z - _pivotOffset.Z);
            }
        }

        /// <summary>
        ///
        /// </summary>
        public Material material
        {
            get
            {
                if (graphics != null)
                    return graphics.material;
                else
                    return null;
            }
            set
            {
                if (graphics != null)
                    graphics.material = value;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public string shader
        {
            get
            {
                if (graphics != null)
                    return graphics.shader;
                else
                    return null;
            }
            set
            {
                if (graphics != null)
                    graphics.shader = value;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public int renderingOrder
        {
            get
            {
                return _renderingOrder;
            }
        }

        virtual public void SetRenderingOrder(UpdateContext context, bool inBatch)
        {
            // [Godot] The rendering order is bookkeeping only: Godot draws canvas items in scene tree order,
            // which Container keeps equal to the display list order.
            if ((_flags & Flags.GameObjectDisposed) != 0)
            {
                DisplayDisposedWarning();
                return;
            }

            _renderingOrder = context.renderingOrder;
            if (graphics != null)
                graphics.SetRenderingOrder(context, inBatch);
            else
                context.renderingOrder++;

            if (_paintingMode > 0)
                paintingGraphics.renderingOrder = _renderingOrder;
        }

        /// <summary>
        ///
        /// </summary>
        // [Godot] Godot 2D has no camera culling layers; the layer value is stored and propagated like in Unity
        // but does not affect rendering.
        public int layer
        {
            get
            {
                return _layer;
            }
            set
            {
                SetLayer(value, false);
            }
        }

        /// <summary>
        /// If the object can be focused?
        /// </summary>
        public bool focusable
        {
            get { return (_flags & Flags.NotFocusable) == 0; }
            set
            {
                if (value)
                    _flags &= ~Flags.NotFocusable;
                else
                    _flags |= Flags.NotFocusable;
            }
        }

        /// <summary>
        /// If the object can be navigated by TAB?
        /// </summary>
        public bool tabStop
        {
            get { return (_flags & Flags.TabStop) != 0; }
            set
            {
                if (value)
                    _flags |= Flags.TabStop;
                else
                    _flags &= ~Flags.TabStop;
            }
        }

        /// <summary>
        /// If the object focused?
        /// </summary>
        public bool focused
        {
            get
            {
                return Stage.inst.focus == this || (this is Container) && ((Container)this).IsAncestorOf(Stage.inst.focus);
            }
        }

        internal bool _AcceptTab()
        {
            if (_touchable && _visible
                && ((_flags & Flags.TabStop) != 0 || (_flags & Flags.TabStopChildren) != 0)
                && (_flags & Flags.NotFocusable) == 0)
            {
                Stage.inst.SetFocus(this, true);
                return true;
            }
            else
                return false;
        }

        /// <summary>
        ///
        /// </summary>
        /// <value></value>
        public string cursor
        {
            get { return _cursor; }
            set
            {
                _cursor = value;
                if (!Engine.IsEditorHint()
                    && (this == Stage.inst.touchTarget || (this is Container) && ((Container)this).IsAncestorOf(Stage.inst.touchTarget)))
                {
                    Stage.inst._ChangeCursor(_cursor);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public bool isDisposed
        {
            // [Godot] gameObject == null -> the node is gone or was freed
            get { return (_flags & Flags.Disposed) != 0 || node == null || !GodotObject.IsInstanceValid(node); }
        }

        internal void InternalSetParent(Container value)
        {
            if (parent != value)
            {
                if (value == null && (parent._flags & Flags.Disposed) != 0)
                    parent = value;
                else
                {
                    parent = value;
                    UpdateHierarchy();
                }
                _flags |= Flags.OutlineChanged;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public Container topmost
        {
            get
            {
                DisplayObject currentObject = this;
                while (currentObject.parent != null)
                    currentObject = currentObject.parent;
                // [Godot] A registered panel root (UIPanel/UIPainter) was a Stage child in Unity, so its content
                // is on stage: stage, onAddedToStage, click/rollOver/focus and touch monitors depend on this.
                Container root = currentObject as Container;
                if (root != null && !(root is Stage) && Stage.IsRegisteredPanel(root))
                    return Stage.inst;
                return root;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public Stage stage
        {
            get
            {
                return topmost as Stage;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public Container worldSpaceContainer
        {
            get
            {
                Container wsc = null;
                DisplayObject currentObject = this;
                while (currentObject.parent != null)
                {
                    if ((currentObject is Container) && ((Container)currentObject).renderMode == RenderMode.WorldSpace)
                    {
                        wsc = (Container)currentObject;
                        break;
                    }
                    currentObject = currentObject.parent;
                }

                return wsc;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public bool touchable
        {
            get { return _touchable; }
            set
            {
                if (_touchable != value)
                {
                    _touchable = value;
                    if (this is Container)
                    {
                        ColliderHitTest hitArea = ((Container)this).hitArea as ColliderHitTest;
                        if (hitArea != null)
                            hitArea.enabled = value; // [Godot] was hitArea.collider.enabled
                    }
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <value></value>
        public bool touchDisabled
        {
            get { return (_flags & Flags.TouchDisabled) != 0; }
        }

        /// <summary>
        /// 进入绘画模式，整个对象将画到一张RenderTexture上，然后这种贴图将代替原有的显示内容。
        /// 可以在onPaint回调里对这张纹理进行进一步操作，实现特殊效果。
        /// </summary>
        public void EnterPaintingMode()
        {
            EnterPaintingMode(16384, null, 1);
        }

        /// <summary>
        /// 进入绘画模式，整个对象将画到一张RenderTexture上，然后这种贴图将代替原有的显示内容。
        /// 可以在onPaint回调里对这张纹理进行进一步操作，实现特殊效果。
        /// 可能有多个地方要求进入绘画模式，这里用requestorId加以区别，取值是1、2、4、8、16以此类推。1024内内部保留。用户自定义的id从1024开始。
        /// </summary>
        /// <param name="requestId">请求者id</param>
        /// <param name="extend">纹理四周的留空。如果特殊处理后的内容大于原内容，那么这里的设置可以使纹理扩大。</param>
        public void EnterPaintingMode(int requestorId, Margin? extend)
        {
            EnterPaintingMode(requestorId, extend, 1);
        }

        /// <summary>
        /// 进入绘画模式，整个对象将画到一张RenderTexture上，然后这种贴图将代替原有的显示内容。
        /// 可以在onPaint回调里对这张纹理进行进一步操作，实现特殊效果。
        /// 可能有多个地方要求进入绘画模式，这里用requestorId加以区别，取值是1、2、4、8、16以此类推。1024内内部保留。用户自定义的id从1024开始。
        /// </summary>
        /// <param name="requestorId">请求者id</param>
        /// <param name="extend">扩展纹理。如果特殊处理后的内容大于原内容，那么这里的设置可以使纹理扩大。</param>
        /// <param name="scale">附加一个缩放系数</param>
        // [Godot] No RenderTexture/CaptureCamera. A host Node2D ("<name> (Painter)") is inserted between the node
        // and its parent and put in CanvasGroup mode (RenderingServer.CanvasItemSetCanvasGroupMode, Transparent):
        // the node's own drawing and all its children are composited in the backbuffer and drawn once with the
        // material of paintingGraphics (an NGraphics on the host), so filters can use paintingGraphics.ToggleKeyword /
        // SetShaderParameter. The host never draws by itself (no _Draw), so the group rect is fitted to the content
        // and grown by `extend`. Because the host is separate from `node`, the object's own graphics material does
        // not clash with the painting material. CanvasGroup and ClipChildren both use the backbuffer, so a
        // painting-mode object inside a mask (or a mask inside it) may not render correctly.
        public void EnterPaintingMode(int requestorId, Margin? extend, float scale)
        {
            bool first = _paintingMode == 0;
            _paintingMode |= requestorId;
            if (first)
            {
                if (_paintingInfo == null)
                {
                    _paintingInfo = new PaintingInfo()
                    {
                        captureDelegate = Capture,
                        scale = 1
                    };
                }

                if (paintingGraphics == null)
                {
                    // [Godot] Unity reused gameObject when graphics == null; here a separate host is always used.
                    _paintingHost = new Node2D();
                    _paintingHost.Name = DisplayObjectNode.ValidateName((node != null ? (string)node.Name : "DisplayObject") + " (Painter)");
                    paintingGraphics = new NGraphics(_paintingHost);
                }
                else
                    paintingGraphics.enabled = true;
                // [Godot] the painting material samples the CanvasGroup back buffer (needed by blendMode as well as filters)
                paintingGraphics.canvasGroup = true;

                WrapWithPaintingHost();

                if (this is Container)
                {
                    // [Godot] Unity moved the children to CaptureCamera.hiddenLayer; the CanvasGroup groups them in place.
                    ((Container)this).UpdateBatchingFlags();
                }
                else
                    this.InvalidateBatchingState();
            }
            if (extend != null)
                _paintingInfo.extend = (Margin)extend;
            _paintingInfo.scale = scale;
            _paintingInfo.flag = 1;

            RefreshPaintingGroup();
        }

        /// <summary>
        /// 离开绘画模式
        /// </summary>
        /// <param name="requestId"></param>
        public void LeavePaintingMode(int requestorId)
        {
            if (_paintingMode == 0 || (_flags & Flags.Disposed) != 0)
                return;

            _paintingMode ^= requestorId;
            if (_paintingMode == 0)
            {
                paintingGraphics.enabled = false;
                paintingGraphics.canvasGroup = false;
                UnwrapPaintingHost();

                if (this is Container)
                {
                    ((Container)this).UpdateBatchingFlags();
                }
                else
                    this.InvalidateBatchingState();
            }
        }

        /// <summary>
        ///
        /// </summary>
        public bool paintingMode
        {
            get { return _paintingMode > 0; }
        }

        /// <summary>
        /// 将整个显示对象（如果是容器，则容器包含的整个显示列表）静态化，所有内容被缓冲到一张纹理上。
        /// DC将保持为1。CPU消耗将降到最低。但对象的任何变化不会更新。
        /// 当cacheAsBitmap已经为true时，再次调用cacheAsBitmap=true将会刷新对象一次。
        /// </summary>
        // [Godot] approximation: the content is grouped with a CanvasGroup and the container stops updating its
        // children after the first capture (like Unity), but Godot still redraws the group every frame, so there
        // is no GPU saving and node property changes (position, visibility) still show.
        public bool cacheAsBitmap
        {
            get { return (_flags & Flags.CacheAsBitmap) != 0; }
            set
            {
                if (value)
                {
                    _flags |= Flags.CacheAsBitmap;
                    EnterPaintingMode(8, null, UIContentScaler.scaleFactor);
                }
                else
                {
                    _flags &= ~Flags.CacheAsBitmap;
                    LeavePaintingMode(8);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="extend"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        // [Godot] degraded: Unity rendered the object alone with CaptureCamera. Godot reads back the region of the
        // viewport texture covered by the object (last rendered frame), so anything drawn above or below it in that
        // region is included, and the object must already be in the scene tree and drawn once.
        public Texture2D GetScreenShot(Margin? extend, float scale)
        {
            EnterPaintingMode(8, null, scale);
            UpdatePainting();
            Capture();

            Texture2D output = null;
            Viewport viewport = (node != null && GodotObject.IsInstanceValid(node) && node.IsInsideTree()) ? node.GetViewport() : null;
            Godot.Image image = viewport != null ? viewport.GetTexture().GetImage() : null;
            if (image != null && !image.IsEmpty())
            {
                Rect rect = paintingGraphics.contentRect;
                Transform2D toPixels = viewport.GetFinalTransform() * GetWorldTransform();
                Rect2 pixelRect = toPixels * (Rect2)rect;
                int x0 = UMath.Clamp(UMath.FloorToInt(pixelRect.Position.X), 0, image.GetWidth());
                int y0 = UMath.Clamp(UMath.FloorToInt(pixelRect.Position.Y), 0, image.GetHeight());
                int x1 = UMath.Clamp(UMath.CeilToInt(pixelRect.End.X), 0, image.GetWidth());
                int y1 = UMath.Clamp(UMath.CeilToInt(pixelRect.End.Y), 0, image.GetHeight());
                if (x1 > x0 && y1 > y0)
                {
                    Godot.Image region = image.GetRegion(new Rect2I(x0, y0, x1 - x0, y1 - y0));
                    int textureWidth = UMath.RoundToInt(rect.width * scale);
                    int textureHeight = UMath.RoundToInt(rect.height * scale);
                    if (textureWidth > 0 && textureHeight > 0
                        && (textureWidth != region.GetWidth() || textureHeight != region.GetHeight()))
                        region.Resize(textureWidth, textureHeight);
                    output = ImageTexture.CreateFromImage(region);
                }
            }

            if (output == null)
                output = ImageTexture.CreateFromImage(Godot.Image.CreateEmpty(1, 1, false, Godot.Image.Format.Rgba8));

            LeavePaintingMode(8);

            return output;
        }

        /// <summary>
        ///
        /// </summary>
        public IFilter filter
        {
            get
            {
                return _filter;
            }

            set
            {
                if (Engine.IsEditorHint()) //编辑期间不支持！！
                    return;

                if (value == _filter)
                    return;

                if (_filter != null)
                    _filter.Dispose();

                if (value != null && value.target != null)
                    value.target.filter = null;

                _filter = value;
                if (_filter != null)
                    _filter.target = this;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public BlendMode blendMode
        {
            get { return _blendMode; }
            set
            {
                _blendMode = value;
                InvalidateBatchingState();

                if (graphics == null)
                {
                    if (_blendMode != BlendMode.Normal)
                    {
                        if (Engine.IsEditorHint()) //Not supported in edit mode！
                            return;

                        EnterPaintingMode(2, null);
                        paintingGraphics.blendMode = _blendMode;
                    }
                    else
                        LeavePaintingMode(2);
                }
                else
                    graphics.blendMode = _blendMode;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="targetSpace"></param>
        /// <returns></returns>
        virtual public Rect GetBounds(DisplayObject targetSpace)
        {
            EnsureSizeCorrect();

            if (targetSpace == this) // optimization
            {
                return _contentRect;
            }
            else if (targetSpace == parent && _rotation.Z == 0)
            {
                return new Rect(_position.X, _position.Y,
                    _contentRect.width * _scale.X, _contentRect.height * _scale.Y);
            }
            else
                return TransformRect(_contentRect, targetSpace);
        }

        internal DisplayObject InternalHitTest()
        {
            if (_visible && (!HitTestContext.forTouch || _touchable))
                return HitTest();
            else
                return null;
        }

        internal DisplayObject InternalHitTestMask()
        {
            if (_visible)
                return HitTest();
            else
                return null;
        }

        virtual protected DisplayObject HitTest()
        {
            Rect rect = GetBounds(this);
            if (rect.width == 0 || rect.height == 0)
                return null;

            Vector2 localPoint = WorldToLocal(HitTestContext.worldPoint, HitTestContext.direction).ToV2();
            if (rect.Contains(localPoint))
                return this;
            else
                return null;
        }

        /// <summary>
        /// 将舞台坐标转换为本地坐标
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public Vector2 GlobalToLocal(Vector2 point)
        {
            Container wsc = this.worldSpaceContainer;

            if (wsc != null && wsc.hitArea is MeshColliderHitTest) //I am in a world space
            {
                // [Godot] UIPainter (3D surface): stage coordinates cannot be mapped onto the surface.
                // Unity returned NaN here as well when the ray missed the mesh.
                return new Vector2(float.NaN, float.NaN);
            }
            else //I am in stage space
            {
                // [Godot] Stage space -> canvas global (world) space -> local space. Camera2D/CanvasLayer transforms
                // are part of GetGlobalTransformWithCanvas, so world-space panels (UIPanel) need no camera code.
                Vector3 worldPoint = Stage.inst.LocalToWorld(new Vector3(point.X, point.Y, 0));
                return this.WorldToLocal(worldPoint, UVec.back).ToV2();
            }
        }

        /// <summary>
        /// 将本地坐标转换为舞台坐标
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public Vector2 LocalToGlobal(Vector2 point)
        {
            Container wsc = this.worldSpaceContainer;

            Vector3 worldPoint = this.LocalToWorld(new Vector3(point.X, point.Y, 0));
            if (wsc != null && wsc.hitArea is MeshColliderHitTest) //Not supported for UIPainter, use TransfromPoint instead.
                return new Vector2(float.NaN, float.NaN);

            // [Godot] world (canvas global) -> stage space, see GlobalToLocal.
            return Stage.inst.WorldToLocal(worldPoint, UVec.back).ToV2();
        }

        /// <summary>
        /// 转换世界坐标点到等效的本地xy平面的点。等效的意思是他们在屏幕方向看到的位置一样。
        /// 返回的点是在对象的本地坐标空间，且z=0
        /// </summary>
        /// <param name="worldPoint"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        // [Godot] "World" is the canvas global space (viewport coordinates for objects under the Stage).
        // Godot 2D has no z, so direction is ignored and the inverse of node.GetGlobalTransformWithCanvas()
        // is applied directly (skew and the rotationX/Y approximation are part of that transform).
        public Vector3 WorldToLocal(Vector3 worldPoint, Vector3 direction)
        {
            Transform2D inv = SafeAffineInverse(GetWorldTransform());
            Vector2 localPoint = inv * new Vector2(worldPoint.X, worldPoint.Y);
            return new Vector3(localPoint.X, localPoint.Y, 0);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="localPoint"></param>
        /// <returns></returns>
        public Vector3 LocalToWorld(Vector3 localPoint)
        {
            // [Godot] no y flip, no vertex matrix: skew/perspective are part of the node transform.
            Vector2 worldPoint = GetWorldTransform() * new Vector2(localPoint.X, localPoint.Y);
            return new Vector3(worldPoint.X, worldPoint.Y, 0);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="point"></param>
        /// <param name="targetSpace">null if to world space</param>
        /// <returns></returns>
        public Vector2 TransformPoint(Vector2 point, DisplayObject targetSpace)
        {
            if (targetSpace == this)
                return point;

            point = LocalToWorld(point.ToV3()).ToV2();
            if (targetSpace != null)
                point = targetSpace.WorldToLocal(point.ToV3(), UVec.back).ToV2();

            return point;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="targetSpace">null if to world space</param>
        /// <returns></returns>
        public Rect TransformRect(Rect rect, DisplayObject targetSpace)
        {
            if (targetSpace == this)
                return rect;

            if (targetSpace == parent && _rotation.Z == 0) // optimization
            {
                Vector2 vec = _scale;
                return new Rect((this.x + rect.x) * vec.X, (this.y + rect.y) * vec.Y,
                    rect.width * vec.X, rect.height * vec.Y);
            }
            else
            {
                Vector4 vec4 = new Vector4(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

                TransformRectPoint(rect.xMin, rect.yMin, targetSpace, ref vec4);
                TransformRectPoint(rect.xMax, rect.yMin, targetSpace, ref vec4);
                TransformRectPoint(rect.xMin, rect.yMax, targetSpace, ref vec4);
                TransformRectPoint(rect.xMax, rect.yMax, targetSpace, ref vec4);

                return Rect.MinMaxRect(vec4.X, vec4.Y, vec4.Z, vec4.W);
            }
        }

        protected void TransformRectPoint(float px, float py, DisplayObject targetSpace, ref Vector4 vec4)
        {
            Vector2 v = TransformPoint(new Vector2(px, py), targetSpace);

            if (vec4.X > v.X) vec4.X = v.X;
            if (vec4.Z < v.X) vec4.Z = v.X;
            if (vec4.Y > v.Y) vec4.Y = v.Y;
            if (vec4.W < v.Y) vec4.W = v.Y;
        }

        /// <summary>
        ///
        /// </summary>
        public void RemoveFromParent()
        {
            if (parent != null)
                parent.RemoveChild(this);
        }

        /// <summary>
        ///
        /// </summary>
        public void InvalidateBatchingState()
        {
            if (parent != null)
                parent.InvalidateBatchingState(true);
        }

        virtual public void Update(UpdateContext context)
        {
            if (_checkPixelPerfect != 0)
            {
                if (_rotation == Vector3.Zero)
                {
                    Vector3 v = _position;
                    v.X = UMath.Round(v.X);
                    v.Y = UMath.Round(v.Y);
                    _pixelPerfectAdjustment = v - _position;
                    if (_pixelPerfectAdjustment != Vector3.Zero)
                    {
                        _position = v;
                        ApplyTransform();
                    }
                }
                _checkPixelPerfect = 0;
            }

            // [Godot] A root (Stage, panel root) has no parent to activate its node; Unity's Stage did
            // gameObject.SetActive(true) itself. Keep a visible root's node visible.
            if (parent == null && _visible && node != null
                && (_flags & (Flags.UserGameObject | Flags.Disposed | Flags.GameObjectDisposed)) == 0
                && !node.Visible && !IsParkedAtHome())
                SetNodeActive(true);

            // [Godot] alpha is not baked into vertices (node.Modulate carries it); grayed still selects the material.
            if (graphics != null)
                graphics.Update(context, context.alpha * _alpha, context.grayed | _grayed);

            if (_paintingMode != 0)
            {
                UpdatePainting();

                //如果是容器，Capture要等到Container.Update的最后执行，因为容器中可能也有需要Capture的内容，要等他们完成后再进行容器的Capture。
                if (!(this is Container))
                {
                    if ((_flags & Flags.CacheAsBitmap) == 0 || _paintingInfo.flag != 2)
                        UpdateContext.OnEnd += _paintingInfo.captureDelegate;
                }

                paintingGraphics.Update(context, 1, false);
            }

            if (_filter != null)
                _filter.Update();

            Stats.ObjectCount++;
        }

        void UpdatePainting()
        {
            NTexture paintingTexture = paintingGraphics.texture;
            if (paintingTexture != null && paintingTexture.disposed) //Texture可能已被Stage.MonitorTexture销毁
            {
                paintingTexture = null;
                _paintingInfo.flag = 1;
            }

            if (_paintingInfo.flag == 1)
            {
                _paintingInfo.flag = 0;

                //从优化考虑，决定使用绘画模式的容器都需要明确指定大小，而不是自动计算包围。这在UI使用上并没有问题，因为组件总是有固定大小的
                Margin extend = _paintingInfo.extend;
                paintingGraphics.contentRect = new Rect(-extend.left, -extend.top, _contentRect.width + extend.left + extend.right, _contentRect.height + extend.top + extend.bottom);
                // [Godot] No RenderTexture is created: the CanvasGroup renders the live content into the backbuffer
                // every frame. paintingGraphics.texture stays null; only the group margins are refreshed.
                RefreshPaintingGroup();
            }

            if (paintingTexture != null)
                paintingTexture.lastActive = FrameTime.time;
        }

        void Capture()
        {
            // [Godot] Unity rendered the object into paintingGraphics.texture with CaptureCamera here. The CanvasGroup
            // composites the live content every frame, so there is nothing to capture; the state machine (flag = 2,
            // used by cacheAsBitmap) and the onPaint callback are kept.
            _paintingInfo.flag = 2; //2表示已完成一次Capture
            if (onPaint != null)
                onPaint();
        }

        /// <summary>
        /// 为对象设置一个默认的父Transform。当对象不在显示列表里时，它的GameObject挂到哪里。
        /// </summary>
        // [Godot] type changed from Transform to Node.
        public Node home
        {
            get { return _home; }
            set
            {
                _home = value;
                Node outer = GetOuterNode();
                if (value != null && outer != null && outer.GetParent() == null)
                    value.AddChild(outer);
            }
        }

        void UpdateHierarchy()
        {
            if ((_flags & Flags.GameObjectDisposed) != 0)
                return;

            if ((_flags & Flags.UserGameObject) != 0)
            {
                //we dont change transform parent of this object
                if (node != null)
                {
                    if (parent != null && visible)
                        SetNodeActive(true);
                    else
                        SetNodeActive(false);
                }
            }
            else if (parent != null)
            {
                // [Godot] The parent container puts the node (or its painting host) under its own node, at the
                // position that matches the display list order.
                parent.AttachChildNode(this);

                SetNodeActive(_visible);

                // [Godot] Unity used CaptureCamera.hiddenLayer when the parent was in painting mode; the CanvasGroup
                // keeps the content in place, so the parent's layer is inherited as is.
                int layerValue = parent._layer;
                SetLayer(layerValue, true);
            }
            else if ((_flags & Flags.Disposed) == 0 && this.node != null && !StageEngine.beingQuit)
            {
                // [Godot] Godot allows orphan nodes, so leaving the display list only detaches the node.
                Node outer = GetOuterNode();
                Node oldParent = outer.GetParent();
                if (oldParent != null && oldParent != _home)
                    oldParent.RemoveChild(outer);

                if (!Engine.IsEditorHint())
                {
                    if (gOwner == null || gOwner.parent == null)//如果gOwner还有parent的话，说明只是暂时的隐藏
                    {
                        if (_home != null && GodotObject.IsInstanceValid(_home) && outer.GetParent() == null)
                            _home.AddChild(outer);
                        // [Godot] _home == null: the node stays an orphan (Unity: DontDestroyOnLoad).
                    }
                }

                SetNodeActive(false);
            }
        }

        virtual protected bool SetLayer(int value, bool fromParent)
        {
            if ((_flags & Flags.LayerSet) != 0) //setted
            {
                if (fromParent)
                    return false;
            }
            else if ((_flags & Flags.LayerFromParent) != 0) //inherit from parent
            {
                if (!fromParent)
                    _flags |= Flags.LayerSet;
            }
            else
            {
                if (fromParent)
                    _flags |= Flags.LayerFromParent;
                else
                    _flags |= Flags.LayerSet;
            }

            // [Godot] Unity wrote the layer of paintingGraphics.gameObject in painting mode (the content itself was on
            // CaptureCamera.hiddenLayer). There is a single layer value here.
            if (_layer != value)
            {
                _SetLayerDirect(value);

                if (this is Container)
                {
                    int cnt = ((Container)this).numChildren;
                    for (int i = 0; i < cnt; i++)
                    {
                        DisplayObject child = ((Container)this).GetChildAt(i);
                        child.SetLayer(value, true);
                    }
                }
            }

            return true;
        }

        virtual internal void _SetLayerDirect(int value)
        {
            // [Godot] informational only (no camera culling layers); NGraphics sub instances have no layer.
            _layer = value;
        }

        virtual public void Dispose()
        {
            if ((_flags & Flags.Disposed) != 0)
                return;

            _flags |= Flags.Disposed;
            RemoveFromParent();
            RemoveEventListeners();
            if (graphics != null)
                graphics.Dispose();
            if (_filter != null)
                _filter.Dispose();
            if (paintingGraphics != null)
            {
                if (paintingGraphics.texture != null)
                    paintingGraphics.texture.Dispose();

                paintingGraphics.Dispose();
                // [Godot] the painting host is always a separate node: take the node out of it, then free it.
                if (_paintingHostActive)
                    UnwrapPaintingHost();
                if (_paintingHost != null)
                {
                    FreeNode(_paintingHost);
                    _paintingHost = null;
                }
            }
            // [Godot] Containers dispose their children before their own node is freed (Unity destroyed the
            // GameObject tree first). Freeing a Godot node frees the whole subtree, and the children's NGraphics
            // must still be able to clean up their nodes.
            DisposeChildren();
            DestroyGameObject();
        }

        /// <summary>
        /// [Godot] Called by Dispose before the node is freed. Container overrides it.
        /// </summary>
        internal virtual void DisposeChildren()
        {
        }

        internal void DisplayDisposedWarning()
        {
            if ((_flags & Flags.DisposedWarning) == 0)
            {
                _flags |= Flags.DisposedWarning;

                StringBuilder sb = new StringBuilder();
                sb.Append("DisplayObject is still in use but GameObject was disposed. (");
                if (gOwner != null)
                {
                    sb.Append("type=").Append(gOwner.GetType().Name).Append(", x=").Append(gOwner.x).Append(", y=").Append(gOwner.y).Append(", name=").Append(gOwner.name);
                    if (gOwner.packageItem != null)
                        sb.Append(", res=" + gOwner.packageItem.name);
                }
                else
                {
                    sb.Append("id=").Append(id).Append(", type=").Append(this.GetType().Name).Append(", name=").Append(name);
                }
                sb.Append(")");
                GD.PushError(sb.ToString());
            }
        }

        BatchElement _batchElement;
        // [Godot] Godot batches 2D draws itself; batch elements are still produced for API compatibility
        // (GoWrapper overrides this) but nothing reorders them.
        virtual public BatchElement AddToBatch(List<BatchElement> batchElements, bool force)
        {
            if (graphics != null || force)
            {
                if (_batchElement == null)
                    _batchElement = new BatchElement(this, null);
                _batchElement.material = material;
                _batchElement.breakBatch = (_flags & Flags.SkipBatching) != 0;
                batchElements.Add(_batchElement);

                // [Godot] Unity also added one element per graphics.subInstances entry so that the batcher could
                // reorder them. There is no FairyGUI batcher in Godot, so sub instances are not listed.

                return _batchElement;
            }
            else
                return null;
        }

        protected internal class PaintingInfo
        {
            public Action captureDelegate; //缓存这个delegate，可以防止Capture状态下每帧104B的GC
            public Margin extend;
            public float scale;
            public int flag;
        }

        [Flags]
        public enum Flags
        {
            Disposed = 1,
            UserGameObject = 2,
            TouchDisabled = 4,
            OutlineChanged = 8,
            UpdatingSize = 0x10,
            WidthChanged = 0x20,
            HeightChanged = 0x40,
            PixelPerfect = 0x80,
            LayerSet = 0x100,
            LayerFromParent = 0x200,
            NotFocusable = 0x400,
            TabStop = 0x800,
            TabStopChildren = 0x1000,
            FairyBatching = 0x2000,
            BatchingRequested = 0x4000,
            BatchingRoot = 0x8000,
            SkipBatching = 0x10000,
            CacheAsBitmap = 0x20000,
            GameObjectDisposed = 0x40000,
            DisposedWarning = 0x80000
        }

        // ------------------------------------------------------------------------------------------------
        // [Godot] Godot-specific helpers (no Unity counterpart).
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// [Godot] Called from DisplayObjectNode._Draw(). The only place where NGraphics may submit canvas commands.
        /// TextField overrides it to draw dynamic font glyphs.
        /// </summary>
        internal virtual void OnNodeDraw()
        {
            graphics?.Draw();
        }

        /// <summary>
        /// [Godot] The node that is attached to the parent: the painting host in painting mode, otherwise node.
        /// </summary>
        internal Node2D GetOuterNode()
        {
            if (_paintingHostActive && _paintingHost != null)
                return _paintingHost;
            else
                return node;
        }

        /// <summary>
        /// [Godot] QueueFree() inside the scene tree, Free() outside of it.
        /// </summary>
        internal static void FreeNode(Node n)
        {
            if (n == null || !GodotObject.IsInstanceValid(n) || n.IsQueuedForDeletion())
                return;

            if (n.IsInsideTree())
                n.QueueFree();
            else
                n.Free();
        }

        bool IsParkedAtHome()
        {
            Node outer = GetOuterNode();
            return _home != null && outer != null && GodotObject.IsInstanceValid(_home) && outer.GetParent() == _home;
        }

        /// <summary>
        /// [Godot] Unity: gameObject.SetActive(value).
        /// </summary>
        void SetNodeActive(bool value)
        {
            if (node != null)
                node.Visible = value;
        }

        /// <summary>
        /// [Godot] True if this object is the applied (non-reversed or reversed) mask of its parent container.
        /// </summary>
        internal bool IsActiveMask()
        {
            return parent != null && parent._maskApplied && parent._maskAppliedTo == this;
        }

        /// <summary>
        /// [Godot] alpha -> node.Modulate.a. An active mask keeps Modulate.a = 1 and writes its alpha to
        /// SelfModulate instead. The mask materials (Container) test mask alpha > 0.001 like Unity's stencil, so the
        /// mask's alpha does not reach the masked content (as in Unity) unless it is 0.
        /// </summary>
        internal void ApplyAlpha()
        {
            if (node == null)
                return;

            Color c = node.Modulate;
            if (IsActiveMask())
            {
                c.A = 1;
                node.Modulate = c;
                Color sc = node.SelfModulate;
                sc.A = _alpha;
                node.SelfModulate = sc;
                _selfModulateAlpha = true;
            }
            else
            {
                c.A = _alpha;
                node.Modulate = c;
                if (_selfModulateAlpha)
                {
                    Color sc = node.SelfModulate;
                    sc.A = 1;
                    node.SelfModulate = sc;
                    _selfModulateAlpha = false;
                }
            }
        }

        /// <summary>
        /// [Godot] Inserts the painting host between node and its current parent, at the same child index.
        /// </summary>
        void WrapWithPaintingHost()
        {
            if (_paintingHostActive || _paintingHost == null || node == null)
                return;

            Node p = node.GetParent();
            int index = p != null ? node.GetIndex() : -1;
            if (p != null)
                p.RemoveChild(node);
            _paintingHost.AddChild(node);
            if (p != null)
            {
                p.AddChild(_paintingHost);
                p.MoveChild(_paintingHost, index);
            }
            _paintingHostActive = true;
        }

        /// <summary>
        /// [Godot] Puts node back where the painting host was and detaches the host (kept for reuse).
        /// </summary>
        void UnwrapPaintingHost()
        {
            if (!_paintingHostActive)
                return;

            _paintingHostActive = false;
            if (_paintingHost == null || !GodotObject.IsInstanceValid(_paintingHost))
                return;

            Node p = _paintingHost.GetParent();
            int index = p != null ? _paintingHost.GetIndex() : -1;
            if (p != null)
                p.RemoveChild(_paintingHost);
            if (node != null && GodotObject.IsInstanceValid(node) && node.GetParent() == _paintingHost)
            {
                _paintingHost.RemoveChild(node);
                if (p != null)
                {
                    p.AddChild(node);
                    p.MoveChild(node, index);
                }
            }
        }

        /// <summary>
        /// [Godot] (Re)applies the CanvasGroup mode of the painting host. `extend` becomes the fit margin
        /// (Godot has one margin for all sides, the largest side is used). Mipmaps are generated for BlurFilter.
        /// </summary>
        void RefreshPaintingGroup()
        {
            if (_paintingHost == null || !GodotObject.IsInstanceValid(_paintingHost) || _paintingInfo == null)
                return;

            Margin extend = _paintingInfo.extend;
            float margin = UMath.Max(UMath.Max(extend.left, extend.right), UMath.Max(extend.top, extend.bottom));
            if (margin < 0)
                margin = 0;
            RenderingServer.CanvasItemSetCanvasGroupMode(_paintingHost.GetCanvasItem(),
                RenderingServer.CanvasGroupMode.Transparent, margin + 5, true, margin, true);
        }

        /// <summary>
        /// [Godot] Local -> canvas global ("world") transform of the node. Uses GetGlobalTransformWithCanvas()
        /// inside the tree. Outside the tree the parent chain is composed manually (silent fallback): Godot does not
        /// invalidate cached global transforms of descendants of out-of-tree nodes, so GetGlobalTransform() can be stale.
        /// </summary>
        internal Transform2D GetWorldTransform()
        {
            if (node == null || !GodotObject.IsInstanceValid(node))
                return Transform2D.Identity;

            if (node.IsInsideTree())
                return node.GetGlobalTransformWithCanvas();

            Transform2D t = node.Transform;
            Node p = node.GetParent();
            CanvasItem ci = node;
            while (p != null && !ci.TopLevel)
            {
                CanvasItem pci = p as CanvasItem;
                if (pci == null)
                    break;
                t = pci.GetTransform() * t;
                ci = pci;
                p = p.GetParent();
            }
            return t;
        }

        /// <summary>
        /// [Godot] AffineInverse() that does not throw on a singular matrix (e.g. rotationX = 90). The result maps
        /// every point to NaN, so hit tests fail like they did in Unity for edge-on objects.
        /// </summary>
        internal static Transform2D SafeAffineInverse(Transform2D t)
        {
            float det = t.Determinant();
            if (det == 0 || float.IsNaN(det) || float.IsInfinity(det))
                return new Transform2D(new Vector2(float.NaN, float.NaN), new Vector2(float.NaN, float.NaN), new Vector2(float.NaN, float.NaN));
            return t.AffineInverse();
        }

        /// <summary>
        /// [Godot] Writes x/y/pivot/rotation/scale/skew into node.Transform as one Transform2D.
        /// </summary>
        void ApplyTransform()
        {
            if (node == null)
                return;

            Transform2D t = ComputeTransform();
            if (float.IsNaN(t.X.X) || float.IsNaN(t.X.Y) || float.IsNaN(t.Y.X) || float.IsNaN(t.Y.Y)
                || float.IsNaN(t.Origin.X) || float.IsNaN(t.Origin.Y)
                || float.IsInfinity(t.Origin.X) || float.IsInfinity(t.Origin.Y))
                return;

            if (t == _appliedTransform)
                return;

            _appliedTransform = t;
            node.Transform = t;

            // [Godot] the content of a (non-reversed) mask is a child of the mask node and must cancel its transform.
            if (IsActiveMask())
                parent.SyncMaskContentTransform();
        }

        Transform2D ComputeTransform()
        {
            // pivot in local space, and where it lands in the parent space
            float px = _pivot.X * _contentRect.width;
            float py = _pivot.Y * _contentRect.height;

            // skew matrix (Unity ToolSet.SkewMatrix in y-down space): x' = x*cos(skewY) - y*sin(skewX), y' = x*sin(skewY) + y*cos(skewX)
            float k00 = 1, k01 = 0, k10 = 0, k11 = 1;
            if (_skew.X != 0 || _skew.Y != 0)
            {
                float skx = _skew.X * UMath.Deg2Rad;
                float sky = _skew.Y * UMath.Deg2Rad;
                k00 = UMath.Cos(sky);
                k01 = -UMath.Sin(skx);
                k10 = UMath.Sin(sky);
                k11 = UMath.Cos(skx);
            }

            float r00, r01, r10, r11, r20, r21;
            GetRotationMatrix(out r00, out r01, out r10, out r11, out r20, out r21);
            float sx = _scale.X;
            float sy = _scale.Y;

            // linear part L and the pivot position pp in parent space
            float l00, l01, l10, l11;
            float ppx, ppy;
            if (_perspective)
            {
                // Unity: TRS(scale only) * (skew * rotation) around the pivot (vertex matrix). [Godot] approximation:
                // no perspective divide.
                float a00 = k00 * r00 + k01 * r10;
                float a01 = k00 * r01 + k01 * r11;
                float a10 = k10 * r00 + k11 * r10;
                float a11 = k10 * r01 + k11 * r11;
                l00 = sx * a00; l01 = sx * a01;
                l10 = sy * a10; l11 = sy * a11;
                ppx = _position.X + sx * px;
                ppy = _position.Y + sy * py;
            }
            else
            {
                // Unity: TRS(rotation, scale) of the transform, then the skew vertex matrix around the pivot.
                float b00 = r00 * sx, b01 = r01 * sy;
                float b10 = r10 * sx, b11 = r11 * sy;
                l00 = b00 * k00 + b01 * k10;
                l01 = b00 * k01 + b01 * k11;
                l10 = b10 * k00 + b11 * k10;
                l11 = b10 * k01 + b11 * k11;
                ppx = _position.X + b00 * px + b01 * py;
                ppy = _position.Y + b10 * px + b11 * py;
            }

            float ox = ppx - (l00 * px + l01 * py);
            float oy = ppy - (l10 * px + l11 * py);

            // Transform2D columns: X = (l00, l10), Y = (l01, l11)
            return new Transform2D(new Vector2(l00, l10), new Vector2(l01, l11), new Vector2(ox, oy));
        }

        /// <summary>
        /// [Godot] Orthographic projection of Unity's Euler rotation (Z, then X, then Y) to the FairyGUI y-down plane.
        /// m00..m11 is the 2D linear part, m20/m21 give the z of a rotated point (only used for location.z).
        /// rotationX = rotationY = 0 gives a plain clockwise rotation. With a single X or Y rotation it is
        /// exactly "scale y by cos(rotationX)" / "scale x by cos(rotationY)". In perspective mode Unity does the
        /// rotation in the vertex matrix, which Godot cannot project, so the same affine approximation is used.
        /// </summary>
        void GetRotationMatrix(out float m00, out float m01, out float m10, out float m11, out float m20, out float m21)
        {
            float rx = _rotation.X * UMath.Deg2Rad;
            float ry = _rotation.Y * UMath.Deg2Rad;
            float rz = _rotation.Z * UMath.Deg2Rad;
            float cx = UMath.Cos(rx), sx = UMath.Sin(rx);
            float cy = UMath.Cos(ry), sy = UMath.Sin(ry);
            float c = UMath.Cos(rz), s = UMath.Sin(rz);

            m00 = cy * c - sy * sx * s;
            m01 = -cy * s - sy * sx * c;
            m10 = cx * s;
            m11 = cx * c;
            m20 = -sy * c - cy * sx * s;
            m21 = sy * s - cy * sx * c;
        }
    }
}
