// Ported from FairyGUI-unity: Assets/Scripts/UI/GLoader3D.cs
using Godot;
using FairyGUI.Utils;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    /// <remarks>
    /// [Godot] Spine / DragonBones are not available (Extensions/SpineLoader.cs and DragonBonesLoader.cs are not ported).
    /// The loader shows Godot content instead: a url that is not a ui:// item is loaded as a PackedScene (.tscn/.scn)
    /// and wrapped with GoWrapper; any node can also be shown with SetWrapTarget. The animation API
    /// (animationName/playing/frame/loop/color/timeScale/ignoreEngineTimeScale/Advance) drives the first
    /// AnimationPlayer and/or AnimatedSprite2D found in the wrapped content (best effort, see ApplyNative):
    /// - animationName: the animation to play. Empty = keep the content's own animation (AnimationPlayer: the
    ///   autoplay / last assigned one; AnimatedSprite2D: its current `animation`).
    /// - frame (used when playing == false): AnimationPlayer: percent (0-100) of the animation length, like the Spine
    ///   loader; AnimatedSprite2D: frame index, like the DragonBones loader.
    /// - loop: enforced on the native nodes only when animationName is set.
    /// - color: multiplies the RGB of the wrapped root CanvasItem's Modulate (alpha is left alone, like Spine).
    /// - skinName: stored only, Godot nodes have no skin concept. Override OnChangeNative to map it.
    /// </remarks>
    public partial class GLoader3D : GObject, IAnimationGear, IColorGear
    {
        string _url;
        AlignType _align;
        VertAlignType _verticalAlign;
        bool _autoSize;
        FillType _fill;
        bool _shrinkOnly;
        string _animationName;
        string _skinName;
        bool _playing;
        int _frame;
        bool _loop;
        bool _updatingLayout;
        Color _color;

        protected PackageItem _contentItem;
        protected GoWrapper _content;

        // [Godot] State used to drive native Godot animation nodes inside the wrapped content
        // (replaces the Spine/DragonBones fields of Extensions/SpineLoader.cs and DragonBonesLoader.cs).
        AnimationPlayer _nativePlayer;
        AnimatedSprite2D _nativeSprite;
        CanvasItem _nativeColorTarget;
        Color _nativeBaseColor;
        float _nativePlayerBaseSpeed;
        float _nativeSpriteBaseSpeed;
        bool _nativeDirty;
        float _nativePendingAdvance;
        double _nativeLastPos;
        int _nativeLastFrame;
        bool _nativePlayerWasPlaying;
        bool _nativeSpriteWasPlaying;
        float _timeScale;
        bool _ignoreEngineTimeScale;
        bool _speedControlled;
        float _appliedSpeed;

        public GLoader3D()
        {
            _url = string.Empty;
            _align = AlignType.Left;
            _verticalAlign = VertAlignType.Top;
            _playing = true;
            _color = UColor.white;
            _timeScale = 1; // [Godot] Unity leaves the (unused) auto-property at 0; here it is a real speed factor.
            _nativeBaseColor = UColor.white;
            _nativePlayerBaseSpeed = 1;
            _nativeSpriteBaseSpeed = 1;
            _nativeLastPos = -1;
            _nativeLastFrame = -1;
            _appliedSpeed = float.NaN;
        }

        override protected void CreateDisplayObject()
        {
            displayObject = new Container("GLoader3D");
            displayObject.gOwner = this;

            _content = new GoWrapper();
            _content.onUpdate += OnUpdateContent;
            ((Container)displayObject).AddChild(_content);
            ((Container)displayObject).opaque = true;
        }

        override public void Dispose()
        {
            UnbindNativeContent(); // [Godot] drop the references to the wrapped animation nodes
            _content.Dispose();
            base.Dispose();
        }

        /// <summary>
        ///
        /// </summary>
        public string url
        {
            get { return _url; }
            set
            {
                if (_url == value)
                    return;

                ClearContent();
                _url = value;
                LoadContent();
                UpdateGear(7);
            }
        }

        override public string icon
        {
            get { return _url; }
            set { this.url = value; }
        }

        /// <summary>
        ///
        /// </summary>
        public AlignType align
        {
            get { return _align; }
            set
            {
                if (_align != value)
                {
                    _align = value;
                    UpdateLayout();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public VertAlignType verticalAlign
        {
            get { return _verticalAlign; }
            set
            {
                if (_verticalAlign != value)
                {
                    _verticalAlign = value;
                    UpdateLayout();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public FillType fill
        {
            get { return _fill; }
            set
            {
                if (_fill != value)
                {
                    _fill = value;
                    UpdateLayout();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public bool shrinkOnly
        {
            get { return _shrinkOnly; }
            set
            {
                if (_shrinkOnly != value)
                {
                    _shrinkOnly = value;
                    UpdateLayout();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public bool autoSize
        {
            get { return _autoSize; }
            set
            {
                if (_autoSize != value)
                {
                    _autoSize = value;
                    UpdateLayout();
                }
            }
        }

        public bool playing
        {
            get { return _playing; }
            set
            {
                if (_playing != value)
                {
                    _playing = value;
                    OnChange("playing");
                    UpdateGear(5);
                }
            }
        }

        public int frame
        {
            get { return _frame; }
            set
            {

                if (_frame != value)
                {
                    _frame = value;
                    OnChange("frame");
                    UpdateGear(5);
                }
            }
        }

        /// <summary>
        /// [Godot] Unity: not implemented. Here it multiplies the playback speed of the wrapped
        /// AnimationPlayer / AnimatedSprite2D (SpeedScale = authored SpeedScale * timeScale, once set).
        /// </summary>
        public float timeScale
        {
            get { return _timeScale; }
            set
            {
                _timeScale = value;
                _speedControlled = true;
                ApplyNativeSpeed();
            }
        }

        /// <summary>
        /// [Godot] Unity: not implemented. Here, when true, the SpeedScale of the wrapped animation nodes is
        /// divided by Engine.TimeScale (re-evaluated every frame) so that they play in real time.
        /// </summary>
        public bool ignoreEngineTimeScale
        {
            get { return _ignoreEngineTimeScale; }
            set
            {
                _ignoreEngineTimeScale = value;
                _speedControlled = true;
                ApplyNativeSpeed();
            }
        }

        /// <summary>
        /// [Godot] Unity: not implemented. Here it advances the wrapped AnimationPlayer / AnimatedSprite2D
        /// by time * their SpeedScale (used by Transition when it starts in the middle).
        /// </summary>
        /// <param name="time"></param>
        public void Advance(float time)
        {
            if (!IsValidNode(_nativePlayer) && !IsValidNode(_nativeSprite))
                return;

            if (_nativeDirty || !IsNativeContentInTree())
            {
                //先应用状态，再前进
                _nativePendingAdvance += time;
                RequestApplyNative();
            }
            else
                AdvanceNative(time);
        }

        /// <summary>
        ///
        /// </summary>
        public bool loop
        {
            get { return _loop; }
            set
            {
                if (_loop != value)
                {
                    _loop = value;
                    OnChange("loop");
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <value></value>
        public string animationName
        {
            get { return _animationName; }
            set
            {
                _animationName = value;
                OnChange("animationName");
                UpdateGear(5);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <value></value>
        public string skinName
        {
            get { return _skinName; }
            set
            {
                _skinName = value;
                OnChange("skinName");
                UpdateGear(5);
            }
        }

        /// <summary>
        ///
        /// </summary>
        public Material material
        {
            get { return _content.material; }
            set { _content.material = value; }
        }

        /// <summary>
        ///
        /// </summary>
        public string shader
        {
            get { return _content.shader; }
            set { _content.shader = value; }
        }

        /// <summary>
        ///
        /// </summary>
        public Color color
        {
            get { return _color; }
            set
            {
                if (_color != value)
                {
                    _color = value;
                    UpdateGear(4);

                    OnChange("color");
                }
            }
        }

        /// <summary>
        /// [Godot] Unity: GameObject. The wrapped Godot node (usually a Node2D or Control).
        /// </summary>
        public Node wrapTarget
        {
            get { return _content.wrapTarget; }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="target">[Godot] Unity: GameObject gameObject. Any Godot node (usually a Node2D or Control).</param>
        /// <param name="cloneMaterial"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public void SetWrapTarget(Node target, bool cloneMaterial, int width, int height)
        {
            UnbindNativeContent(); // [Godot] restore the previous target's Modulate / SpeedScale
            _content.SetWrapTarget(target, cloneMaterial);
            _content.SetSize(width, height);
            sourceWidth = width;
            sourceHeight = height;

            UpdateLayout();

            // [Godot] Unity's SetSpine/SetDragonBones applied the loader state (OnChangeSpine(null)) after wrapping.
            // Do the same for the native animation nodes of the new target.
            BindNativeContent(target);
            RequestApplyNative();
        }

        override public IFilter filter
        {
            get { return _content.filter; }
            set { _content.filter = value; }
        }

        override public BlendMode blendMode
        {
            get { return _content.blendMode; }
            set { _content.blendMode = value; }
        }

        /// <summary>
        ///
        /// </summary>
        protected void LoadContent()
        {
            ClearContent();

            if (string.IsNullOrEmpty(_url))
                return;

            _contentItem = UIPackage.GetItemByURL(_url);

            if (_contentItem != null)
            {
                // [Godot] null check added: skeletonLoaders only exists for Spine/DragonBones items
                // (Unity throws a NullReferenceException for other item types here).
                if (_contentItem.skeletonLoaders != null)
                    _contentItem.skeletonLoaders.Add(this);
                _contentItem = _contentItem.getBranch();
                _contentItem = _contentItem.getHighResolution();
                _contentItem.Load();

                // [Godot] Unity: LoadSpine() for PackageItemType.Spine (FAIRYGUI_SPINE) and LoadDragonBones() for
                // PackageItemType.DragoneBones (FAIRYGUI_DRAGONBONES). Neither runtime exists in Godot, so these
                // items show nothing (same as Unity without the defines; UIPackage already warns when loading them).
                // Use a PackedScene url or SetWrapTarget to show Godot content instead.
            }
            else
                LoadExternal();
        }

        virtual protected void OnChange(string propertyName)
        {
            // [Godot] Unity returns when _contentItem is null and forwards to OnChangeSpine / OnChangeDragonBones.
            // Those runtimes are not ported; the change is forwarded to the native Godot animation nodes of the
            // wrapped content instead, whatever the source of the content (PackedScene url or SetWrapTarget).
            OnChangeNative(propertyName);
        }

        virtual protected void LoadExternal()
        {
            // [Godot] Unity: empty (external content is up to subclasses). Here a url that is not a package item is a
            // Godot resource path (res://xxx.tscn, .scn ...) of a PackedScene; it is instantiated and shown with the
            // GoWrapper. Override LoadExternal/FreeExternal for custom loading or pooling.
            if (_url.StartsWith(UIPackage.URL_PREFIX))
                return; //包里找不到的ui://资源

            PackedScene scene = null;
            if (ResourceLoader.Exists(_url, "PackedScene"))
                scene = ResourceLoader.Load(_url, "PackedScene") as PackedScene;
            if (scene == null)
            {
                GD.PushWarning("GLoader3D: cannot load PackedScene '" + _url + "'");
                return;
            }

            Node target = scene.Instantiate();
            if (target == null)
                return;

            // The source size is the size of a Control root, otherwise the current size of the loader
            // (the content is then shown at scale 1 and scaled from there when the loader is resized).
            int w, h;
            Control ctrl = target as Control;
            if (ctrl != null && ctrl.Size.X > 0 && ctrl.Size.Y > 0)
            {
                w = UMath.RoundToInt(ctrl.Size.X);
                h = UMath.RoundToInt(ctrl.Size.Y);
            }
            else
            {
                w = UMath.RoundToInt(this.width);
                h = UMath.RoundToInt(this.height);
            }

            SetWrapTarget(target, false, w, h);
        }

        virtual protected void FreeExternal()
        {
            // [Godot] Unity: GameObject.DestroyImmediate(_content.wrapTarget). The node is freed with QueueFree while it is
            // in the tree, so that changing the url from a signal of the content itself is safe.
            Node target = _content.wrapTarget;
            if (IsValidNode(target))
            {
                if (target.IsInsideTree())
                    target.QueueFree();
                else
                    target.Free();
            }
        }

        protected void UpdateLayout()
        {
            if (sourceWidth == 0 || sourceHeight == 0)
                return;

            float contentWidth = sourceWidth;
            float contentHeight = sourceHeight;

            if (_autoSize)
            {
                _updatingLayout = true;
                if (contentWidth == 0)
                    contentWidth = 50;
                if (contentHeight == 0)
                    contentHeight = 30;
                SetSize(contentWidth, contentHeight);

                _updatingLayout = false;

                if (_width == contentWidth && _height == contentHeight)
                {
                    _content.SetXY(0, 0);
                    _content.SetScale(1, 1);

                    InvalidateBatchingState();
                    return;
                }
                //如果不相等，可能是由于大小限制造成的，要后续处理
            }

            float sx = 1, sy = 1;
            if (_fill != FillType.None)
            {
                sx = this.width / sourceWidth;
                sy = this.height / sourceHeight;

                if (sx != 1 || sy != 1)
                {
                    if (_fill == FillType.ScaleMatchHeight)
                        sx = sy;
                    else if (_fill == FillType.ScaleMatchWidth)
                        sy = sx;
                    else if (_fill == FillType.Scale)
                    {
                        if (sx > sy)
                            sx = sy;
                        else
                            sy = sx;
                    }
                    else if (_fill == FillType.ScaleNoBorder)
                    {
                        if (sx > sy)
                            sy = sx;
                        else
                            sx = sy;
                    }

                    if (_shrinkOnly)
                    {
                        if (sx > 1)
                            sx = 1;
                        if (sy > 1)
                            sy = 1;
                    }

                    contentWidth = sourceWidth * sx;
                    contentHeight = sourceHeight * sy;
                }
            }

            _content.SetScale(sx, sy);

            float nx;
            float ny;
            if (_align == AlignType.Center)
                nx = (this.width - contentWidth) / 2;
            else if (_align == AlignType.Right)
                nx = this.width - contentWidth;
            else
                nx = 0;
            if (_verticalAlign == VertAlignType.Middle)
                ny = (this.height - contentHeight) / 2;
            else if (_verticalAlign == VertAlignType.Bottom)
                ny = this.height - contentHeight;
            else
                ny = 0;
            _content.SetXY(nx, ny);

            InvalidateBatchingState();
        }

        protected void ClearContent()
        {
            UnbindNativeContent(); // [Godot] restore Modulate / SpeedScale of the wrapped animation nodes

            if (IsValidNode(_content.wrapTarget))
            {
                // [Godot] Unity: FreeSpine() / FreeDragonBones() when _contentItem is a Spine / DragonBones item.
                // Those runtimes are not ported, so nothing was created for a package item: a node set with
                // SetWrapTarget is only detached below.
                if (_contentItem == null)
                    FreeExternal();
            }
            _content.wrapTarget = null;
            _contentItem = null;
        }

        protected void OnUpdateContent(UpdateContext context)
        {
            // [Godot] Unity: OnUpdateSpine / OnUpdateDragonBones copy context.alpha * _content.alpha into the skeleton.
            // Not needed in Godot: the wrapped nodes are children of the GoWrapper node and inherit its Modulate.
            // What remains is applying deferred state to the native animation nodes and emulating `loop`.
            if (_nativePlayer == null && _nativeSprite == null)
                return;

            if (_nativeDirty)
            {
                if (!IsNativeContentInTree())
                    return;
                ApplyNative();
            }
            else if (_speedControlled && _ignoreEngineTimeScale)
                ApplyNativeSpeed(); //Engine.TimeScale may have changed

            UpdateNativeLoop();
        }

        override protected void HandleSizeChanged()
        {
            base.HandleSizeChanged();

            if (!_updatingLayout)
                UpdateLayout();
        }

        override public void Setup_BeforeAdd(ByteBuffer buffer, int beginPos)
        {
            base.Setup_BeforeAdd(buffer, beginPos);

            buffer.Seek(beginPos, 5);

            _url = buffer.ReadS();
            _align = (AlignType)buffer.ReadByte();
            _verticalAlign = (VertAlignType)buffer.ReadByte();
            _fill = (FillType)buffer.ReadByte();
            _shrinkOnly = buffer.ReadBool();
            _autoSize = buffer.ReadBool();
            _animationName = buffer.ReadS();
            _skinName = buffer.ReadS();
            _playing = buffer.ReadBool();
            _frame = buffer.ReadInt();
            _loop = buffer.ReadBool();

            if (buffer.ReadBool())
                this.color = buffer.ReadColor(); //color

            if (!string.IsNullOrEmpty(_url))
                LoadContent();
        }

        // ---------------------------------------------------------------------------------------------
        // [Godot] Native Godot animation support. Replaces Extensions/SpineLoader.cs and DragonBonesLoader.cs
        // (SetSpine / SetDragonBones / OnChangeSpine / OnChangeDragonBones ...), which are not ported.
        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// [Godot] The first AnimationPlayer found in the wrapped content (null if none).
        /// Takes the place of Unity's spineAnimation / armatureComponent.
        /// </summary>
        public AnimationPlayer nativeAnimationPlayer
        {
            get { return IsValidNode(_nativePlayer) ? _nativePlayer : null; }
        }

        /// <summary>
        /// [Godot] The first AnimatedSprite2D found in the wrapped content (null if none).
        /// </summary>
        public AnimatedSprite2D nativeAnimatedSprite
        {
            get { return IsValidNode(_nativeSprite) ? _nativeSprite : null; }
        }

        /// <summary>
        /// [Godot] Applies a change of the loader state to the native animation nodes.
        /// Counterpart of Unity's OnChangeSpine / OnChangeDragonBones.
        /// </summary>
        /// <param name="propertyName">"playing", "frame", "loop", "animationName", "skinName", "color", or null for all.</param>
        virtual protected void OnChangeNative(string propertyName)
        {
            if (propertyName == "color")
            {
                ApplyNativeColor();
                return;
            }

            if (propertyName == "skinName")
                return; // Godot animation nodes have no skin concept.

            RequestApplyNative();
        }

        void BindNativeContent(Node target)
        {
            UnbindNativeContent();

            if (!IsValidNode(target))
                return;

            _nativeColorTarget = target as CanvasItem;
            if (_nativeColorTarget != null)
                _nativeBaseColor = _nativeColorTarget.Modulate;

            _nativePlayer = FindNativeNode<AnimationPlayer>(target);
            if (_nativePlayer != null)
                _nativePlayerBaseSpeed = _nativePlayer.SpeedScale;
            _nativeSprite = FindNativeNode<AnimatedSprite2D>(target);
            if (_nativeSprite != null)
                _nativeSpriteBaseSpeed = _nativeSprite.SpeedScale;

            _appliedSpeed = float.NaN;
            _nativeLastPos = -1;
            _nativeLastFrame = -1;
            _nativePlayerWasPlaying = false;
            _nativeSpriteWasPlaying = false;

            ApplyNativeColor();
        }

        void UnbindNativeContent()
        {
            if (IsValidNode(_nativeColorTarget))
                _nativeColorTarget.Modulate = _nativeBaseColor;

            if (!float.IsNaN(_appliedSpeed))
            {
                if (IsValidNode(_nativePlayer))
                    _nativePlayer.SpeedScale = _nativePlayerBaseSpeed;
                if (IsValidNode(_nativeSprite))
                    _nativeSprite.SpeedScale = _nativeSpriteBaseSpeed;
            }

            _nativeColorTarget = null;
            _nativePlayer = null;
            _nativeSprite = null;
            _nativeBaseColor = UColor.white;
            _nativeDirty = false;
            _nativePendingAdvance = 0;
            _appliedSpeed = float.NaN;
        }

        void RequestApplyNative()
        {
            if (_nativePlayer == null && _nativeSprite == null)
                return;

            //在场景树中时立即应用；否则等节点进入场景树（_Ready、autoplay已执行）后在OnUpdateContent中应用
            if (IsNativeContentInTree())
                ApplyNative();
            else
                _nativeDirty = true;
        }

        bool IsNativeContentInTree()
        {
            Node target = _content.wrapTarget;
            return IsValidNode(target) && target.IsInsideTree();
        }

        void ApplyNative()
        {
            _nativeDirty = false;

            ApplyNativeSpeed();

            if (IsValidNode(_nativePlayer))
                ApplyNativePlayer();
            if (IsValidNode(_nativeSprite))
                ApplyNativeSprite();

            if (_nativePendingAdvance != 0)
            {
                float time = _nativePendingAdvance;
                _nativePendingAdvance = 0;
                AdvanceNative(time);
            }
        }

        void ApplyNativeSpeed()
        {
            if (!_speedControlled)
                return;

            float speed = _timeScale;
            if (_ignoreEngineTimeScale)
            {
                float ets = FrameTime.timeScale;
                if (ets > 0)
                    speed /= ets;
            }

            if (speed == _appliedSpeed)
                return;
            _appliedSpeed = speed;

            if (IsValidNode(_nativePlayer))
                _nativePlayer.SpeedScale = _nativePlayerBaseSpeed * speed;
            if (IsValidNode(_nativeSprite))
                _nativeSprite.SpeedScale = _nativeSpriteBaseSpeed * speed;
        }

        void ApplyNativeColor()
        {
            if (!IsValidNode(_nativeColorTarget))
                return;

            //和Spine/DragonBones一样只作用于rgb，alpha由显示树（Modulate）控制
            _nativeColorTarget.Modulate = new Color(_nativeBaseColor.R * _color.R, _nativeBaseColor.G * _color.G,
                _nativeBaseColor.B * _color.B, _nativeBaseColor.A);
        }

        void ApplyNativePlayer()
        {
            AnimationPlayer player = _nativePlayer;
            string name = !string.IsNullOrEmpty(_animationName) ? _animationName : (string)player.AssignedAnimation;
            if (string.IsNullOrEmpty(name))
                return; //没有指定动画，内容也没有autoplay

            if (!player.HasAnimation(name))
            {
                player.Stop(); //like the Spine loader: state.ClearTrack(0)
                return;
            }

            if (_playing)
            {
                //与Spine一样：动画改变或已播放完毕才从头播放，否则继续
                player.Play(name);
            }
            else
            {
                if (player.IsPlaying())
                    player.Pause();
                if ((string)player.AssignedAnimation != name)
                    player.AssignedAnimation = name;
                float length = (float)player.GetAnimation(name).Length;
                player.Seek(UMath.Lerp(0, length, _frame / 100f), true, false); //like the Spine loader: frame is a percentage
            }
            _nativeLastPos = -1;
            _nativePlayerWasPlaying = player.IsPlaying();
        }

        void ApplyNativeSprite()
        {
            AnimatedSprite2D sprite = _nativeSprite;
            SpriteFrames frames = sprite.SpriteFrames;
            if (frames == null)
                return;

            string name = !string.IsNullOrEmpty(_animationName) ? _animationName : (string)sprite.Animation;
            if (string.IsNullOrEmpty(name))
                return;

            if (!frames.HasAnimation(name))
            {
                sprite.Stop(); //like the DragonBones loader: animation.Reset()
                return;
            }

            if (_playing)
                sprite.Play(name);
            else
            {
                sprite.Pause();
                if ((string)sprite.Animation != name)
                    sprite.Animation = name;
                sprite.SetFrameAndProgress(_frame, 0); //like the DragonBones loader: GotoAndStopByFrame(frame)
            }
            _nativeLastFrame = -1;
            _nativeSpriteWasPlaying = sprite.IsPlaying();
        }

        void AdvanceNative(float time)
        {
            if (IsValidNode(_nativePlayer))
                _nativePlayer.Advance(time); // AnimationMixer.Advance applies SpeedScale itself
            if (IsValidNode(_nativeSprite))
                AdvanceNativeSprite(time);
        }

        void AdvanceNativeSprite(float time)
        {
            // AnimatedSprite2D has no advance(): step frames like its internal process does (forward only).
            AnimatedSprite2D sprite = _nativeSprite;
            SpriteFrames frames = sprite.SpriteFrames;
            StringName anim = sprite.Animation;
            if (frames == null || !frames.HasAnimation(anim))
                return;

            int cnt = frames.GetFrameCount(anim);
            float fps = (float)frames.GetAnimationSpeed(anim) * sprite.SpeedScale;
            if (cnt == 0 || fps <= 0 || time <= 0)
                return;

            bool loopAnim = frames.GetAnimationLoopMode(anim) != SpriteFrames.LoopMode.None;
            float total = 0;
            for (int i = 0; i < cnt; i++)
                total += (float)frames.GetFrameDuration(anim, i);
            if (total <= 0)
                return;

            float remaining = time * fps; //以“帧时长单位”计
            if (loopAnim && remaining > total)
                remaining %= total;

            int frame = UMath.Clamp(sprite.Frame, 0, cnt - 1);
            float progress = UMath.Clamp01((float)sprite.FrameProgress);
            while (remaining > 0)
            {
                float dur = (float)frames.GetFrameDuration(anim, frame);
                float left = (1 - progress) * dur;
                if (remaining < left)
                {
                    progress += remaining / dur;
                    break;
                }

                remaining -= left;
                if (frame < cnt - 1)
                {
                    frame++;
                    progress = 0;
                }
                else if (loopAnim)
                {
                    frame = 0;
                    progress = 0;
                }
                else
                {
                    progress = 1;
                    break;
                }
            }
            sprite.SetFrameAndProgress(frame, progress);
        }

        void UpdateNativeLoop()
        {
            // Emulates `loop` when it differs from the loop mode of the animation resource (Animation / SpriteFrames are
            // shared resources, so their loop mode is not modified). Only when animationName is set.
            if (string.IsNullOrEmpty(_animationName))
                return;

            if (IsValidNode(_nativePlayer) && (string)_nativePlayer.AssignedAnimation == _animationName
                && _nativePlayer.HasAnimation(_animationName))
            {
                AnimationPlayer player = _nativePlayer;
                Godot.Animation anim = player.GetAnimation(_animationName);
                bool nativeLoop = anim.LoopMode != Godot.Animation.LoopModeEnum.None;
                bool isPlaying = player.IsPlaying();

                if (_loop && !nativeLoop)
                {
                    //播放完毕（由播放变为停止）后重新播放
                    if (_playing && _nativePlayerWasPlaying && !isPlaying)
                    {
                        player.Play(_animationName); //restarts from the beginning when the end was reached
                        isPlaying = player.IsPlaying();
                    }
                }
                else if (!_loop && nativeLoop && isPlaying)
                {
                    //位置回绕（或pingpong折返）说明一轮已结束：停在末尾
                    double pos = player.CurrentAnimationPosition;
                    if (_nativeLastPos >= 0 && pos < _nativeLastPos && player.SpeedScale > 0)
                    {
                        player.Pause();
                        player.Seek(UMath.Max(0, (float)anim.Length - 0.0001f), true, false);
                        isPlaying = false;
                        pos = -1;
                    }
                    _nativeLastPos = pos;
                }
                _nativePlayerWasPlaying = isPlaying;
            }

            if (IsValidNode(_nativeSprite) && (string)_nativeSprite.Animation == _animationName
                && _nativeSprite.SpriteFrames != null && _nativeSprite.SpriteFrames.HasAnimation(_animationName))
            {
                AnimatedSprite2D sprite = _nativeSprite;
                SpriteFrames frames = sprite.SpriteFrames;
                bool nativeLoop = frames.GetAnimationLoopMode(_animationName) != SpriteFrames.LoopMode.None;
                bool isPlaying = sprite.IsPlaying();

                if (_loop && !nativeLoop)
                {
                    if (_playing && _nativeSpriteWasPlaying && !isPlaying)
                    {
                        sprite.Play(_animationName); //restarts from frame 0 when the last frame was reached
                        isPlaying = sprite.IsPlaying();
                    }
                }
                else if (!_loop && nativeLoop && isPlaying)
                {
                    int frame = sprite.Frame;
                    if (_nativeLastFrame >= 0 && frame < _nativeLastFrame && sprite.SpeedScale > 0)
                    {
                        sprite.Pause();
                        sprite.SetFrameAndProgress(frames.GetFrameCount(_animationName) - 1, 1);
                        isPlaying = false;
                        frame = -1;
                    }
                    _nativeLastFrame = frame;
                }
                _nativeSpriteWasPlaying = isPlaying;
            }
        }

        static bool IsValidNode(GodotObject obj)
        {
            return obj != null && GodotObject.IsInstanceValid(obj);
        }

        static T FindNativeNode<T>(Node root) where T : Node
        {
            if (root is T)
                return (T)root;

            int cnt = root.GetChildCount();
            for (int i = 0; i < cnt; i++)
            {
                T ret = FindNativeNode<T>(root.GetChild(i));
                if (ret != null)
                    return ret;
            }
            return null;
        }
    }
}
