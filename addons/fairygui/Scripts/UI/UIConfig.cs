// Ported from FairyGUI-unity: Assets/Scripts/UI/UIConfig.cs
using System;
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    /// <summary>
    /// Global configs. These options should be set before any UI construction.
    /// </summary>
    // [Godot] MonoBehaviour component -> optional Node. Add it to a scene (or autoload) to apply Items and
    // PreloadPackages in _Ready. All static fields can also be set directly from code, as in Unity.
    public partial class UIConfig : Node
    {
        /// <summary>
        /// Dynamic Font Support.
        /// 4.x: Put the xxx.ttf into /Resources or /Resources/Fonts, and set defaultFont="xxx".
        /// 5.x: set defaultFont to system font name(or names joint with comma). e.g. defaultFont="Microsoft YaHei, SimHei"
        /// [Godot] Resolved by FontManager: registered alias, ui:// package font, res:// path, SystemFont (by name), ThemeDB.FallbackFont.
        /// </summary>
        public static string defaultFont = "";

        [Obsolete("No use anymore")]
        public static bool renderingTextBrighterOnDesktop = true;

        /// <summary>
        /// Resource using in Window.ShowModalWait for locking the window.
        /// </summary>
        public static string windowModalWaiting;

        /// <summary>
        /// Resource using in GRoot.ShowModalWait for locking the screen.
        /// </summary>
        public static string globalModalWaiting;

        /// <summary>
        /// When a modal window is in front, the background becomes dark.
        /// </summary>
        public static Color modalLayerColor = new Color(0f, 0f, 0f, 0.4f);

        /// <summary>
        /// Default button click sound.
        /// </summary>
        public static NAudioClip buttonSound;

        /// <summary>
        /// Default button click sound volume.
        /// </summary>
        public static float buttonSoundVolumeScale = 1f;

        /// <summary>
        /// Resource url of horizontal scrollbar
        /// </summary>
        public static string horizontalScrollBar;

        /// <summary>
        /// Resource url of vertical scrollbar
        /// </summary>
        public static string verticalScrollBar;

        /// <summary>
        /// Scrolling step in pixels
        /// 当调用ScrollPane.scrollUp/Down/Left/Right时，或者点击滚动条的上下箭头时，滑动的距离。
        /// 鼠标滚轮触发一次滚动的距离设定为defaultScrollStep*2
        /// </summary>
        public static float defaultScrollStep = 25;

        /// <summary>
        /// Deceleration ratio of scrollpane when its in touch dragging.
        /// 当手指拖动并释放滚动区域后，内容会滑动一定距离后停下，这个速率就是减速的速率。
        /// 越接近1，减速越慢，意味着滑动的时间和距离更长。
        /// 这个是全局设置，也可以通过ScrollPane.decelerationRate进行个性设置。
        /// </summary>
        public static float defaultScrollDecelerationRate = 0.967f;

        /// <summary>
        /// Scrollbar display mode. Recommended 'Auto' for mobile and 'Visible' for web.
        /// </summary>
        public static ScrollBarDisplayType defaultScrollBarDisplay = ScrollBarDisplayType.Default;

        /// <summary>
        /// Allow dragging anywhere in container to scroll.
        /// </summary>
        public static bool defaultScrollTouchEffect = true;

        /// <summary>
        /// The "rebound" effect in the scolling container.
        /// </summary>
        public static bool defaultScrollBounceEffect = true;

        /// <summary>
        /// When the scrolling container is set to "snap to the item", the rolling distance threshold of which item is close to is determined.
        /// </summary>
        public static float defaultScrollSnappingThreshold = 0.5f;

        /// <summary>
        /// When the scrolling container is set to "page mode", it determines the scrolling distance threshold to which page to turn.
        /// </summary>
        public static float defaultScrollPagingThreshold = 0.3f;

        /// <summary>
        /// Resources url of PopupMenu.
        /// </summary>
        public static string popupMenu;

        /// <summary>
        /// Resource url of menu seperator.
        /// </summary>
        public static string popupMenu_seperator;

        /// <summary>
        /// In case of failure of loading content for GLoader, use this sign to indicate an error.
        /// </summary>
        public static string loaderErrorSign;

        /// <summary>
        /// Resource url of tooltips.
        /// </summary>
        public static string tooltipsWin;

        /// <summary>
        /// The number of visible items in ComboBox.
        /// </summary>
        public static int defaultComboBoxVisibleItemCount = 10;

        /// <summary>
        /// Pixel offsets of finger to trigger scrolling
        /// </summary>
        public static int touchScrollSensitivity = 20;

        /// <summary>
        /// Pixel offsets of finger to trigger dragging
        /// </summary>
        public static int touchDragSensitivity = 10;

        /// <summary>
        /// Pixel offsets of mouse pointer to trigger dragging.
        /// </summary>
        public static int clickDragSensitivity = 2;

        /// <summary>
        /// Allow softness on top or left side for scrollpane.
        /// </summary>
        public static bool allowSoftnessOnTopOrLeftSide = true;

        /// <summary>
        /// When click the window, brings to front automatically.
        /// </summary>
        public static bool bringWindowToFrontOnClick = true;

        /// <summary>
        ///
        /// </summary>
        public static float inputCaretSize = 1;

        /// <summary>
        ///
        /// </summary>
        public static Color inputHighlightColor = UColor.FromBytes(255, 223, 141, 128);

        /// <summary>
        ///
        /// </summary>
        public static float frameTimeForAsyncUIConstruction = 0.002f;

        /// <summary>
        /// if RenderTexture using in painting mode has depth support.
        /// [Godot] Painting mode uses CanvasGroup instead of a RenderTexture, so this option has no effect. Kept for API compatibility.
        /// </summary>
        public static bool depthSupportForPaintingMode = true;

        /// <summary>
        /// Indicates whether to draw extra 4 or 8 times to achieve stroke effect for textfield.
        /// If it is true, that is the 8 times, otherwise it is the 4 times.
        /// </summary>
        public static bool enhancedTextOutlineEffect = false;

        [Obsolete("No use anymore.")]
        public static VertAlignType richTextRowVerticalAlign = VertAlignType.Bottom;

        /// <summary>
        /// Suggest to enable it on low dpi (e.g. 96dpi) screens.
        /// </summary>
        public static bool makePixelPerfect = false;

        public enum ConfigKey
        {
            DefaultFont,
            ButtonSound,
            ButtonSoundVolumeScale,
            HorizontalScrollBar,
            VerticalScrollBar,
            DefaultScrollStep,
            DefaultScrollBarDisplay,
            DefaultScrollTouchEffect,
            DefaultScrollBounceEffect,
            TouchScrollSensitivity,
            WindowModalWaiting,
            GlobalModalWaiting,
            PopupMenu,
            PopupMenu_seperator,
            LoaderErrorSign,
            TooltipsWin,
            DefaultComboBoxVisibleItemCount,
            TouchDragSensitivity,
            ClickDragSensitivity,
            ModalLayerColor,
            RenderingTextBrighterOnDesktop,
            AllowSoftnessOnTopOrLeftSide,
            InputCaretSize,
            InputHighlightColor,
            EnhancedTextOutlineEffect,
            DepthSupportForPaintingMode,
            RichTextRowVerticalAlign,
            Branch,

            PleaseSelect = 100
        }

        [Serializable]
        public class ConfigValue
        {
            public bool valid;
            public string s;
            public int i;
            public float f;
            public bool b;
            public Color c;

            public void Reset()
            {
                valid = false;
                s = null;
                i = 0;
                f = 0;
                b = false;
                c = UColor.black;
            }
        }

        // [Godot] Unity stores List<ConfigValue> indexed by ConfigKey and edits it with a custom inspector.
        // Godot cannot export plain C# objects, so Items is an exported typed dictionary (ConfigKey -> value).
        // The inspector shows a ConfigKey dropdown per entry; the value may be a String, int, float, bool or Color.
        // Load() converts every entry to a ConfigValue and applies it exactly like Unity.
        [Export]
        public Godot.Collections.Dictionary<ConfigKey, Variant> Items = new Godot.Collections.Dictionary<ConfigKey, Variant>();

        // [Godot] List<string> -> string[] (exported as PackedStringArray). Each entry is a path accepted by
        // UIPackage.AddPackage(string), e.g. "res://UI/Basics" (no "_fui.bytes" suffix).
        [Export]
        public string[] PreloadPackages = new string[0];

        // [Godot] Unity Awake -> _Ready.
        public override void _Ready()
        {
            if (!Engine.IsEditorHint())
            {
                foreach (string packagePath in PreloadPackages)
                {
                    UIPackage.AddPackage(packagePath);
                }

                Load();
            }
        }

        public void Load()
        {
            // [Godot] Unity iterates the List by index (index == ConfigKey). Here every ConfigKey is looked up
            // in the Items dictionary in the same order, so the switch below is identical to Unity's.
            int cnt = (int)ConfigKey.Branch + 1;
            for (int i = 0; i < cnt; i++)
            {
                Variant v;
                if (!Items.TryGetValue((ConfigKey)i, out v))
                    continue;

                ConfigValue value = ToConfigValue(v);
                if (!value.valid)
                    continue;

                switch ((UIConfig.ConfigKey)i)
                {
                    case ConfigKey.ButtonSound:
                        if (!Engine.IsEditorHint())
                            UIConfig.buttonSound = UIPackage.GetItemAssetByURL(value.s) as NAudioClip;
                        break;

                    case ConfigKey.ButtonSoundVolumeScale:
                        UIConfig.buttonSoundVolumeScale = value.f;
                        break;

                    case ConfigKey.ClickDragSensitivity:
                        UIConfig.clickDragSensitivity = value.i;
                        break;

                    case ConfigKey.DefaultComboBoxVisibleItemCount:
                        UIConfig.defaultComboBoxVisibleItemCount = value.i;
                        break;

                    case ConfigKey.DefaultFont:
                        UIConfig.defaultFont = value.s;
                        break;

                    case ConfigKey.DefaultScrollBarDisplay:
                        UIConfig.defaultScrollBarDisplay = (ScrollBarDisplayType)value.i;
                        break;

                    case ConfigKey.DefaultScrollBounceEffect:
                        UIConfig.defaultScrollBounceEffect = value.b;
                        break;

                    case ConfigKey.DefaultScrollStep:
                        UIConfig.defaultScrollStep = value.i;
                        break;

                    case ConfigKey.DefaultScrollTouchEffect:
                        UIConfig.defaultScrollTouchEffect = value.b;
                        break;

                    case ConfigKey.GlobalModalWaiting:
                        UIConfig.globalModalWaiting = value.s;
                        break;

                    case ConfigKey.HorizontalScrollBar:
                        UIConfig.horizontalScrollBar = value.s;
                        break;

                    case ConfigKey.LoaderErrorSign:
                        UIConfig.loaderErrorSign = value.s;
                        break;

                    case ConfigKey.ModalLayerColor:
                        UIConfig.modalLayerColor = value.c;
                        break;

                    case ConfigKey.PopupMenu:
                        UIConfig.popupMenu = value.s;
                        break;

                    case ConfigKey.PopupMenu_seperator:
                        UIConfig.popupMenu_seperator = value.s;
                        break;

                    case ConfigKey.TooltipsWin:
                        UIConfig.tooltipsWin = value.s;
                        break;

                    case ConfigKey.TouchDragSensitivity:
                        UIConfig.touchDragSensitivity = value.i;
                        break;

                    case ConfigKey.TouchScrollSensitivity:
                        UIConfig.touchScrollSensitivity = value.i;
                        break;

                    case ConfigKey.VerticalScrollBar:
                        UIConfig.verticalScrollBar = value.s;
                        break;

                    case ConfigKey.WindowModalWaiting:
                        UIConfig.windowModalWaiting = value.s;
                        break;

                    case ConfigKey.AllowSoftnessOnTopOrLeftSide:
                        UIConfig.allowSoftnessOnTopOrLeftSide = value.b;
                        break;

                    case ConfigKey.InputCaretSize:
                        UIConfig.inputCaretSize = value.i;
                        break;

                    case ConfigKey.InputHighlightColor:
                        UIConfig.inputHighlightColor = value.c;
                        break;

                    case ConfigKey.DepthSupportForPaintingMode:
                        UIConfig.depthSupportForPaintingMode = value.b;
                        break;

                    case ConfigKey.EnhancedTextOutlineEffect:
                        UIConfig.enhancedTextOutlineEffect = value.b;
                        break;

                    case ConfigKey.Branch:
                        UIPackage.branch = value.s;
                        break;
                }
            }
        }

        // [Godot] Converts an inspector value of Items to Unity's ConfigValue layout.
        // int and float fill both i and f (Unity reads DefaultScrollStep/InputCaretSize as int, ButtonSoundVolumeScale as float).
        static ConfigValue ToConfigValue(Variant v)
        {
            ConfigValue value = new ConfigValue();
            value.Reset();

            switch (v.VariantType)
            {
                case Variant.Type.Nil:
                    return value;

                case Variant.Type.String:
                case Variant.Type.StringName:
                    value.s = v.AsString();
                    break;

                case Variant.Type.Bool:
                    value.b = v.AsBool();
                    value.i = value.b ? 1 : 0;
                    break;

                case Variant.Type.Int:
                    value.i = v.AsInt32();
                    value.f = value.i;
                    value.b = value.i != 0;
                    break;

                case Variant.Type.Float:
                    value.f = v.AsSingle();
                    value.i = (int)value.f;
                    break;

                case Variant.Type.Color:
                    value.c = v.AsColor();
                    break;

                default:
                    GD.PushWarning("FairyGUI: UIConfig item value type not supported - " + v.VariantType);
                    return value;
            }

            value.valid = true;
            return value;
        }

        public static void SetDefaultValue(ConfigKey key, ConfigValue value)
        {
            switch (key)
            {
                case ConfigKey.ButtonSoundVolumeScale:
                    value.f = 1;
                    break;

                case ConfigKey.ClickDragSensitivity:
                    value.i = 2;
                    break;

                case ConfigKey.DefaultComboBoxVisibleItemCount:
                    value.i = 10;
                    break;

                case ConfigKey.DefaultScrollBarDisplay:
                    value.i = (int)ScrollBarDisplayType.Default;
                    break;

                case ConfigKey.DefaultScrollBounceEffect:
                case ConfigKey.DefaultScrollTouchEffect:
                    value.b = true;
                    break;

                case ConfigKey.DefaultScrollStep:
                    value.i = 25;
                    break;

                case ConfigKey.ModalLayerColor:
                    value.c = new Color(0f, 0f, 0f, 0.4f);
                    break;

                case ConfigKey.RenderingTextBrighterOnDesktop:
                    value.b = true;
                    break;

                case ConfigKey.TouchDragSensitivity:
                    value.i = 10;
                    break;

                case ConfigKey.TouchScrollSensitivity:
                    value.i = 20;
                    break;

                case ConfigKey.InputCaretSize:
                    value.i = 1;
                    break;

                case ConfigKey.InputHighlightColor:
                    value.c = UColor.FromBytes(255, 223, 141, 128);
                    break;

                case ConfigKey.DepthSupportForPaintingMode:
                    value.b = false;
                    break;

                case ConfigKey.Branch:
                    value.s = "";
                    break;
            }
        }

        public static void ClearResourceRefs()
        {
            UIConfig.defaultFont = "";
            UIConfig.buttonSound = null;
            UIConfig.globalModalWaiting = null;
            UIConfig.horizontalScrollBar = null;
            UIConfig.loaderErrorSign = null;
            UIConfig.popupMenu = null;
            UIConfig.popupMenu_seperator = null;
            UIConfig.tooltipsWin = null;
            UIConfig.verticalScrollBar = null;
            UIConfig.windowModalWaiting = null;
            UIConfig.soundLoader = null;
        }

        public void ApplyModifiedProperties()
        {
            EMRenderSupport.Reload();
        }

        public delegate NAudioClip SoundLoader(string url);

        /// <summary>
        /// 
        /// </summary>
        public static SoundLoader soundLoader = null;

        // [Godot] The UNITY_2019_3_OR_NEWER RuntimeInitializeOnLoadMethod hook (calls ClearResourceRefs() on domain reload)
        // is removed: Godot has no "enter play mode without domain reload"; static state starts fresh each run.
    }
}
