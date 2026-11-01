// Ported from FairyGUI-unity: Assets/Scripts/Core/Text/TouchScreenKeyboard.cs
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    // [Godot] Unity wraps UnityEngine.TouchScreenKeyboard, which owns its own text buffer and reports the whole
    // text back through GetInput. Godot shows its virtual keyboard with DisplayServer.VirtualKeyboardShow/Hide and
    // delivers the typed text in a platform dependent way:
    //  - Android/iOS: every typed character, Backspace and Enter arrive as ordinary InputEventKey events, which Stage
    //    forwards to the focused InputTextField (HandleKey). supportsCaret is true and GetInput returns null.
    //  - Web (export option "experimental virtual keyboard"): the whole text of the hidden html input element arrives
    //    through the window's input text callback. supportsCaret is false and GetInput returns that text, as in Unity.
    public class TouchScreenKeyboard : IKeyboard
    {
        // [Godot] replaces the UnityEngine.TouchScreenKeyboard instance: true between Open and Close/done.
        bool _opened;
        // [Godot] the keyboard is considered closed by the user only after it has been seen with a positive height.
        bool _heightSeen;
        // [Godot] Web only: text received from the input text callback and not yet returned by GetInput.
        string _webText;

        static readonly bool _isWeb = OS.HasFeature("web");
        static TouchScreenKeyboard _webActive;
        static bool _webCallbackCreated;
        static Callable _webTextCallback;

        public bool done
        {
            get
            {
                // [Godot] Godot has no keyboard status (Done/Canceled/LostFocus). The keyboard is treated as done
                // when it was visible (VirtualKeyboardGetHeight() > 0) and has been hidden again by the user.
                // A hardware keyboard keeps the height at 0, so it never reports done (focus-out still closes it).
                // Web does not report the keyboard height: there the keyboard is closed by the focus-out only.
                if (!_opened)
                    return true;

                if (_isWeb || !DisplayServer.HasFeature(DisplayServer.Feature.VirtualKeyboard))
                    return false;

                int height = DisplayServer.VirtualKeyboardGetHeight();
                if (height > 0)
                {
                    _heightSeen = true;
                    return false;
                }

                return _heightSeen;
            }
        }

        public bool supportsCaret
        {
            // [Godot] Unity: false (GetInput returns the whole text). Android/iOS edit at the caret through key events,
            // so the caret is supported there. Web returns the whole text, as Unity does.
            get { return !_isWeb; }
        }

        public string GetInput()
        {
            if (_opened)
            {
                // [Godot] Android/iOS: no text buffer to read, the typed text is delivered as InputEventKey and handled
                // by InputTextField.HandleKey. Web: the whole text, only when it changed since the last call.
                string s = _webText;
                _webText = null;

                if (this.done)
                {
                    _opened = false;
                    _heightSeen = false;
                }

                return s;
            }
            else
                return null;
        }

        public void Open(string text, bool autocorrection, bool multiline, bool secure, bool alert, string textPlaceholder, int keyboardType, bool hideInput)
        {
            if (_opened)
                return;

            // [Godot] hideInput, autocorrection, alert and textPlaceholder have no Godot equivalent and are ignored.
            _opened = true;
            _heightSeen = false;
            _webText = null;

            if (!DisplayServer.HasFeature(DisplayServer.Feature.VirtualKeyboard))
                return;

            if (_isWeb)
            {
                InstallWebTextCallback();
                _webActive = this;
            }

            DisplayServer.VirtualKeyboardType type;
            if (secure)
                type = DisplayServer.VirtualKeyboardType.Password;
            else
            {
                type = ToVirtualKeyboardType(keyboardType);
                if (type == DisplayServer.VirtualKeyboardType.Default && multiline)
                    type = DisplayServer.VirtualKeyboardType.Multiline;
            }

            if (text == null)
                text = string.Empty;

            // [Godot] InputTextField selects the whole text right after opening the keyboard (SetSelection(0, -1)),
            // so the native edit box is opened with the same selection to keep both sides in sync.
            DisplayServer.VirtualKeyboardShow(text, null, type, -1, 0, text.Length);
        }

        public void Close()
        {
            if (_opened)
            {
                if (DisplayServer.HasFeature(DisplayServer.Feature.VirtualKeyboard))
                    DisplayServer.VirtualKeyboardHide();
                _opened = false;
                _heightSeen = false;
                _webText = null;
            }

            if (_webActive == this)
                _webActive = null;
        }

        // [Godot] Web only. Godot's main Window registers its own input text callback, which passes the text to the
        // focused Godot Control (LineEdit/TextEdit "_set_text"). It is replaced here by a callback that keeps the text
        // for the open FairyGUI keyboard and otherwise does what the Window does, so Godot controls keep working.
        // It is installed again on every Open because the Window registers its callback again when it is shown.
        static void InstallWebTextCallback()
        {
            if (!_webCallbackCreated)
            {
                _webTextCallback = Callable.From<string, bool>(OnWebInputText);
                _webCallbackCreated = true;
            }
            DisplayServer.WindowSetInputTextCallback(_webTextCallback, (int)DisplayServer.MainWindowId);
        }

        static void OnWebInputText(string text, bool emitSignal)
        {
            if (_webActive != null && _webActive._opened)
            {
                _webActive._webText = text ?? string.Empty;
                return;
            }

            // [Godot] same as Window._window_input_text -> Viewport._push_text_input.
            // [Godot] approximation: an embedded sub-window that has the focus receives the text through
            // Viewport.PushTextInput, which does not emit the text_changed signal.
            SceneTree tree = Engine.GetMainLoop() as SceneTree;
            if (tree == null || tree.Root == null)
                return;

            Godot.Control focusOwner = tree.Root.GuiGetFocusOwner();
            if (focusOwner != null && focusOwner.HasMethod("_set_text"))
                focusOwner.Call("_set_text", text, emitSignal);
            else
                tree.Root.PushTextInput(text);
        }

        // [Godot] maps UnityEngine.TouchScreenKeyboardType values (the int stored by the FairyGUI editor)
        // to Godot's DisplayServer.VirtualKeyboardType.
        static DisplayServer.VirtualKeyboardType ToVirtualKeyboardType(int keyboardType)
        {
            switch (keyboardType)
            {
                case 2: //NumbersAndPunctuation
                case 11: //DecimalPad
                    return DisplayServer.VirtualKeyboardType.NumberDecimal;
                case 3: //URL
                    return DisplayServer.VirtualKeyboardType.Url;
                case 4: //NumberPad
                    return DisplayServer.VirtualKeyboardType.Number;
                case 5: //PhonePad
                case 6: //NamePhonePad
                    return DisplayServer.VirtualKeyboardType.Phone;
                case 7: //EmailAddress
                    return DisplayServer.VirtualKeyboardType.EmailAddress;
                default:
                    return DisplayServer.VirtualKeyboardType.Default;
            }
        }
    }
}
