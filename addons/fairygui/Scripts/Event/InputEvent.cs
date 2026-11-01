// Ported from FairyGUI-unity: Assets/Scripts/Event/InputEvent.cs
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    public class InputEvent
    {
        /// <summary>
        /// x position in stage coordinates.
        /// </summary>
        public float x { get; internal set; }

        /// <summary>
        /// y position in stage coordinates.
        /// </summary>
        public float y { get; internal set; }

        /// <summary>
        ///
        /// </summary>
        // [Godot] Unity KeyCode -> Godot.Key (layout-mapped keycode, without modifier bits).
        public Key keyCode { get; internal set; }

        /// <summary>
        ///
        /// </summary>
        public char character { get; internal set; }

        /// <summary>
        ///
        /// </summary>
        // [Godot] Unity EventModifiers -> Godot.KeyModifierMask.
        public KeyModifierMask modifiers { get; internal set; }

        /// <summary>
        ///
        /// </summary>
        public float mouseWheelDelta { get; internal set; }

        /// <summary>
        ///
        /// </summary>
        public int touchId { get; internal set; }

        /// <summary>
        /// -1-none,0-left,1-right,2-middle
        /// </summary>
        public int button { get; internal set; }

        /// <summary>
        ///
        /// </summary>
        /// <value></value>
        public int clickCount { get; internal set; }

        /// <summary>
        /// Duraion of holding the button. You can read this in touchEnd or click event.
        /// </summary>
        /// <value></value>
        public float holdTime { get; internal set; }

        public InputEvent()
        {
            touchId = -1;
            x = 0;
            y = 0;
            clickCount = 0;
            keyCode = Key.None;
            character = '\0';
            modifiers = 0;
            mouseWheelDelta = 0;
        }

        /// <summary>
        ///
        /// </summary>
        public Vector2 position
        {
            get { return new Vector2(x, y); }
        }

        /// <summary>
        ///
        /// </summary>
        public bool isDoubleClick
        {
            get { return clickCount > 1 && button == 0; }
        }

        /// <summary>
        ///
        /// </summary>
        public bool ctrlOrCmd
        {
            get
            {
                return ctrl || command;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public bool ctrl
        {
            get
            {
                // [Godot] Godot.Key has no left/right variants for modifiers; Key.Ctrl covers both.
                return Input.IsKeyPressed(Key.Ctrl);
            }
        }

        /// <summary>
        ///
        /// </summary>
        public bool shift
        {
            get
            {
                // [Godot] Key.Shift covers LeftShift/RightShift.
                return Input.IsKeyPressed(Key.Shift);
            }
        }

        /// <summary>
        ///
        /// </summary>
        public bool alt
        {
            get
            {
                // [Godot] Key.Alt covers LeftAlt/RightAlt.
                return Input.IsKeyPressed(Key.Alt);
            }
        }

        /// <summary>
        ///
        /// </summary>
        public bool command
        {
            get
            {
                // In win, as long as the win key and other keys are pressed at the same time, the getKey will continue to return true. So it can only be shielded.
                // [Godot] RuntimePlatform.OSXPlayer/OSXEditor -> OS.GetName() == "macOS"; LeftCommand/RightCommand -> Key.Meta.
                if (OS.GetName() == "macOS")
                {
                    return Input.IsKeyPressed(Key.Meta);
                }
                else
                    return false;
            }
        }
    }
}
