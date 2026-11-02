// Ported from FairyGUI-unity: Assets/Scripts/Core/Stage.cs
using System;
using Godot;
using System.Collections.Generic;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    // [Godot] Input model: Unity polled Input.* every LateUpdate and received key/wheel events in OnGUI.
    // Godot delivers discrete events, so StageEngine._Input calls HandleInputEvent(e), which dispatches
    // touchBegin/touchMove/touchEnd/click/rollOver/rollOut/mouseWheel/keyDown/keyUp immediately with Unity's
    // semantics. InternalUpdate (StageEngine._Process) still re-evaluates the hit target once per frame so
    // rollOver/rollOut follow UI that moves under a static cursor, exactly like Unity's per-frame GetHitTarget.
    // Coordinates: stage coordinates = viewport coordinates (y down, origin top-left). The Stage node lives
    // in a CanvasLayer owned by StageEngine and is never transformed, so no y flipping is needed anywhere.
    public class Stage : Container
    {
        // [Godot] Unity: (int)_contentRect.height, marked [Obsolete("Use size.y")]. These are now the documented
        // replacement for Screen.width/height (PORTING §2), so they read the viewport size live and are not obsolete.
        public int stageHeight { get { return (int)GetViewportSize().Y; } }

        // [Godot] See stageHeight.
        public int stageWidth { get { return (int)GetViewportSize().X; } }

        /// <summary>
        ///
        /// </summary>
        public float soundVolume { get; set; }

        public event Action beforeUpdate;
        public event Action afterUpdate;

        DisplayObject _touchTarget;
        DisplayObject _focused;
        InputTextField _lastInput;
        UpdateContext _updateContext;
        List<DisplayObject> _rollOutChain;
        List<DisplayObject> _rollOverChain;
        TouchInfo[] _touches;
        int _touchCount;
        Vector2 _touchPosition;
        int _frameGotHitTarget;
        int _frameGotTouchPosition;
        bool _customInput;
        Vector2 _customInputPos;
        bool _customInputButtonDown;
        // [Godot] AudioSource -> a pool of AudioStreamPlayer under a node attached to the StageEngine.
        Node _audio;
        List<AudioStreamPlayer> _audioPlayers;
        List<NTexture> _toCollectTextures = new List<NTexture>();
        EventListener _onStageResized;
        List<DisplayObject> _focusOutChain;
        List<DisplayObject> _focusInChain;
        List<Container> _focusHistory;
        Container _nextFocus;
        class CursorDef
        {
            public Texture2D texture;
            public Vector2 hotspot;
        }
        Dictionary<string, CursorDef> _cursors;
        string _currentCursor;

        // [Godot] New state replacing Input.mousePosition / Input.GetTouch polling.
        Vector2 _mousePos = new Vector2(-1, -1); //viewport coordinates of the mouse, negative = unknown/outside the window
        Vector2 _lastTouchPos; //position of the most recent touch event (Unity: Input.GetTouch(Input.touchCount - 1))
        int _lastScreenWidth;
        int _lastScreenHeight;
        static bool _forceScreenSizeCheck;
        static List<Container> _panels = new List<Container>();

        /// <summary>
        /// Content scale settings of the stage. Replaces Stage.inst.gameObject.GetComponent&lt;UIContentScaler&gt;().
        /// </summary>
        // [Godot] New.
        public UIContentScaler contentScaler;

        static bool _touchScreen;
        static bool _keyboardInput;
        static bool _touchSupportDetected;
        internal static int _clickTestThreshold;
        static IKeyboard _keyboard;
        static bool _keyboardOpened;
        static int _canvasLayer = 100;

        static Stage _inst;
        /// <summary>
        ///
        /// </summary>
        public static Stage inst
        {
            get
            {
                if (_inst == null)
                    Instantiate();

                return _inst;
            }
        }

        /// <summary>
        /// True when the Stage has been created (reading this never creates it).
        /// </summary>
        // [Godot] New. Used by StageEngine during shutdown/notifications to avoid creating a Stage.
        internal static bool instantiated
        {
            get { return _inst != null; }
        }

        /// <summary>
        /// CanvasLayer.layer of the layer that hosts the Stage. Default 100 (drawn above the game, unaffected by Camera2D).
        /// </summary>
        // [Godot] New.
        public static int canvasLayer
        {
            get { return _canvasLayer; }
            set
            {
                _canvasLayer = value;
                if (StageEngine.inst != null && GodotObject.IsInstanceValid(StageEngine.inst))
                    StageEngine.inst.ApplyCanvasLayer(value);
            }
        }

        // [Godot] RuntimeInitializeOnLoadMethod (domain-reload reset) removed: Godot resets statics on assembly reload.

        /// <summary>
        ///
        /// </summary>
        public static void Instantiate()
        {
            if (_inst == null)
            {
                _inst = new Stage();
                GRoot._inst = new GRoot();
                GRoot._inst.ApplyContentScaleFactor();
                _inst.AddChild(GRoot._inst.displayObject);

                // [Godot] StageCamera.CheckMainCamera() removed: no stage camera in Godot 2D.
            }
        }

        /// <summary>
        /// 如果是true，表示触摸输入，将使用Input.GetTouch接口读取触摸屏输入。
        /// 如果是false，表示使用鼠标输入，将使用Input.GetMouseButtonXXX接口读取鼠标输入。
        /// 一般来说，不需要设置，底层会自动根据系统环境设置正确的值。
        /// </summary>
        // [Godot] Also switched automatically: true on the first real InputEventScreenTouch, and on desktop back to
        // false on a real mouse button press while no finger is down.
        public static bool touchScreen
        {
            get { return _touchScreen; }
            set
            {
                _touchScreen = value;
                if (_touchScreen)
                {
                    // [Godot] #if !(UNITY_WEBPLAYER || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR)
                    if (!IsKeyboardPlatform())
                        keyboardInput = true;
                    _clickTestThreshold = 50;
                    _touchSupportDetected = true;
                }
                else
                {
                    keyboardInput = false;
                    _keyboardOpened = false;
                    inst.ResetInputState();
                    _clickTestThreshold = 10;
                }
            }
        }

        /// <summary>
        /// 如果是true，表示使用屏幕上弹出的键盘输入文字。常见于移动设备。
        /// 如果是false，表示是接受按键消息输入文字。常见于PC。
        /// 一般来说，不需要设置，底层会自动根据系统环境设置正确的值。
        /// </summary>
        public static bool keyboardInput
        {
            get { return _keyboardInput; }
            set
            {
                _keyboardInput = value;
                if (value && _keyboard == null)
                {
                    // [Godot] #if !(UNITY_WEBPLAYER || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR)
                    if (!IsKeyboardPlatform())
                        _keyboard = new TouchScreenKeyboard();
                }
            }
        }

        // [Godot] Runtime replacement of the compile-time platform list
        // UNITY_WEBPLAYER || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR.
        // Desktop Linux is included as well: Godot has no virtual keyboard there. Web keeps Unity WebGL behaviour.
        static bool IsKeyboardPlatform()
        {
            return OS.HasFeature("pc") || OS.HasFeature("editor");
        }

        // [Godot] Desktop platforms may switch back from touch to mouse input (hybrid touch laptops). Web and mobile
        // never do: browsers synthesize compatibility mouse events after touches.
        static bool CanSwitchBackToMouse()
        {
            return OS.HasFeature("pc");
        }

        /// <summary>
        ///
        /// </summary>
        public static IKeyboard keyboard
        {
            get { return _keyboard; }
            set { _keyboard = value; }
        }

        /// <summary>
        ///
        /// </summary>
        public static bool isTouchOnUI
        {
            get
            {
                return _inst != null && _inst.touchTarget != null;
            }
        }

        /// <summary>
        /// As unity does not provide ways to detect this, you should set it by yourself.
        /// This will effect:
        /// 1. compoistion cursor pos.
        /// 2. mouse wheel speed.
        /// </summary>
        public static float devicePixelRatio
        {
            get; set;
        }

        /// <summary>
        /// The scale of the mouse scroll delta.
        /// </summary>
        public static float mouseWheelScale
        {
            get; set;
        }

        /// <summary>
        ///
        /// </summary>
        public Stage() : base()
        {
            _inst = this;
            soundVolume = 1;

            _updateContext = new UpdateContext();
            _frameGotHitTarget = -1;

            _touches = new TouchInfo[5];
            for (int i = 0; i < _touches.Length; i++)
                _touches[i] = new TouchInfo();

            // [Godot] Application.platform -> OS.GetName()
            bool isOSX = OS.GetName() == "macOS";

            if (OS.GetName() == "Windows"
                || OS.GetName() == "Web"
                || isOSX)
                touchScreen = false;
            else
            {
                // [Godot] Input.touchSupported -> DisplayServer.IsTouchscreenAvailable()
                touchScreen = DisplayServer.IsTouchscreenAvailable();
            }

            // 在PC上，是否retina屏对输入法位置，鼠标滚轮速度都有影响，但现在没发现Unity有获得的方式。仅判断是否Mac可能不够（外接显示器的情况）。所以最好自行设置。
            // [Godot] Godot reports the retina factor directly: DisplayServer.ScreenGetScale() (macOS only, 1 elsewhere).
            devicePixelRatio = isOSX ? UMath.Max(1, DisplayServer.ScreenGetScale()) : 1;
            mouseWheelScale = 1;

            _rollOutChain = new List<DisplayObject>();
            _rollOverChain = new List<DisplayObject>();
            _focusOutChain = new List<DisplayObject>();
            _focusInChain = new List<DisplayObject>();
            _focusHistory = new List<Container>();
            _cursors = new Dictionary<string, CursorDef>();

            Vector2 screenSize = GetViewportSize();
            _lastScreenWidth = (int)screenSize.X;
            _lastScreenHeight = (int)screenSize.Y;
            SetSize(_lastScreenWidth, _lastScreenHeight);
            // [Godot] Unity scaled the Stage transform by StageCamera.DefaultUnitsPerPixel. The Godot stage is in
            // viewport pixels and keeps an identity transform.

            // [Godot] Unity destroyed any existing StageEngine and added a new one to the Stage GameObject. Here the
            // engine is a separate Node: reuse the autoload (or a previously created one), otherwise create it and
            // add it to SceneTree.Root deferred (the root may be busy setting up children right now).
            StageEngine engine = StageEngine.inst;
            if (engine == null || !GodotObject.IsInstanceValid(engine) || engine.IsQueuedForDeletion())
            {
                engine = new StageEngine();
                engine.Name = "FairyGUI";
                SceneTree tree = Engine.GetMainLoop() as SceneTree;
                if (tree != null && tree.Root != null)
                    tree.Root.CallDeferred(Node.MethodName.AddChild, engine);
                else
                    GD.PushError("FairyGUI: no SceneTree, the StageEngine cannot be added. Create the Stage after the main loop has started.");
            }

            node.Name = "Stage";
            // [Godot] gameObject.layer / DontDestroyOnLoad: the Stage node lives under StageEngine's CanvasLayer,
            // which survives scene changes. The node is usable before it enters the tree.
            engine.AttachStage(this);
            contentScaler = new UIContentScaler();

            EnableSound();

            Timers.inst.Add(5, 0, RunTextureCollector);

            // [Godot] SceneManager.sceneLoaded (StageCamera.CheckMainCamera) removed: no stage camera in Godot.
            // [Godot] InputTextField.RegisterEvent() (FAIRYGUI_INPUT_SYSTEM) removed: Godot delivers key/IME events to StageEngine.
        }

        public override void Dispose()
        {
            base.Dispose();

            // [Godot] Unity's world-space panels were children of the Stage and were disposed with it.
            // Registered panels are separate roots here, so dispose them explicitly.
            for (int i = _panels.Count - 1; i >= 0; i--)
            {
                Container panel = _panels[i];
                if (!panel.isDisposed)
                    panel.Dispose();
            }
            _panels.Clear();

            // [Godot] The AudioSource was a component of the Stage GameObject; the player pool is a separate node.
            DisableSound();

            Timers.inst.Remove(RunTextureCollector);
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onStageResized
        {
            get { return _onStageResized ?? (_onStageResized = new EventListener(this, "onStageResized")); }
        }

        /// <summary>
        ///
        /// </summary>
        public DisplayObject touchTarget
        {
            get
            {
                if (_frameGotHitTarget != FrameTime.frameCount)
                    GetHitTarget();

                if (_touchTarget == this)
                    return null;
                else
                    return _touchTarget;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public DisplayObject focus
        {
            get
            {
                if (_focused != null && _focused.isDisposed)
                    _focused = null;
                return _focused;
            }
            set
            {
                SetFocus(value);
            }
        }

        public void SetFocus(DisplayObject newFocus, bool byKey = false)
        {
            if (newFocus == this)
                newFocus = null;

            _nextFocus = null;

            if (_focused == newFocus)
                return;

            Container navRoot = null;
            DisplayObject element = newFocus;
            while (element != null)
            {
                if (!element.focusable)
                    return;
                else if ((element is Container) && ((Container)element).tabStopChildren)
                {
                    if (navRoot == null)
                        navRoot = element as Container;
                }

                element = element.parent;
            }

            DisplayObject oldFocus = _focused;
            _focused = newFocus;

            if (navRoot != null)
            {
                navRoot._lastFocus = _focused;
                int pos = _focusHistory.IndexOf(navRoot);
                if (pos != -1)
                {
                    if (pos < _focusHistory.Count - 1)
                        _focusHistory.RemoveRange(pos + 1, _focusHistory.Count - pos - 1);
                }
                else
                {
                    _focusHistory.Add(navRoot);
                    if (_focusHistory.Count > 10)
                        _focusHistory.RemoveAt(0);
                }
            }

            _focusInChain.Clear();
            _focusOutChain.Clear();

            element = oldFocus;
            while (element != null)
            {
                if (element.focusable)
                    _focusOutChain.Add(element);
                element = element.parent;
            }

            element = _focused;
            int i;
            while (element != null)
            {
                i = _focusOutChain.IndexOf(element);
                if (i != -1)
                {
                    _focusOutChain.RemoveRange(i, _focusOutChain.Count - i);
                    break;
                }
                if (element.focusable)
                    _focusInChain.Add(element);

                element = element.parent;
            }

            int cnt = _focusOutChain.Count;
            if (cnt > 0)
            {
                for (i = 0; i < cnt; i++)
                {
                    element = _focusOutChain[i];
                    if (element.stage != null)
                    {
                        element.DispatchEvent("onFocusOut", null);
                        if (_focused != newFocus) //focus changed in event
                            return;
                    }
                }
                _focusOutChain.Clear();
            }

            cnt = _focusInChain.Count;
            if (cnt > 0)
            {
                for (i = 0; i < cnt; i++)
                {
                    element = _focusInChain[i];
                    if (element.stage != null)
                    {
                        element.DispatchEvent("onFocusIn", byKey ? "key" : null);
                        if (_focused != newFocus) //focus changed in event
                            return;
                    }
                }
                _focusInChain.Clear();
            }

            if (_focused is InputTextField)
                _lastInput = (InputTextField)_focused;
        }

        internal void _OnFocusRemoving(Container sender)
        {
            _nextFocus = sender;
            if (_focusHistory.Count > 0)
            {
                int i = _focusHistory.Count - 1;
                DisplayObject test = _focusHistory[i];
                DisplayObject element = _focused;
                while (element != null && element != sender)
                {
                    if ((element is Container) && ((Container)element).tabStopChildren && element == test)
                    {
                        i--;
                        if (i < 0)
                            break;

                        test = _focusHistory[i];
                    }

                    element = element.parent;
                }

                if (i != _focusHistory.Count - 1)
                {
                    _focusHistory.RemoveRange(i + 1, _focusHistory.Count - i - 1);
                    if (_focusHistory.Count > 0)
                        _nextFocus = _focusHistory[_focusHistory.Count - 1];
                }
            }

            if (_focused is InputTextField)
                _lastInput = null;
            _focused = null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="backward"></param>
        public void DoKeyNavigate(bool backward)
        {
            Container navBase = null;
            DisplayObject element = _focused;
            while (element != null)
            {
                if ((element is Container) && ((Container)element).tabStopChildren)
                {
                    navBase = element as Container;
                    break;
                }

                element = element.parent;
            }

            if (navBase == null)
                navBase = this;

            var it = navBase.GetDescendants(backward);
            bool started = _focused == null;
            DisplayObject test2 = _focused != null ? _focused.parent : null;

            while (it.MoveNext())
            {
                DisplayObject dobj = it.Current;
                if (started)
                {
                    if (dobj == test2)
                        test2 = test2.parent;
                    else if (dobj._AcceptTab())
                        return;
                }
                else if (dobj == _focused)
                    started = true;
            }

            if (started)
            {
                it.Reset();
                while (it.MoveNext())
                {
                    DisplayObject dobj = it.Current;
                    if (dobj == _focused)
                        break;

                    if (dobj == test2)
                        test2 = test2.parent;
                    else if (dobj._AcceptTab())
                        return;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public Vector2 touchPosition
        {
            get
            {
                UpdateTouchPosition();
                return _touchPosition;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="touchId"></param>
        /// <returns></returns>
        public Vector2 GetTouchPosition(int touchId)
        {
            UpdateTouchPosition();

            if (touchId < 0)
                return _touchPosition;

            for (int j = 0; j < 5; j++)
            {
                TouchInfo touch = _touches[j];
                if (touch.touchId == touchId)
                    return new Vector2(touch.x, touch.y);
            }

            return _touchPosition;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="touchId"></param>
        /// <returns></returns>
        public DisplayObject GetTouchTarget(int touchId)
        {
            if (_frameGotHitTarget != FrameTime.frameCount)
                GetHitTarget();

            for (int j = 0; j < 5; j++)
            {
                TouchInfo touch = _touches[j];
                if (touch.touchId == touchId)
                    return touch.target != this ? touch.target : null;
            }

            return null;
        }


        /// <summary>
        ///
        /// </summary>
        public int touchCount
        {
            get { return _touchCount; }
        }

        public int[] GetAllTouch(int[] result)
        {
            if (result == null)
                result = new int[_touchCount];
            int i = 0;
            for (int j = 0; j < 5; j++)
            {
                TouchInfo touch = _touches[j];
                if (touch.touchId != -1)
                {
                    result[i++] = touch.touchId;
                    if (i >= result.Length)
                        break;
                }
            }
            return result;
        }

        /// <summary>
        ///
        /// </summary>
        public void ResetInputState()
        {
            for (int j = 0; j < 5; j++)
                _touches[j].Reset();

            if (!touchScreen)
                _touches[0].touchId = 0;

            _touchCount = 0;
        }


        /// <summary>
        ///
        /// </summary>
        /// <param name="touchId"></param>
        public void CancelClick(int touchId)
        {
            for (int j = 0; j < 5; j++)
            {
                TouchInfo touch = _touches[j];
                if (touch.touchId == touchId)
                    touch.clickCancelled = true;
            }
        }

        /// <summary>
        ///
        /// </summary>
        // [Godot] Creates the AudioStreamPlayer pool node under the StageEngine (Unity: AddComponent<AudioSource>).
        public void EnableSound()
        {
            if (_audio == null)
            {
                _audio = new Node();
                _audio.Name = "Sound";
                _audioPlayers = new List<AudioStreamPlayer>();
                StageEngine engine = StageEngine.inst;
                if (engine != null && GodotObject.IsInstanceValid(engine))
                    engine.AttachNode(_audio);
            }
        }

        /// <summary>
        ///
        /// </summary>
        public void DisableSound()
        {
            if (_audio != null)
            {
                if (GodotObject.IsInstanceValid(_audio))
                {
                    if (_audio.IsInsideTree())
                        _audio.QueueFree();
                    else
                        _audio.Free();
                }
                _audio = null;
                _audioPlayers = null;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="clip"></param>
        /// <param name="volumeScale"></param>
        // [Godot] AudioClip -> Godot.AudioStream (callers pass NAudioClip.nativeClip, as in Unity).
        public void PlayOneShotSound(AudioStream clip, float volumeScale)
        {
            if (_audio != null && this.soundVolume > 0)
                PlayOneShot(clip, volumeScale * this.soundVolume);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="clip"></param>
        public void PlayOneShotSound(AudioStream clip)
        {
            if (_audio != null && this.soundVolume > 0)
                PlayOneShot(clip, this.soundVolume);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="clip"></param>
        /// <param name="volumeScale"></param>
        // [Godot] New overload taking the FairyGUI wrapper directly.
        public void PlayOneShotSound(NAudioClip clip, float volumeScale)
        {
            if (clip != null)
                PlayOneShotSound(clip.nativeClip, volumeScale);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="clip"></param>
        // [Godot] New overload taking the FairyGUI wrapper directly.
        public void PlayOneShotSound(NAudioClip clip)
        {
            if (clip != null)
                PlayOneShotSound(clip.nativeClip);
        }

        // [Godot] AudioSource.PlayOneShot: overlapping one-shots need several players. Reuse a finished player,
        // otherwise add one (up to 16, then the oldest one is restarted).
        void PlayOneShot(AudioStream clip, float volume)
        {
            if (clip == null || !GodotObject.IsInstanceValid(_audio))
                return;

            if (_audio.GetParent() == null)
            {
                StageEngine engine = StageEngine.inst;
                if (engine != null && GodotObject.IsInstanceValid(engine))
                    engine.AttachNode(_audio);
            }
            if (!_audio.IsInsideTree())
                return;

            AudioStreamPlayer player = null;
            int cnt = _audioPlayers.Count;
            for (int i = 0; i < cnt; i++)
            {
                AudioStreamPlayer p = _audioPlayers[i];
                if (!p.Playing)
                {
                    player = p;
                    _audioPlayers.RemoveAt(i);
                    break;
                }
            }

            if (player == null)
            {
                if (cnt < 16)
                {
                    player = new AudioStreamPlayer();
                    _audio.AddChild(player);
                    // [Godot] Unity's AudioSource.PlayOneShot does not keep the clip: release the stream when it ends,
                    // so UIPackage.UnloadAssets/RemovePackage can free it. (Stop() does not emit finished.)
                    AudioStreamPlayer created = player;
                    created.Finished += () => created.Stream = null;
                }
                else
                {
                    player = _audioPlayers[0];
                    _audioPlayers.RemoveAt(0);
                    player.Stop();
                }
            }
            _audioPlayers.Add(player); //most recently used at the end

            player.Stream = clip;
            player.VolumeLinear = volume;
            player.Play();
        }


        /// <summary>
        ///
        /// </summary>
        /// <param name="text"></param>
        /// <param name="autocorrection"></param>
        /// <param name="multiline"></param>
        /// <param name="secure"></param>
        /// <param name="alert"></param>
        /// <param name="textPlaceholder"></param>
        /// <param name="keyboardType"></param>
        /// <param name="hideInput"></param>
        public void OpenKeyboard(string text, bool autocorrection, bool multiline, bool secure,
            bool alert, string textPlaceholder, int keyboardType, bool hideInput)
        {
            if (_keyboard != null)
            {
                _keyboard.Open(text, autocorrection, multiline, secure, alert, textPlaceholder, keyboardType, hideInput);
                _keyboardOpened = true;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public void CloseKeyboard()
        {
            if (_keyboard != null)
            {
                _keyboard.Close();
            }
        }

        /// <summary>
        /// 输入字符到当前光标位置
        /// </summary>
        /// <param name="value"></param>
        public void InputString(string value)
        {
            if (_lastInput != null)
                _lastInput.ReplaceSelection(value);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="screenPos"></param>
        /// <param name="buttonDown"></param>
        // [Godot] screenPos is in viewport coordinates (y down), i.e. stage coordinates. Unity used screen
        // coordinates (y up) and flipped them.
        public void SetCustomInput(Vector2 screenPos, bool buttonDown)
        {
            _customInput = true;
            _customInputButtonDown = buttonDown;
            _customInputPos = screenPos;
            _frameGotHitTarget = 0;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="screenPos"></param>
        /// <param name="buttonDown"></param>
        /// <param name="buttonUp"></param>
        // [Godot] screenPos is in viewport coordinates (y down).
        public void SetCustomInput(Vector2 screenPos, bool buttonDown, bool buttonUp)
        {
            _customInput = true;
            if (buttonDown)
                _customInputButtonDown = true;
            else if (buttonUp)
                _customInputButtonDown = false;
            _customInputPos = screenPos;
            _frameGotHitTarget = 0;
        }

        // [Godot] SetCustomInput(ref RaycastHit, ...) overloads removed: they need a Unity camera and physics raycast.

        public void ForceUpdate()
        {
            _updateContext.Begin();
            Update(_updateContext);
            UpdatePanels(_updateContext);
            _updateContext.End();
        }

        internal void InternalUpdate()
        {
            CheckScreenSize();

            HandleEvents();

            if (_nextFocus != null)
            {
                if (_nextFocus.stage != null)
                {
                    if (_nextFocus.tabStopChildren)
                    {
                        if (_nextFocus._lastFocus != null && _nextFocus.IsAncestorOf(_nextFocus._lastFocus))
                            SetFocus(_nextFocus._lastFocus);
                        else
                            SetFocus(_nextFocus);
                    }
                    else
                        SetFocus(_nextFocus);
                }
                _nextFocus = null;
            }

            if (beforeUpdate != null)
                beforeUpdate();

            _updateContext.Begin();
            Update(_updateContext);
            UpdatePanels(_updateContext);
            _updateContext.End();

            if (BaseFont.textRebuildFlag)
            {
                //字体贴图更改了，重新渲染一遍，防止本帧文字显示错误
                _updateContext.Begin();
                Update(_updateContext);
                UpdatePanels(_updateContext);
                _updateContext.End();

                BaseFont.textRebuildFlag = false;
            }

            if (afterUpdate != null)
                afterUpdate();
        }

        // [Godot] Registered panels (UIPanel/UIPainter) are not children of the Stage here, so they are updated
        // after the Stage within the same UpdateContext (Unity updated them as Stage children).
        void UpdatePanels(UpdateContext context)
        {
            for (int i = 0; i < _panels.Count; i++)
            {
                Container panel = _panels[i];
                if (panel.isDisposed || !panel.node.IsInsideTree())
                    continue;

                panel.Update(context);
            }
        }

        void GetHitTarget()
        {
            if (_frameGotHitTarget == FrameTime.frameCount)
                return;

            _frameGotHitTarget = FrameTime.frameCount;

            if (_customInput)
            {
                // [Godot] _customInputPos is already in stage coordinates (no y flip).
                Vector2 pos = _customInputPos;

                TouchInfo touch = _touches[0];
                _touchTarget = HitTestAll(pos);
                touch.target = _touchTarget;
            }
            else if (touchScreen)
            {
                _touchTarget = null;

                // [Godot] Unity re-tested every non-stationary touch here each frame. Godot touches only move with
                // events (HandleTouchEvents hit-tests them), so between events every active touch is "stationary"
                // and keeps the target found by its last event.
                for (int j = 0; j < 5; j++)
                {
                    TouchInfo touch = _touches[j];
                    if (touch.touchId != -1 && touch.target != null)
                        _touchTarget = touch.target;
                }
            }
            else
            {
                // [Godot] Input.mousePosition -> the last mouse position reported by Godot (viewport coordinates, y down).
                Vector2 pos = _mousePos;
                TouchInfo touch = _touches[0];
                if (float.IsNaN(pos.X) || float.IsNaN(pos.Y) || float.IsInfinity(pos.X) || float.IsInfinity(pos.Y)) //not a valid position
                    _touchTarget = this;
                else if (pos.X < 0 || pos.Y < 0) // outside of the window
                    _touchTarget = this;
                else
                    _touchTarget = HitTestAll(pos);
                touch.target = _touchTarget;
            }

            // [Godot] HitTestContext.ClearRaycastHitCache() removed: 2D hit testing does no physics raycasts.
        }

        // [Godot] Hit test the Stage tree first (its CanvasLayer is drawn above the game world), then the
        // registered panels from the top-most (highest panel order / last registered) to the bottom-most.
        // Returns `this` when nothing is hit, as Container.HitTest does for the Stage.
        DisplayObject HitTestAll(Vector2 pos)
        {
            DisplayObject target = HitTest(pos, true);
            if (target != null && target != this)
                return target;

            for (int i = _panels.Count - 1; i >= 0; i--)
            {
                Container panel = _panels[i];
                if (panel.isDisposed || !panel.visible || !panel.touchable)
                    continue;
                if (!panel.node.IsInsideTree() || !panel.node.IsVisibleInTree())
                    continue;

                DisplayObject ret = panel.HitTest(pos, true);
                if (ret != null)
                    return ret;
            }

            return this;
        }

        // [Godot] Replaces StageCamera's per-frame screen size check (StageCamera.Update/OnScreenSizeChanged).
        // Internal: UIContentScaler.ApplyChange syncs the size before computing the scale factor.
        internal void CheckScreenSize()
        {
            Vector2 size = GetViewportSize();
            int w = (int)size.X;
            int h = (int)size.Y;
            if (w == 0 || h == 0) //e.g. minimized
                return;

            if (w != _lastScreenWidth || h != _lastScreenHeight || _forceScreenSizeCheck)
            {
                _forceScreenSizeCheck = false;
                _lastScreenWidth = w;
                _lastScreenHeight = h;

                StageCamera.screenSizeVer++;
                HandleScreenSizeChanged(w, h, StageCamera.UnitsPerPixel);
            }
        }

        // [Godot] New. Used by StageCamera.ApplyModifiedProperties.
        internal static void ForceScreenSizeCheck()
        {
            _forceScreenSizeCheck = true;
        }

        // [Godot] Size of the viewport the Stage is drawn in: GetViewport().GetVisibleRect().Size.
        // Before the engine enters the tree the root window is used, then DisplayServer.WindowGetSize().
        Vector2 GetViewportSize()
        {
            Viewport viewport = null;
            StageEngine engine = StageEngine.inst;
            if (engine != null && GodotObject.IsInstanceValid(engine) && engine.IsInsideTree())
                viewport = engine.GetViewport();
            if (viewport == null)
            {
                SceneTree tree = Engine.GetMainLoop() as SceneTree;
                if (tree != null)
                    viewport = tree.Root;
            }

            if (viewport != null)
                return viewport.GetVisibleRect().Size;
            else
                return DisplayServer.WindowGetSize();
        }

        internal void HandleScreenSizeChanged(int screenWidth, int screenHeight, float unitsPerPixel)
        {
            SetSize(screenWidth, screenHeight);
            // [Godot] Unity scaled the Stage transform by unitsPerPixel; the Godot stage is never scaled.

            if (!DispatchEvent("onStageResized", null))
            {
                // [Godot] gameObject.GetComponent<UIContentScaler>() -> contentScaler
                UIContentScaler scaler = this.contentScaler;
                scaler.ApplyChange();
                GRoot.inst.ApplyContentScaleFactor();
            }
        }

        /// <summary>
        /// Feed a Godot input event to FairyGUI. Called by StageEngine._Input.
        /// Returns true when the event concerns FairyGUI UI; the caller then marks it handled.
        /// </summary>
        // [Godot] Replaces HandleGUIEvents(Event) and the Input.* polling of HandleMouseEvents/HandleTouchEvents.
        public bool HandleInputEvent(Godot.InputEvent evt)
        {
            if (evt is InputEventMouseButton)
            {
                InputEventMouseButton mb = (InputEventMouseButton)evt;
                if (mb.Device == Godot.InputEvent.DeviceIdEmulation) //mouse emulated from touch
                    return false;

                switch (mb.ButtonIndex)
                {
                    case MouseButton.Left:
                        return HandleMouseButton(0, mb.Pressed, mb.Canceled, mb.Position);
                    case MouseButton.Right:
                        return HandleMouseButton(1, mb.Pressed, mb.Canceled, mb.Position);
                    case MouseButton.Middle:
                        return HandleMouseButton(2, mb.Pressed, mb.Canceled, mb.Position);
                    case MouseButton.WheelUp:
                    case MouseButton.WheelDown:
                        if (!mb.Pressed)
                            return false;
                        // Unity Event.delta.y: positive when scrolling down. A wheel notch is reported as 3 lines on
                        // Windows by Unity; Godot reports 1 event per notch with factor = notch fraction (touchpads).
                        float factor = mb.Factor != 0 ? mb.Factor : 1;
                        float delta = (mb.ButtonIndex == MouseButton.WheelDown ? 3 : -3) * factor;
                        return HandleMouseWheel(delta, mb.Position);
                    default:
                        return false;
                }
            }
            else if (evt is InputEventMouseMotion)
            {
                InputEventMouseMotion mm = (InputEventMouseMotion)evt;
                if (mm.Device == Godot.InputEvent.DeviceIdEmulation) //mouse emulated from touch
                    return false;

                return HandleMouseMotion(mm.Position);
            }
            else if (evt is InputEventScreenTouch)
            {
                InputEventScreenTouch st = (InputEventScreenTouch)evt;
                if (st.Device == Godot.InputEvent.DeviceIdEmulation) //touch emulated from mouse
                    return false;

                TouchPhase phase;
                if (st.Pressed)
                    phase = TouchPhase.Began;
                else if (st.Canceled)
                    phase = TouchPhase.Canceled;
                else
                    phase = TouchPhase.Ended;
                return HandleTouchEvents(st.Index, st.Position, phase);
            }
            else if (evt is InputEventScreenDrag)
            {
                InputEventScreenDrag sd = (InputEventScreenDrag)evt;
                if (sd.Device == Godot.InputEvent.DeviceIdEmulation) //touch emulated from mouse
                    return false;

                return HandleTouchEvents(sd.Index, sd.Position, TouchPhase.Moved);
            }
            else if (evt is InputEventKey)
            {
                return HandleKeyEvent((InputEventKey)evt);
            }
            else if (evt is InputEventPanGesture)
            {
                // [Godot] macOS trackpad / Magic Mouse and Wayland touchpad scrolling arrive as pan gestures, not as
                // wheel buttons (Unity: EventType.ScrollWheel). Delta.Y > 0 scrolls down, like Unity's delta.y.
                // One pan unit is treated like one wheel factor unit, i.e. 3 lines (see the wheel mapping above).
                InputEventPanGesture pg = (InputEventPanGesture)evt;
                if (pg.Delta.Y == 0)
                    return false;
                return HandleMouseWheel(pg.Delta.Y * 3, pg.Position);
            }

            return false;
        }

        // [Godot] The key part of Unity's HandleGUIEvents (EventType.KeyDown / KeyUp).
        // A Godot InputEventKey carries both the key code and the typed character; Unity sent them as separate
        // KeyDown events, InputTextField handles both in one call. Returns true if the event should be consumed:
        // eaten by the IME, or a text input has the focus.
        bool HandleKeyEvent(InputEventKey evt)
        {
            bool pressed = evt.Pressed; //key repeat (Echo) is a KeyDown too, as in Unity

            if (InputTextField.EatKeyEvent(evt))
                return true;

            TouchInfo touch = _touches[0];
            touch.keyCode = evt.Keycode;
            touch.modifiers = evt.GetModifiersMask();
            touch.character = '\0';

            long unicode = pressed ? evt.Unicode : 0;
            if (unicode == 0 && pressed && !evt.CtrlPressed && !evt.AltPressed && !evt.MetaPressed)
            {
                // [Godot] Godot strips control characters from Unicode; Unity delivered them as the event character
                // (InputTextField inserts '\n' for Enter in multi-line fields and '\t' for Tab).
                if (evt.Keycode == Key.Enter || evt.Keycode == Key.KpEnter)
                    unicode = '\n';
                else if (evt.Keycode == Key.Tab)
                    unicode = evt.ShiftPressed ? 25 : '\t';
            }

            if (unicode > 0x10FFFF || unicode >= 0xD800 && unicode <= 0xDFFF) //not a valid code point
                unicode = 0;

            if (unicode > 0xFFFF)
            {
                // Unity delivered characters outside the BMP as two KeyDown events (high + low surrogate).
                string s = char.ConvertFromUtf32((int)unicode);
                touch.character = s[0];
                DispatchKeyEvent(touch, true);
                touch.keyCode = Key.None;
                touch.character = s[1];
                DispatchKeyEvent(touch, true);
            }
            else
            {
                touch.character = (char)unicode;
                DispatchKeyEvent(touch, pressed);
            }

            DisplayObject f = this.focus;
            return f is InputTextField;
        }

        void DispatchKeyEvent(TouchInfo touch, bool down)
        {
            touch.UpdateEvent();
            DisplayObject f = this.focus;
            if (down)
            {
                if (f != null)
                    f.BubbleEvent("onKeyDown", touch.evt);
                else
                    DispatchEvent("onKeyDown", touch.evt);
            }
            else
            {
                if (f != null)
                    f.BubbleEvent("onKeyUp", touch.evt);
                else
                    DispatchEvent("onKeyUp", touch.evt);
            }
        }

        // [Godot] The ScrollWheel part of Unity's HandleGUIEvents.
        bool HandleMouseWheel(float delta, Vector2 pos)
        {
            if (!touchScreen)
            {
                _mousePos = pos;
                _frameGotHitTarget = -1;
                GetHitTarget();
            }

            if (_touchTarget != null)
            {
                TouchInfo touch = _touches[0];
                touch.mouseWheelDelta = delta * Stage.mouseWheelScale;
                touch.UpdateEvent();
                _touchTarget.BubbleEvent("onMouseWheel", touch.evt);
                touch.mouseWheelDelta = 0;
            }

            return _touchTarget != null && _touchTarget != this;
        }

        /// <summary>
        /// Called by StageEngine when the mouse leaves the window.
        /// </summary>
        // [Godot] Unity read a negative/invalid Input.mousePosition when the mouse was outside the window.
        internal void HandleMouseExit()
        {
            _mousePos = new Vector2(-1, -1);
            if (touchScreen)
                return;

            _frameGotHitTarget = -1;
            GetHitTarget();
            TouchInfo touch = _touches[0];
            if (touch.lastRollOver != touch.target)
                HandleRollOver(touch);
        }

        void HandleEvents()
        {
            GetHitTarget();

            UpdateTouchPosition();

            if (_customInput)
            {
                HandleCustomInput();
                _customInput = false;
            }
            else
            {
                if (!_touchSupportDetected)
                {
                    // [Godot] Unity polled Input.touchCount on WebGL here; Godot switches touchScreen on in
                    // HandleTouchEvents when the first real touch arrives.
                    if (OS.GetName() != "Web")
                        _touchSupportDetected = true;
                }
                // [Godot] Touch begin/move/end and mouse buttons are dispatched per event from HandleInputEvent.
                // Per frame only the mouse roll over/out is refreshed (UI may move under a static cursor).
                if (!touchScreen)
                    HandleMouseEvents();
            }

            if (_keyboardOpened)
                HandleKeyboardInput();
        }

        void UpdateTouchPosition()
        {
            if (_frameGotTouchPosition != FrameTime.frameCount)
            {
                _frameGotTouchPosition = FrameTime.frameCount;
                if (_customInput)
                {
                    // [Godot] already in stage coordinates (no y flip)
                    _touchPosition = _customInputPos;
                }
                else if (touchScreen)
                {
                    // [Godot] Input.GetTouch(Input.touchCount - 1).position -> position of the latest touch event
                    if (_touchCount > 0)
                        _touchPosition = _lastTouchPos;
                }
                else
                {
                    Vector2 pos = _mousePos;
                    if (pos.X >= 0 && pos.Y >= 0) // 编辑器环境下坐标有时是负
                    {
                        _touchPosition = pos;
                    }
                }
            }
        }

        void HandleKeyboardInput()
        {
            string s = _keyboard.GetInput();
            if (s != null)
            {
                InputTextField textField = _focused as InputTextField;
                if (textField != null)
                {
                    if (_keyboard.supportsCaret)
                        textField.ReplaceSelection(s);
                    else
                        textField.ReplaceText(s);
                }
            }

            if (_keyboard.done)
            {
                SetFocus(null);
                _keyboardOpened = false;
            }
        }

        void HandleCustomInput()
        {
            // [Godot] already in stage coordinates (no y flip)
            Vector2 pos = _customInputPos;
            TouchInfo touch = _touches[0];

            if (touch.x != pos.X || touch.y != pos.Y)
            {
                touch.x = pos.X;
                touch.y = pos.Y;
                touch.Move();
            }

            if (touch.lastRollOver != touch.target)
                HandleRollOver(touch);

            if (_customInputButtonDown)
            {
                if (!touch.began)
                {
                    _touchCount = 1;
                    touch.Begin();
                    touch.button = 0;
                    touch.touchId = 0;
                    SetFocus(touch.target);

                    touch.UpdateEvent();
                    touch.target.BubbleEvent("onTouchBegin", touch.evt);
                }
            }
            else if (touch.began)
            {
                _touchCount = 0;
                touch.End();

                DisplayObject clickTarget = touch.ClickTest();
                if (clickTarget != null)
                {
                    touch.UpdateEvent();
                    clickTarget.BubbleEvent("onClick", touch.evt);
                }

                touch.button = -1;
            }
        }

        // [Godot] Per frame part of Unity's HandleMouseEvents: keep touch position and roll over/out up to date.
        // Button presses/releases are dispatched per event by HandleMouseButton.
        void HandleMouseEvents()
        {
            TouchInfo touch = _touches[0];
            if (touch.x != _touchPosition.X || touch.y != _touchPosition.Y)
            {
                touch.x = _touchPosition.X;
                touch.y = _touchPosition.Y;
                touch.Move();
            }

            if (touch.lastRollOver != touch.target)
                HandleRollOver(touch);
        }

        // [Godot] Button part of Unity's HandleMouseEvents, called for every Left/Right/Middle button event
        // (Unity polled Input.GetMouseButtonDown/Up(0..2) once per frame). Returns true if the event concerns UI.
        bool HandleMouseButton(int button, bool pressed, bool canceled, Vector2 pos)
        {
            if (touchScreen)
            {
                // [Godot] a real mouse on a desktop touch device: switch back to mouse input
                if (pressed && _touchCount == 0 && CanSwitchBackToMouse())
                    touchScreen = false;
                else
                    return false;
            }

            _mousePos = pos;
            _frameGotHitTarget = -1;
            _frameGotTouchPosition = -1;
            GetHitTarget();
            UpdateTouchPosition();

            TouchInfo touch = _touches[0];
            if (touch.x != _touchPosition.X || touch.y != _touchPosition.Y)
            {
                touch.x = _touchPosition.X;
                touch.y = _touchPosition.Y;
                touch.Move();
            }

            if (touch.lastRollOver != touch.target)
                HandleRollOver(touch);

            bool onUI = IsPointerOnUI(touch);

            if (pressed)
            {
                if (!touch.began)
                {
                    _touchCount = 1;
                    touch.Begin();
                    touch.button = button;
                    SetFocus(touch.target);

                    touch.UpdateEvent();
                    touch.target.BubbleEvent("onTouchBegin", touch.evt);
                }
            }
            else
            {
                if (touch.began)
                {
                    _touchCount = 0;
                    // [Godot] a canceled release (e.g. the window lost focus) ends the touch without a click
                    if (canceled)
                        touch.clickCancelled = true;
                    touch.End();

                    DisplayObject clickTarget = touch.ClickTest();
                    if (clickTarget != null)
                    {
                        touch.UpdateEvent();

                        if (button == 1 || button == 2)
                            clickTarget.BubbleEvent("onRightClick", touch.evt);
                        else
                            clickTarget.BubbleEvent("onClick", touch.evt);
                    }

                    touch.button = -1;
                }

                // We have to do this, coz the cursor will auto change back after a click or dragging
                if (button == 0 && _currentCursor != null)
                    _ChangeCursor(_currentCursor);
            }

            return onUI;
        }

        // [Godot] Move part of Unity's HandleMouseEvents, called for every mouse motion event.
        bool HandleMouseMotion(Vector2 pos)
        {
            _mousePos = pos;
            if (touchScreen)
                return false;

            _frameGotHitTarget = -1;
            _frameGotTouchPosition = -1;
            GetHitTarget();
            UpdateTouchPosition();

            TouchInfo touch = _touches[0];
            if (touch.x != _touchPosition.X || touch.y != _touchPosition.Y)
            {
                touch.x = _touchPosition.X;
                touch.y = _touchPosition.Y;
                touch.Move();
            }

            if (touch.lastRollOver != touch.target)
                HandleRollOver(touch);

            return IsPointerOnUI(touch);
        }

        // [Godot] Replaces UnityEngine.TouchPhase. There is no Stationary: Godot only reports changes.
        enum TouchPhase
        {
            Began,
            Moved,
            Ended,
            Canceled
        }

        // [Godot] Unity iterated Input.GetTouch(i) every frame; Godot delivers one event per touch change, so this
        // is the loop body for a single touch (InputEventScreenTouch/ScreenDrag, Index = touchId), including the
        // slot lookup Unity did in GetHitTarget. Returns true if the touch concerns UI.
        bool HandleTouchEvents(int touchId, Vector2 pos, TouchPhase phase)
        {
            if (!touchScreen)
            {
                // [Godot] first real touch: switch to touch input (Unity: Input.touchSupported / WebGL detection)
                TouchInfo mouse = _touches[0];
                if (mouse.lastRollOver != null)
                {
                    mouse.target = null;
                    HandleRollOver(mouse);
                }
                touchScreen = true;
                ResetInputState();
            }

            _lastTouchPos = pos;
            _frameGotTouchPosition = -1;

            TouchInfo touch = null;
            TouchInfo free = null;
            for (int j = 0; j < 5; j++)
            {
                if (_touches[j].touchId == touchId)
                {
                    touch = _touches[j];
                    break;
                }

                if (_touches[j].touchId == -1)
                    free = _touches[j];
            }
            if (touch == null)
            {
                touch = free;
                if (touch == null || phase != TouchPhase.Began)
                    return false;

                touch.touchId = touchId;
            }

            _touchTarget = HitTestAll(pos);
            touch.target = _touchTarget;
            _frameGotHitTarget = FrameTime.frameCount;

            bool onUI = IsPointerOnUI(touch);

            if (touch.x != pos.X || touch.y != pos.Y)
            {
                touch.x = pos.X;
                touch.y = pos.Y;
                if (touch.began)
                    touch.Move();
            }

            if (touch.lastRollOver != touch.target)
                HandleRollOver(touch);

            if (phase == TouchPhase.Began)
            {
                if (!touch.began)
                {
                    _touchCount++;
                    touch.Begin();
                    touch.button = 0;
                    SetFocus(touch.target);

                    touch.UpdateEvent();
                    touch.target.BubbleEvent("onTouchBegin", touch.evt);
                }
            }
            else if (phase == TouchPhase.Canceled || phase == TouchPhase.Ended)
            {
                if (touch.began)
                {
                    _touchCount--;
                    touch.End();

                    if (phase != TouchPhase.Canceled)
                    {
                        DisplayObject clickTarget = touch.ClickTest();
                        if (clickTarget != null)
                        {
                            // [Godot] Unity set clickCount = uTouch.tapCount. Godot has no tap count on release, so
                            // the time/distance based clickCount computed by TouchInfo.End is kept.
                            touch.UpdateEvent();
                            clickTarget.BubbleEvent("onClick", touch.evt);
                        }
                    }

                    touch.target = null;
                    HandleRollOver(touch);

                    touch.touchId = -1;
                }
            }

            return onUI;
        }

        // [Godot] New. A pointer event concerns FairyGUI when it hits a FairyGUI object, or when it belongs to a
        // press that started on one (dragging/releasing outside). Such events are marked handled (Stage.isTouchOnUI).
        bool IsPointerOnUI(TouchInfo touch)
        {
            if (touch.target != null && touch.target != this)
                return true;

            return touch.began && touch.downTargets.Count > 0 && touch.downTargets[0] != this;
        }

        void HandleRollOver(TouchInfo touch)
        {
            DisplayObject element;
            _rollOverChain.Clear();
            _rollOutChain.Clear();

            element = touch.lastRollOver;
            while (element != null)
            {
                _rollOutChain.Add(element);
                element = element.parent;
            }

            touch.lastRollOver = touch.target;

            string cursor = this.cursor;
            if (cursor == null)
            {
                element = touch.target;
                while (element != null)
                {
                    if (element.cursor != null && cursor == null)
                        cursor = element.cursor;
                    element = element.parent;
                }
            }

            element = touch.target;
            int i;
            while (element != null)
            {
                i = _rollOutChain.IndexOf(element);
                if (i != -1)
                {
                    _rollOutChain.RemoveRange(i, _rollOutChain.Count - i);
                    break;
                }
                _rollOverChain.Add(element);

                element = element.parent;
            }

            int cnt = _rollOutChain.Count;
            if (cnt > 0)
            {
                for (i = 0; i < cnt; i++)
                {
                    element = _rollOutChain[i];
                    if (element.stage != null)
                        element.DispatchEvent("onRollOut", null);
                }
                _rollOutChain.Clear();
            }

            cnt = _rollOverChain.Count;
            if (cnt > 0)
            {
                for (i = 0; i < cnt; i++)
                {
                    element = _rollOverChain[i];
                    if (element.stage != null)
                        element.DispatchEvent("onRollOver", null);
                }
                _rollOverChain.Clear();
            }

            if (cursor != _currentCursor)
                _ChangeCursor(cursor);
        }

        /// <summary>
        /// 设置UIPanel/UIPainter等的渲染层次，由UIPanel等内部调用。开发者不需要调用。
        /// </summary>
        /// <param name="target"></param>
        /// <param name="value"></param>
        public void ApplyPanelOrder(Container target)
        {
            // [Godot] UIPanel/UIPainter containers are registered panels, not Stage children: only their position
            // in the panel list (hit test order) changes. Their draw order follows the Godot scene tree / ZIndex.
            int index = _panels.IndexOf(target);
            if (index != -1)
            {
                _panels.RemoveAt(index);
                InsertPanel(target);
                return;
            }

            int sortingOrder = target._panelOrder;
            int numChildren = this.numChildren;
            int i = 0;
            int j;
            int curIndex = -1;
            for (; i < numChildren; i++)
            {
                DisplayObject obj = GetChildAt(i);
                if (obj == target)
                {
                    curIndex = i;
                    continue;
                }

                if (obj == GRoot.inst.displayObject)
                    j = 1000;
                else if (obj is Container)
                    j = ((Container)obj)._panelOrder;
                else
                    continue;

                if (sortingOrder <= j)
                {
                    if (curIndex != -1)
                        AddChildAt(target, i - 1);
                    else
                        AddChildAt(target, i);
                    break;
                }
            }
            if (i == numChildren)
                AddChild(target);
        }


        static List<DisplayObject> sTempList1;
        static List<int> sTempList2;
        static Dictionary<uint, int> sTempDict;

        /// <summary>
        /// Adjust display order of all UIPanels rendering in worldspace by their z order.
        /// </summary>
        /// <param name="panelSortingOrder">Only UIPanel.sortingOrder equals to this value will be involve in this sorting</param>
        // [Godot] Every registered panel is "world space" here. Unity reordered Stage children by
        // transform.position.z (far first). Godot draws by tree order and ZIndex, so the registered panels with this
        // sorting order are reordered in the panel list (hit test order) by effective ZIndex, lowest first.
        public void SortWorldSpacePanelsByZOrder(int panelSortingOrder)
        {
            if (sTempList1 == null)
            {
                sTempList1 = new List<DisplayObject>();
                sTempList2 = new List<int>();
                sTempDict = new Dictionary<uint, int>();
            }

            int numChildren = _panels.Count;
            for (int i = 0; i < numChildren; i++)
            {
                Container obj = _panels[i];
                if (obj == null || obj._panelOrder != panelSortingOrder)
                    continue;

                sTempDict[obj.id] = i;

                sTempList1.Add(obj);
                sTempList2.Add(i);
            }

            sTempList1.Sort((DisplayObject c1, DisplayObject c2) =>
            {
                int ret = GetEffectiveZIndex(c1.node).CompareTo(GetEffectiveZIndex(c2.node));
                if (ret == 0)
                {
                    //如果大家z值一样，使用原来的顺序，防止不停交换顺序（闪烁）
                    return sTempDict[c1.id].CompareTo(sTempDict[c2.id]);
                }
                else
                    return ret;
            });

            // [Godot] ChangeChildrenOrder(sTempList2, sTempList1) -> write back into the panel list
            int cnt = sTempList1.Count;
            for (int i = 0; i < cnt; i++)
                _panels[sTempList2[i]] = (Container)sTempList1[i];

            sTempList1.Clear();
            sTempList2.Clear();
            sTempDict.Clear();
        }

        // [Godot] New helper: absolute z index of a canvas item (relative ZIndex summed up to the first absolute one).
        static int GetEffectiveZIndex(Node node)
        {
            int z = 0;
            Node n = node;
            while (n != null)
            {
                CanvasItem ci = n as CanvasItem;
                if (ci != null)
                {
                    z += ci.ZIndex;
                    if (!ci.ZAsRelative)
                        break;
                }
                n = n.GetParent();
            }
            return z;
        }

        /// <summary>
        /// Register a container that is not a child of the Stage (UIPanel, UIPainter...) as an extra root.
        /// It is updated every frame after the Stage, takes part in hit testing (if touchable), and counts as
        /// being on stage. Panels are kept sorted by Container._panelOrder (see ApplyPanelOrder).
        /// </summary>
        // [Godot] New. Unity attached such containers to the Stage as children whose GameObject stayed in place.
        public void RegisterPanel(Container target)
        {
            if (target == null || target == this || IsRegisteredPanel(target))
                return;

            InsertPanel(target);

            if (!target.isDisposed)
                target.BroadcastEvent("onAddedToStage", null);
        }

        /// <summary>
        ///
        /// </summary>
        // [Godot] New. Counterpart of RegisterPanel (Unity: removing the panel container from the Stage).
        public void UnregisterPanel(Container target)
        {
            if (target == null || !IsRegisteredPanel(target))
                return;

            if (!target.isDisposed)
            {
                target.BroadcastEvent("onRemovedFromStage", null);
                if (target == _focused || target.IsAncestorOf(_focused))
                    _OnFocusRemoving(this);
            }
            _panels.Remove(target);
        }

        /// <summary>
        /// Whether the container is a registered panel root (see RegisterPanel).
        /// </summary>
        // [Godot] New. Static so DisplayObject.stage/topmost and event bubbling can query it without creating the Stage.
        public static bool IsRegisteredPanel(Container target)
        {
            if (target == null)
                return false;

            int cnt = _panels.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (_panels[i] == target)
                    return true;
            }
            return false;
        }

        // [Godot] Keeps the panel list sorted by _panelOrder; equal orders keep registration order.
        static void InsertPanel(Container target)
        {
            int i = _panels.Count;
            while (i > 0 && _panels[i - 1]._panelOrder > target._panelOrder)
                i--;
            _panels.Insert(i, target);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="texture"></param>
        public void MonitorTexture(NTexture texture)
        {
            if (_toCollectTextures.IndexOf(texture) == -1)
                _toCollectTextures.Add(texture);
        }

        void RunTextureCollector(object param)
        {
            int cnt = _toCollectTextures.Count;
            float curTime = FrameTime.time;
            int i = 0;
            while (i < cnt)
            {
                NTexture texture = _toCollectTextures[i];
                if (texture.disposed)
                {
                    _toCollectTextures.RemoveAt(i);
                    cnt--;
                }
                else if (curTime - texture.lastActive > 5)
                {
                    texture.Dispose();
                    _toCollectTextures.RemoveAt(i);
                    cnt--;
                }
                else
                    i++;
            }
        }

        public void AddTouchMonitor(int touchId, EventDispatcher target)
        {
            TouchInfo touch = null;
            for (int j = 0; j < 5; j++)
            {
                touch = _touches[j];
                if (touchId == -1 && touch.touchId != -1
                    || touchId != -1 && touch.touchId == touchId)
                    break;
            }
            if (touch.touchMonitors.IndexOf(target) == -1)
                touch.touchMonitors.Add(target);
        }

        public void RemoveTouchMonitor(EventDispatcher target)
        {
            for (int j = 0; j < 5; j++)
            {
                TouchInfo touch = _touches[j];
                int i = touch.touchMonitors.IndexOf(target);
                if (i != -1)
                    touch.touchMonitors[i] = null;
            }
        }

        public bool IsTouchMonitoring(EventDispatcher target)
        {
            for (int j = 0; j < 5; j++)
            {
                TouchInfo touch = _touches[j];
                int i = touch.touchMonitors.IndexOf(target);
                if (i != -1)
                    return true;
            }

            return false;
        }

        // [Godot] Returns a hidden Node2D instead of the Transform of an inactive GameObject under the Stage.
        // It is attached to the StageEngine (not to the Stage node, whose children mirror the display list).
        internal Node2D CreatePoolManager(string name)
        {
            Node2D t = new Node2D();
            t.Name = "[" + name + "]";
            t.Visible = false;

            StageEngine engine = StageEngine.inst;
            if (engine != null && GodotObject.IsInstanceValid(engine))
                engine.AttachNode(t);

            return t;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="cursorName"></param>
        /// <param name="texture"></param>
        /// <param name="hotspot"></param>
        public void RegisterCursor(string cursorName, Texture2D texture, Vector2 hotspot)
        {
            _cursors[cursorName] = new CursorDef() { texture = texture, hotspot = hotspot };
        }

        /// <summary>
        ///
        /// </summary>
        /// <value></value>
        public string activeCursor
        {
            get { return _currentCursor; }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="cursorName"></param>
        // [Godot] Cursor.SetCursor(texture, hotspot, CursorMode.Auto) -> Input.SetCustomMouseCursor(texture, Arrow, hotspot)
        internal void _ChangeCursor(string cursorName)
        {
            CursorDef cursorDef;
            if (cursorName != null && _cursors.TryGetValue(cursorName, out cursorDef))
            {
                if (_currentCursor == cursorName)
                    Input.SetCustomMouseCursor(null);
                _currentCursor = cursorName;
                Input.SetCustomMouseCursor(cursorDef.texture, Input.CursorShape.Arrow, cursorDef.hotspot);
            }
            else
            {
                _currentCursor = null;
                Input.SetCustomMouseCursor(null);
            }
        }
    }

    class TouchInfo
    {
        public float x;
        public float y;
        public int touchId;
        public int clickCount;
        // [Godot] KeyCode -> Godot.Key, EventModifiers -> Godot.KeyModifierMask
        public Key keyCode;
        public char character;
        public KeyModifierMask modifiers;
        public float mouseWheelDelta;
        public int button;

        public float downX;
        public float downY;
        public float downTime;
        public int downFrame;
        public bool began;
        public bool clickCancelled;
        public float lastClickTime;
        public float lastClickX;
        public float lastClickY;
        public int lastClickButton;
        public float holdTime;
        public DisplayObject target;
        public List<DisplayObject> downTargets;
        public DisplayObject lastRollOver;
        public List<EventDispatcher> touchMonitors;

        public InputEvent evt;

        static List<EventBridge> sHelperChain = new List<EventBridge>();

        public TouchInfo()
        {
            evt = new InputEvent();
            downTargets = new List<DisplayObject>();
            touchMonitors = new List<EventDispatcher>();
            Reset();
        }

        public void Reset()
        {
            touchId = -1;
            x = 0;
            y = 0;
            clickCount = 0;
            button = -1;
            keyCode = Key.None;
            character = '\0';
            modifiers = 0;
            mouseWheelDelta = 0;
            lastClickTime = 0;
            began = false;
            target = null;
            downTargets.Clear();
            lastRollOver = null;
            clickCancelled = false;
            touchMonitors.Clear();
        }

        public void UpdateEvent()
        {
            evt.touchId = this.touchId;
            evt.x = this.x;
            evt.y = this.y;
            evt.clickCount = this.clickCount;
            evt.keyCode = this.keyCode;
            evt.character = this.character;
            evt.modifiers = this.modifiers;
            evt.mouseWheelDelta = this.mouseWheelDelta;
            evt.button = this.button;
            evt.holdTime = this.holdTime;
        }

        public void Begin()
        {
            began = true;
            clickCancelled = false;
            downX = x;
            downY = y;
            downTime = FrameTime.unscaledTime;
            downFrame = FrameTime.frameCount;
            holdTime = 0;

            downTargets.Clear();
            if (target != null)
            {
                downTargets.Add(target);
                DisplayObject obj = target;
                while (obj != null)
                {
                    downTargets.Add(obj);
                    obj = obj.parent;
                }
            }
        }

        public void Move()
        {
            if (began)
                holdTime = (FrameTime.frameCount - downFrame) == 1 ? OneFrameTime() : (FrameTime.unscaledTime - downTime);

            UpdateEvent();

            if (UMath.Abs(x - downX) > 50 || UMath.Abs(y - downY) > 50) clickCancelled = true;

            if (touchMonitors.Count > 0)
            {
                int len = touchMonitors.Count;
                for (int i = 0; i < len; i++)
                {
                    EventDispatcher e = touchMonitors[i];
                    if (e != null)
                    {
                        if ((e is DisplayObject) && ((DisplayObject)e).stage == null)
                            continue;
                        if ((e is GObject) && !((GObject)e).onStage)
                            continue;
                        e.GetChainBridges("onTouchMove", sHelperChain, false);
                    }
                }

                Stage.inst.BubbleEvent("onTouchMove", evt, sHelperChain);
                sHelperChain.Clear();
            }
            else
                Stage.inst.DispatchEvent("onTouchMove", evt);
        }

        public void End()
        {
            began = false;

            if (downTargets.Count == 0
                || clickCancelled
                || UMath.Abs(x - downX) > Stage._clickTestThreshold
                || UMath.Abs(y - downY) > Stage._clickTestThreshold)
            {
                clickCancelled = true;
                lastClickTime = 0;
                clickCount = 1;
            }
            else
            {
                if (FrameTime.unscaledTime - lastClickTime < 0.35f
                    && UMath.Abs(x - lastClickX) < Stage._clickTestThreshold
                    && UMath.Abs(y - lastClickY) < Stage._clickTestThreshold
                    && lastClickButton == button)
                {
                    if (clickCount == 2)
                        clickCount = 1;
                    else
                        clickCount++;
                }
                else
                    clickCount = 1;
                lastClickTime = FrameTime.unscaledTime;
                lastClickX = x;
                lastClickY = y;
                lastClickButton = button;
            }

            //当间隔一帧时，使用帧率计算时间，避免掉帧因素
            holdTime = (FrameTime.frameCount - downFrame) == 1 ? OneFrameTime() : (FrameTime.unscaledTime - downTime);
            UpdateEvent();

            if (touchMonitors.Count > 0)
            {
                int len = touchMonitors.Count;
                for (int i = 0; i < len; i++)
                {
                    EventDispatcher e = touchMonitors[i];
                    if (e != null)
                        e.GetChainBridges("onTouchEnd", sHelperChain, false);
                }
                target.BubbleEvent("onTouchEnd", evt, sHelperChain);

                touchMonitors.Clear();
                sHelperChain.Clear();
            }
            else
                target.BubbleEvent("onTouchEnd", evt);
        }

        // [Godot] Unity: 1f / Application.targetFrameRate. Godot's Engine.MaxFps defaults to 0 (unlimited), which
        // would give Infinity, so the duration of the last frame is used when no frame rate limit is set.
        static float OneFrameTime()
        {
            int fps = FrameTime.targetFrameRate;
            return fps > 0 ? 1f / fps : FrameTime.unscaledDeltaTime;
        }

        public DisplayObject ClickTest()
        {
            if (clickCancelled)
            {
                downTargets.Clear();
                return null;
            }

            DisplayObject obj = downTargets[0];
            if (obj.stage != null) //依然派发到原来的downTarget，虽然可能它已经偏离当前位置，主要是为了正确处理点击缩放的效果
            {
                downTargets.Clear();
                return obj;
            }

            obj = target;
            while (obj != null)
            {
                int i = downTargets.IndexOf(obj);
                if (i != -1 && obj.stage != null)
                    break;

                obj = obj.parent;
            }

            downTargets.Clear();

            return obj;
        }
    }
}
