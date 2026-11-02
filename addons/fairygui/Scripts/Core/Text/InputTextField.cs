// Ported from FairyGUI-unity: Assets/Scripts/Core/Text/InputTextField.cs
using System;
using Godot;
using System.Text;
using FairyGUI.Utils;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// [Godot] Unity has two input paths (legacy Input / FAIRYGUI_INPUT_SYSTEM). Godot has one:
//  - Keys: Stage converts Godot.InputEventKey into InputEvent (keyCode = Godot.Key, character = Unicode)
//    and bubbles onKeyDown to the focus; HandleKey processes command keys and inserts characters.
//  - IME: DisplayServer.WindowSetImeActive/WindowSetImePosition while focused; the composition string is
//    polled from DisplayServer.ImeGetText(); committed IME text arrives as InputEventKey(Unicode).
//  - Clipboard: DisplayServer.ClipboardGet/ClipboardSet.

namespace FairyGUI
{
    /// <summary>
    /// 接收用户输入的文本控件。因为支持直接输入表情，所以从RichTextField派生。
    /// </summary>
    public class InputTextField : RichTextField
    {
        /// <summary>
        ///
        /// </summary>
        public int maxLength { get; set; }

        /// <summary>
        /// 如果是true，则当文本获得焦点时，弹出键盘进行输入，如果是false则不会。
        /// 默认是使用Stage.keyboardInput的值。
        /// </summary>
        public bool keyboardInput { get; set; }

        /// <summary>
        ///
        /// </summary>
        public int keyboardType { get; set; }

        /// <summary>
        ///
        /// </summary>
        public bool hideInput { get; set; }

        /// <summary>
        ///
        /// </summary>
        public bool disableIME { get; set; }

        /// <summary>
        ///
        /// </summary>
        public bool mouseWheelEnabled { get; set; }

        /// <summary>
        ///
        /// </summary>
        public static Action<InputTextField, string> onCopy;

        /// <summary>
        ///
        /// </summary>
        public static Action<InputTextField> onPaste;

        /// <summary>
        ///
        /// </summary>
        public static PopupMenu contextMenu;

        string _text;
        string _restrict;
        Regex _restrictPattern;
        bool _displayAsPassword;
        char _passwordChar; // [Godot] added, see passwordChar
        string _promptText;
        string _decodedPromptText;
        int _border;
        int _corner;
        Color _borderColor;
        Color _backgroundColor;
        bool _editable;

        bool _editing;
        int _caretPosition;
        int _selectionStart;
        int _composing;
        char _highSurrogateChar;
        string _textBeforeEdit;
        bool _usingHtmlInput;

        EventListener _onChanged;
        EventListener _onSubmit;

        Shape _caret;
        SelectionShape _selectionShape;
        float _nextBlink;
        bool _imeWindowFocused; // [Godot] window focus seen last frame, to re-arm the IME after a focus loss

        const int GUTTER_X = 2;
        const int GUTTER_Y = 2;

        public InputTextField()
        {
            node.Name = "InputTextField"; // [Godot] gameObject.name -> node.Name

            _text = string.Empty;
            maxLength = 0;
            _editable = true;
            _composing = 0;
            _passwordChar = '*';
            keyboardInput = Stage.keyboardInput;
            _borderColor = UColor.black;
            _backgroundColor = UColor.clear;
            mouseWheelEnabled = true;
            this.tabStop = true;
            cursor = "text-ibeam";

            /* 因为InputTextField定义了ClipRect，而ClipRect是四周缩进了2个像素的（GUTTER)，默认的点击测试
             * 是使用ClipRect的，那会造成无法点击四周的空白区域。所以这里自定义了一个HitArea
             */
            this.hitArea = new RectHitTest();
            this.touchChildren = false;

            onFocusIn.Add(__focusIn);
            onFocusOut.AddCapture(__focusOut);
            onKeyDown.Add(__keydown);
            onTouchBegin.AddCapture(__touchBegin);
            onTouchMove.AddCapture(__touchMove);
            onMouseWheel.Add(__mouseWheel);
            onClick.Add(__click);
            onRightClick.Add(__rightClick);
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onChanged
        {
            get { return _onChanged ?? (_onChanged = new EventListener(this, "onChanged")); }
        }

        /// <summary>
        ///
        /// </summary>
        public EventListener onSubmit
        {
            get { return _onSubmit ?? (_onSubmit = new EventListener(this, "onSubmit")); }
        }

        /// <summary>
        ///
        /// </summary>
        public override string text
        {
            get
            {
                return _text;
            }
            set
            {
                _text = value;
                ClearSelection();
                UpdateText();
            }
        }

        /// <summary>
        ///
        /// </summary>
        public override TextFormat textFormat
        {
            get
            {
                return base.textFormat;
            }
            set
            {
                base.textFormat = value;
                if (_editing)
                {
                    _caret.height = textField.textFormat.size;
                    _caret.DrawRect(0, UColor.clear, textField.textFormat.color);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public string restrict
        {
            get { return _restrict; }
            set
            {
                _restrict = value;
                if (string.IsNullOrEmpty(_restrict))
                    _restrictPattern = null;
                else
                    _restrictPattern = new Regex(value);
            }
        }

        /// <summary>
        ///
        /// </summary>
        public int caretPosition
        {
            get
            {
                textField.Redraw();
                return _caretPosition;
            }
            set
            {
                SetSelection(value, 0);
            }
        }

        public int selectionBeginIndex
        {
            get { return _selectionStart < _caretPosition ? _selectionStart : _caretPosition; }
        }

        public int selectionEndIndex
        {
            get { return _selectionStart < _caretPosition ? _caretPosition : _selectionStart; }
        }

        /// <summary>
        ///
        /// </summary>
        public string promptText
        {
            get
            {
                return _promptText;
            }
            set
            {
                _promptText = value;
                if (!string.IsNullOrEmpty(_promptText))
                    _decodedPromptText = UBBParser.inst.Parse(XMLUtils.EncodeString(_promptText));
                else
                    _decodedPromptText = null;
                UpdateText();
            }
        }

        /// <summary>
        ///
        /// </summary>
        public bool displayAsPassword
        {
            get { return _displayAsPassword; }
            set
            {
                if (_displayAsPassword != value)
                {
                    _displayAsPassword = value;
                    UpdateText();
                }
            }
        }

        /// <summary>
        /// [Godot] Not in the Unity version (which always uses '*'). Character used to mask the text when displayAsPassword is true.
        /// </summary>
        public char passwordChar
        {
            get { return _passwordChar; }
            set
            {
                if (_passwordChar != value)
                {
                    _passwordChar = value;
                    if (_displayAsPassword)
                        UpdateText();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public bool editable
        {
            get { return _editable; }
            set
            {
                _editable = value;
                if (_caret != null)
                    _caret.visible = _editable;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public int border
        {
            get { return _border; }
            set
            {
                _border = value;
                UpdateShape();
            }
        }

        /// <summary>
        ///
        /// </summary>
        public int corner
        {
            get { return _corner; }
            set
            {
                _corner = value;
                UpdateShape();
            }
        }

        /// <summary>
        ///
        /// </summary>
        public Color borderColor
        {
            get { return _borderColor; }
            set
            {
                _borderColor = value;
                UpdateShape();
            }
        }

        /// <summary>
        ///
        /// </summary>
        public Color backgroundColor
        {
            get { return _backgroundColor; }
            set
            {
                _backgroundColor = value;
                UpdateShape();
            }
        }

        void UpdateShape()
        {
            if (_border > 0 || _backgroundColor.A > 0)
            {
                CreateGraphics();

                graphics.enabled = true;
                RoundedRectMesh mesh = graphics.GetMeshFactory<RoundedRectMesh>();
                mesh.lineWidth = _border;
                mesh.lineColor = _borderColor;
                mesh.fillColor = _backgroundColor;
                mesh.topLeftRadius = mesh.topRightRadius = mesh.bottomLeftRadius = mesh.bottomRightRadius = corner;
                graphics.SetMeshDirty();
            }
            else
            {
                if (graphics != null)
                    graphics.enabled = false;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="start"></param>
        /// <param name="length">-1 means the rest count from start</param>
        public void SetSelection(int start, int length)
        {
            if (!_editing)
                Stage.inst.focus = this;

            _selectionStart = start;
            _caretPosition = length < 0 ? int.MaxValue : (start + length);
            if (!textField.Redraw())
            {
                int cnt = textField.charPositions.Count;
                if (_caretPosition >= cnt)
                    _caretPosition = cnt - 1;
                if (_selectionStart >= cnt)
                    _selectionStart = cnt - 1;
                UpdateCaret();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value"></param>
        public void ReplaceSelection(string value)
        {
            if (keyboardInput && Stage.keyboardInput && !Stage.keyboard.supportsCaret)
            {
                this.text = _text + value;
                OnChanged();
                return;
            }

            if (!_editing)
                Stage.inst.focus = this;

            textField.Redraw();

            int t0, t1;
            if (_selectionStart != _caretPosition)
            {
                if (_selectionStart < _caretPosition)
                {
                    t0 = _selectionStart;
                    t1 = _caretPosition;
                    _caretPosition = _selectionStart;
                }
                else
                {
                    t0 = _caretPosition;
                    t1 = _selectionStart;
                    _selectionStart = _caretPosition;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(value))
                    return;

                t0 = t1 = _caretPosition;
            }

            StringBuilder buffer = new StringBuilder();
            GetPartialText(0, t0, buffer);
            if (!string.IsNullOrEmpty(value))
            {
                value = ValidateInput(value);
                buffer.Append(value);

                _caretPosition += GetTextlength(value);
            }
            GetPartialText(t1 + _composing, -1, buffer);

            string newText = buffer.ToString();
            if (maxLength > 0)
            {
                string newText2 = TruncateText(newText, maxLength);
                if (newText2.Length != newText.Length)
                    _caretPosition += (newText2.Length - newText.Length);
                newText = newText2;
            }

            this.text = newText;
            OnChanged();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value"></param>
        public void ReplaceText(string value)
        {
            if (value == _text)
                return;

            if (value == null)
                value = string.Empty;

            value = ValidateInput(value);

            if (maxLength > 0)
                value = TruncateText(value, maxLength);

            _caretPosition = value.Length;

            this.text = value;
            OnChanged();
        }

        void GetPartialText(int startIndex, int endIndex, StringBuilder buffer)
        {
            int elementCount = textField.htmlElements.Count;
            int lastIndex = startIndex;
            string tt;
            if (_displayAsPassword)
                tt = _text;
            else
                tt = textField.parsedText;
            if (endIndex < 0)
                endIndex = tt.Length;
            for (int i = 0; i < elementCount; i++)
            {
                HtmlElement element = textField.htmlElements[i];
                if (element.htmlObject != null && element.text != null)
                {
                    if (element.charIndex >= startIndex && element.charIndex < endIndex)
                    {
                        buffer.Append(tt.Substring(lastIndex, element.charIndex - lastIndex));
                        buffer.Append(element.text);
                        lastIndex = element.charIndex + 1;
                    }
                }
            }
            if (lastIndex < tt.Length)
                buffer.Append(tt.Substring(lastIndex, endIndex - lastIndex));
        }

        int GetTextlength(string value)
        {
            int textLen = value.Length;
            int ret = textLen;
            for (int i = 0; i < textLen; i++)
            {
                if (char.IsHighSurrogate(value[i]))
                    ret--;
            }
            return ret;
        }

        string TruncateText(string value, int length)
        {
            int textLen = value.Length;
            int len = 0;
            int i = 0;
            while (i < textLen)
            {
                if (len == length)
                    return value.Substring(0, i);

                if (char.IsHighSurrogate(value[i]))
                    i++;
                i++;
                len++;
            }
            return value;
        }

        string ValidateInput(string source)
        {
            if (_restrict != null)
            {
                StringBuilder sb = new StringBuilder();
                Match mc = _restrictPattern.Match(source);
                int lastPos = 0;
                string s;
                while (mc != Match.Empty)
                {
                    if (mc.Index != lastPos)
                    {
                        //保留tab和回车
                        for (int i = lastPos; i < mc.Index; i++)
                        {
                            if (source[i] == '\n' || source[i] == '\t')
                                sb.Append(source[i]);
                        }
                    }

                    s = mc.ToString();
                    lastPos = mc.Index + s.Length;
                    sb.Append(s);

                    mc = mc.NextMatch();
                }
                for (int i = lastPos; i < source.Length; i++)
                {
                    if (source[i] == '\n' || source[i] == '\t')
                        sb.Append(source[i]);
                }

                return sb.ToString();
            }
            else
                return source;
        }

        void UpdateText()
        {
            if (!_editing && _text.Length == 0 && !string.IsNullOrEmpty(_decodedPromptText))
            {
                textField.htmlText = _decodedPromptText;
                return;
            }

            if (_displayAsPassword)
                textField.text = EncodePasswordText(_text);
            else
                textField.text = _text;

            _composing = compositionString.Length;
            if (_composing > 0)
            {
                StringBuilder buffer = new StringBuilder();
                GetPartialText(0, _caretPosition, buffer);
                buffer.Append(compositionString);
                GetPartialText(_caretPosition, -1, buffer);

                textField.text = buffer.ToString();
            }
        }

        string EncodePasswordText(string value)
        {
            int textLen = value.Length;
            StringBuilder tmp = new StringBuilder(textLen);
            int i = 0;
            while (i < textLen)
            {
                char c = value[i];
                if (c == '\n')
                    tmp.Append(c);
                else
                {
                    if (char.IsHighSurrogate(c))
                        i++;
                    tmp.Append(_passwordChar); // [Godot] Unity: "*" (configurable through passwordChar, default '*')
                }
                i++;
            }
            return tmp.ToString();
        }

        void ClearSelection()
        {
            if (_selectionStart != _caretPosition)
            {
                if (_selectionShape != null)
                    _selectionShape.Clear();
                _selectionStart = _caretPosition;
            }
        }

        public string GetSelection()
        {
            if (_selectionStart == _caretPosition)
                return string.Empty;

            StringBuilder buffer = new StringBuilder();
            if (_selectionStart < _caretPosition)
                GetPartialText(_selectionStart, _caretPosition, buffer);
            else
                GetPartialText(_caretPosition, _selectionStart, buffer);
            return buffer.ToString();
        }

        void Scroll(int hScroll, int vScroll)
        {
            vScroll = UMath.Clamp(vScroll, 0, textField.lines.Count - 1);
            TextField.LineInfo line = textField.lines[vScroll];
            hScroll = UMath.Clamp(hScroll, 0, line.charCount - 1);

            TextField.CharPosition cp = GetCharPosition(line.charIndex + hScroll);
            Vector2 pt = GetCharLocation(cp);
            MoveContent(new Vector2(GUTTER_X - pt.X, GUTTER_Y - pt.Y), false);
        }

        void AdjustCaret(TextField.CharPosition cp, bool moveSelectionHeader = false)
        {
            _caretPosition = cp.charIndex;
            if (moveSelectionHeader)
                _selectionStart = _caretPosition;

            UpdateCaret();
        }

        void UpdateCaret(bool forceUpdate = false)
        {
            TextField.CharPosition cp;
            if (_editing)
                cp = GetCharPosition(_caretPosition + compositionString.Length);
            else
                cp = GetCharPosition(_caretPosition);

            Vector2 pos = GetCharLocation(cp);
            TextField.LineInfo line = textField.lines[cp.lineIndex];
            Vector2 offset = pos + textField.xy;

            if (offset.X < textField.textFormat.size)
                offset.X += UMath.Min(50, _contentRect.width * 0.5f);
            else if (offset.X > _contentRect.width - GUTTER_X - textField.textFormat.size)
                offset.X -= UMath.Min(50, _contentRect.width * 0.5f);

            if (offset.X < GUTTER_X)
                offset.X = GUTTER_X;
            else if (offset.X > _contentRect.width - GUTTER_X)
                offset.X = UMath.Max(GUTTER_X, _contentRect.width - GUTTER_X);

            if (offset.Y < GUTTER_Y)
                offset.Y = GUTTER_Y;
            else if (offset.Y + line.height >= _contentRect.height - GUTTER_Y)
                offset.Y = UMath.Max(GUTTER_Y, _contentRect.height - line.height - GUTTER_Y);

            MoveContent(offset - pos, forceUpdate);

            if (_editing)
            {
                _caret.position = (textField.xy + pos).ToV3();
                _caret.height = line.height > 0 ? line.height : textField.textFormat.size;

                if (_editable)
                {
                    // [Godot] Unity: StageCamera.WorldToScreenPoint + Input.compositionCursorPos (with a manual y offset on
                    // Windows). Godot: DisplayServer.WindowSetImePosition at the caret bottom, in window pixels.
                    UpdateIMEPosition();
                }

                _nextBlink = FrameTime.time + 0.5f;
                _caret.graphics.enabled = true;

                UpdateSelection(cp);
            }
        }

        void MoveContent(Vector2 pos, bool forceUpdate)
        {
            float ox = textField.x;
            float oy = textField.y;
            float nx = pos.X;
            float ny = pos.Y;
            float rectWidth = _contentRect.width - 1; //-1 to avoid cursor be clipped
            if (rectWidth - nx > textField.textWidth)
                nx = rectWidth - textField.textWidth;
            if (_contentRect.height - ny > textField.textHeight)
                ny = _contentRect.height - textField.textHeight;
            if (nx > 0)
                nx = 0;
            if (ny > 0)
                ny = 0;
            nx = (int)nx;
            ny = (int)ny;

            if (nx != ox || ny != oy || forceUpdate)
            {
                if (_caret != null)
                {
                    _caret.SetXY(nx + _caret.x - ox, ny + _caret.y - oy);
                    _selectionShape.SetXY(nx, ny);
                }
                textField.SetXY(nx, ny);

                List<HtmlElement> elements = textField.htmlElements;
                int count = elements.Count;
                for (int i = 0; i < count; i++)
                {
                    HtmlElement element = elements[i];
                    if (element.htmlObject != null)
                        element.htmlObject.SetPosition(element.position.X + nx, element.position.Y + ny);
                }
            }
        }

        void UpdateSelection(TextField.CharPosition cp)
        {
            if (_selectionStart == _caretPosition)
            {
                _selectionShape.Clear();
                return;
            }

            TextField.CharPosition start;
            if (_editing && compositionString.Length > 0)
            {
                if (_selectionStart < _caretPosition)
                {
                    cp = GetCharPosition(_caretPosition);
                    start = GetCharPosition(_selectionStart);
                }
                else
                    start = GetCharPosition(_selectionStart + compositionString.Length);
            }
            else
                start = GetCharPosition(_selectionStart);
            if (start.charIndex > cp.charIndex)
            {
                TextField.CharPosition tmp = start;
                start = cp;
                cp = tmp;
            }

            Vector2 v1 = GetCharLocation(start);
            Vector2 v2 = GetCharLocation(cp);

            _selectionShape.rects.Clear();
            textField.GetLinesShape(start.lineIndex, v1.X, cp.lineIndex, v2.X, false, _selectionShape.rects);
            _selectionShape.Refresh();
        }

        TextField.CharPosition GetCharPosition(int caretIndex)
        {
            if (caretIndex < 0)
                caretIndex = 0;
            else if (caretIndex >= textField.charPositions.Count)
                caretIndex = textField.charPositions.Count - 1;

            return textField.charPositions[caretIndex];
        }

        /// <summary>
        /// 通过本地坐标获得字符索引位置
        /// </summary>
        /// <param name="location">本地坐标</param>
        /// <returns></returns>
        TextField.CharPosition GetCharPosition(Vector2 location)
        {
            if (textField.charPositions.Count <= 1)
                return textField.charPositions[0];

            location.X -= textField.x;
            location.Y -= textField.y;

            List<TextField.LineInfo> lines = textField.lines;
            int len = lines.Count;
            TextField.LineInfo line;
            int i;
            for (i = 0; i < len; i++)
            {
                line = lines[i];
                if (line.y + line.height > location.Y)
                    break;
            }
            if (i == len)
                i = len - 1;

            int lineIndex = i;

            len = textField.charPositions.Count;
            TextField.CharPosition v;
            int firstInLine = -1;
            for (i = 0; i < len; i++)
            {
                v = textField.charPositions[i];
                if (v.lineIndex == lineIndex)
                {
                    if (firstInLine == -1)
                        firstInLine = i;
                    if (v.offsetX + v.width * 0.5f > location.X)
                        return v;
                }
                else if (firstInLine != -1)
                {
                    if (textField.parsedText[i - 1] == '\n')
                        return textField.charPositions[i - 1];
                    else
                        return v;
                }
            }

            return textField.charPositions[i - 1];
        }

        /// <summary>
        /// 获得字符的坐标。
        /// </summary>
        /// <param name="cp"></param>
        /// <returns></returns>
        Vector2 GetCharLocation(TextField.CharPosition cp)
        {
            TextField.LineInfo line = textField.lines[cp.lineIndex];
            Vector2 pos = new Vector2();
            if (line.charCount == 0 || textField.charPositions.Count == 0)
            {
                if (textField.align == AlignType.Center)
                    pos.X = (int)(_contentRect.width / 2);
                else
                    pos.X = GUTTER_X;
            }
            else
            {
                TextField.CharPosition v = textField.charPositions[Math.Min(cp.charIndex, textField.charPositions.Count - 1)];
                pos.X = v.offsetX;
            }
            pos.Y = line.y;
            return pos;
        }

        override internal void RefreshObjects()
        {
            base.RefreshObjects();

            if (_editing)
            {
                SetChildIndex(_selectionShape, 0);
                SetChildIndex(_caret, this.numChildren - 1);
            }

            int cnt = textField.charPositions.Count;
            if (_caretPosition >= cnt)
                _caretPosition = cnt - 1;
            if (_selectionStart >= cnt)
                _selectionStart = cnt - 1;

            UpdateCaret(true);
        }

        protected void OnChanged()
        {
            DispatchEvent("onChanged", null);

            TextInputHistory.inst.MarkChanged(this);
        }

        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();

            Rect rect = _contentRect;
            rect.x += GUTTER_X;
            rect.y += GUTTER_Y;
            rect.width -= GUTTER_X * 2;
            rect.height -= GUTTER_Y * 2;
            this.clipRect = rect;
            ((RectHitTest)this.hitArea).rect = _contentRect;
        }

        public override void Update(UpdateContext context)
        {
            // [Godot] Unity reads Input.compositionString live and is told about changes by the input system.
            // Here the composition string is polled once per frame while editing (before base.Update so that
            // the TextField redraw of this frame already shows it).
            if (_editing && RefreshCompositionString())
                CheckComposition();

            base.Update(context);

            if (_editing)
            {
                // [Godot] X11 and Wayland switch the window's IME off when the window loses focus and do not restore
                // it (Windows/macOS keep it). Re-arm it when the window regains focus, as LineEdit does.
                if (_IMEOwner == this)
                {
                    Godot.Window w = GodotObject.IsInstanceValid(node) && node.IsInsideTree() ? node.GetWindow() : null;
                    bool windowFocused = w != null && w.HasFocus();
                    if (windowFocused && !_imeWindowFocused && _IMEWindowId != INVALID_WINDOW_ID)
                    {
                        DisplayServer.WindowSetImeActive(true, _IMEWindowId);
                        UpdateIMEPosition();
                    }
                    _imeWindowFocused = windowFocused;
                }

                if (_nextBlink < FrameTime.time)
                {
                    _nextBlink = FrameTime.time + 0.5f;
                    _caret.graphics.enabled = !_caret.graphics.enabled;
                }
            }
        }

        public override void Dispose()
        {
            if ((_flags & Flags.Disposed) != 0)
                return;

            // [Godot] Unity leaves Input.imeCompositionMode as is. Godot's IME state belongs to the window,
            // so it is switched off if the field is disposed while it still owns it.
            ReleaseIME();

            _editing = false;
            if (_caret != null)
            {
                _caret.Dispose();
                _selectionShape.Dispose();
            }

            base.Dispose();
        }

        void DoCopy(string value)
        {
            if (onCopy != null)
            {
                onCopy(this, value);
                return;
            }


            DisplayServer.ClipboardSet(value); // [Godot] GUIUtility.systemCopyBuffer
        }

        void DoPaste()
        {
            if (onPaste != null)
            {
                onPaste(this);
                return;
            }

            string value = DisplayServer.ClipboardGet(); // [Godot] GUIUtility.systemCopyBuffer
            if (!string.IsNullOrEmpty(value))
                ReplaceSelection(value);
        }

        void CreateCaret()
        {
            _caret = new Shape();
            _caret.node.Name = "Caret"; // [Godot] gameObject.name -> node.Name
            _caret.touchable = false;
            _caret._flags |= Flags.SkipBatching;
            _caret.xy = textField.xy;

            _selectionShape = new SelectionShape();
            _selectionShape.node.Name = "Selection"; // [Godot] gameObject.name -> node.Name
            _selectionShape.color = UIConfig.inputHighlightColor;
            _selectionShape._flags |= Flags.SkipBatching;
            _selectionShape.touchable = false;
            _selectionShape.xy = textField.xy;
        }

        void __touchBegin(EventContext context)
        {
            if (!_editing || textField.charPositions.Count <= 1
                || keyboardInput && Stage.keyboardInput && !Stage.keyboard.supportsCaret
                || context.inputEvent.button != 0)
                return;

            ClearSelection();

            Vector2 v = Stage.inst.touchPosition;
            v = this.GlobalToLocal(v);
            TextField.CharPosition cp = GetCharPosition(v);

            AdjustCaret(cp, true);

            context.CaptureTouch();
        }

        void __touchMove(EventContext context)
        {
            if (!_editing)
                return;

            Vector2 v = Stage.inst.touchPosition;
            v = this.GlobalToLocal(v);
            if (float.IsNaN(v.X))
                return;

            TextField.CharPosition cp = GetCharPosition(v);
            if (cp.charIndex != _caretPosition)
                AdjustCaret(cp);
        }

        void __mouseWheel(EventContext context)
        {
            if (_editing && mouseWheelEnabled)
            {
                context.StopPropagation();

                TextField.CharPosition cp = GetCharPosition(new Vector2(GUTTER_X, GUTTER_Y));
                int vScroll = cp.lineIndex;
                int hScroll = cp.charIndex - textField.lines[cp.lineIndex].charIndex;
                if (context.inputEvent.mouseWheelDelta < 0)
                    vScroll--;
                else
                    vScroll++;
                Scroll(hScroll, vScroll);
            }
        }

        void __focusIn(EventContext context)
        {
            if (Engine.IsEditorHint()) // [Godot] Application.isPlaying
                return;

            _editing = true;
            _textBeforeEdit = _text;

            if (_caret == null)
                CreateCaret();

            if (!string.IsNullOrEmpty(_promptText))
                UpdateText();

            float caretSize;
            //如果界面缩小过，光标很容易看不见，这里放大一下
            if (UIConfig.inputCaretSize == 1 && UIContentScaler.scaleFactor < 1)
                caretSize = UIConfig.inputCaretSize / UIContentScaler.scaleFactor;
            else
                caretSize = UIConfig.inputCaretSize;
            _caret.SetSize(caretSize, textField.textFormat.size);
            _caret.DrawRect(0, UColor.clear, textField.textFormat.color);
            _caret.visible = _editable;
            AddChild(_caret);

            _selectionShape.Clear();
            AddChildAt(_selectionShape, 0);

            if (!textField.Redraw())
            {
                TextField.CharPosition cp = GetCharPosition(_caretPosition);
                AdjustCaret(cp);
            }

            if (Stage.keyboardInput)
            {
                if (keyboardInput)
                {
                    if (_editable)
                        Stage.inst.OpenKeyboard(_text, false, _displayAsPassword ? false : !textField.singleLine,
                            _displayAsPassword, false, null, keyboardType, hideInput);

                    SetSelection(0, -1);
                }
            }
            // [Godot] the WebGL html input branch (FAIRYGUI_WEBGL_TEXT_INPUT) is not ported.
            else
            {
                // [Godot] Unity: Input.imeCompositionMode = On/Off (+ Keyboard.SetIMEEnabled for the input system).
                // Godot: DisplayServer.WindowSetImeActive for the window that shows this field.
                if (!disableIME && !_displayAsPassword)
                    SetIMEActive(true);
                else
                    SetIMEActive(false);

                _composing = 0;

                if ((string)context.data == "key") //select all if got focus by tab key
                    SetSelection(0, -1);

                TextInputHistory.inst.StartRecord(this);
            }
        }

        void __focusOut(EventContext contxt)
        {
            if (!_editing)
                return;

            _editing = false;
            if (_usingHtmlInput)
            {
                _usingHtmlInput = false;
                textField.visible = true;
                _caret.visible = true;
                // [Godot] WebGLTextInput.Stop() (FAIRYGUI_WEBGL_TEXT_INPUT) is not ported; _usingHtmlInput is never set.
            }
            else if (Stage.keyboardInput)
            {
                if (keyboardInput)
                    Stage.inst.CloseKeyboard();
            }
            else
            {
                // [Godot] Unity: Input.imeCompositionMode = IMECompositionMode.Auto (+ Keyboard.SetIMEEnabled(true)).
                // Godot has no "auto" mode, so the IME of the window is switched off (as LineEdit does on focus out).
                // This also drops a composition that was still in progress.
                bool composing = _composing > 0;
                ReleaseIME();
                _composing = 0;

                TextInputHistory.inst.StopRecord(this);

                if (composing && string.IsNullOrEmpty(_promptText))
                    UpdateText(); // [Godot] remove the abandoned composition string from the display
            }

            if (!string.IsNullOrEmpty(_promptText))
                UpdateText();

            _caret.RemoveFromParent();
            _selectionShape.RemoveFromParent();

            if (contextMenu != null && contextMenu.contentPane.onStage)
                contextMenu.Hide();
        }

        void __keydown(EventContext context)
        {
            if (!_editing)
                return;

            if (HandleKey(context.inputEvent))
                context.StopPropagation();
        }

        // [Godot] evt.keyCode is Godot.Key (KeyCode.LeftArrow -> Key.Left, Return -> Enter, KeypadEnter -> KpEnter,
        // PageUp/PageDown -> Pageup/Pagedown); evt.character comes from InputEventKey.Unicode.
        bool HandleKey(InputEvent evt)
        {
            bool keyCodeHandled = true;
            // [Godot] Unity delivers Return/Tab text as a separate character event ('\n'/'\t'). Godot sets Unicode to 0
            // for control keys, so the character is synthesized from the key code (multi-line fields only, as in Unity).
            char synthesizedChar = '\0';
            switch (evt.keyCode)
            {
                case Key.Backspace:
                    {
                        if (evt.command)
                        {
                            //for mac:CMD+Backspace=Delete
                            if (_selectionStart == _caretPosition && _caretPosition < textField.charPositions.Count - 1)
                                _selectionStart = _caretPosition + 1;
                        }
                        else
                        {
                            if (_selectionStart == _caretPosition && _caretPosition > 0)
                                _selectionStart = _caretPosition - 1;
                        }
                        if (_editable)
                            ReplaceSelection(null);
                        break;
                    }

                case Key.Delete:
                    {
                        if (_selectionStart == _caretPosition && _caretPosition < textField.charPositions.Count - 1)
                            _selectionStart = _caretPosition + 1;
                        if (_editable)
                            ReplaceSelection(null);
                        break;
                    }

                case Key.Left:
                    {
                        if (!evt.shift)
                            ClearSelection();
                        if (_caretPosition > 0)
                        {
                            if (evt.command) //mac keyboard
                            {
                                TextField.CharPosition cp = GetCharPosition(_caretPosition);
                                TextField.LineInfo line = textField.lines[cp.lineIndex];
                                cp = GetCharPosition(new Vector2(int.MinValue, line.y + textField.y));
                                AdjustCaret(cp, !evt.shift);
                            }
                            else
                            {
                                TextField.CharPosition cp = GetCharPosition(_caretPosition - 1);
                                AdjustCaret(cp, !evt.shift);
                            }
                        }
                        break;
                    }

                case Key.Right:
                    {
                        if (!evt.shift)
                            ClearSelection();
                        if (_caretPosition < textField.charPositions.Count - 1)
                        {
                            if (evt.command)
                            {
                                TextField.CharPosition cp = GetCharPosition(_caretPosition);
                                TextField.LineInfo line = textField.lines[cp.lineIndex];
                                cp = GetCharPosition(new Vector2(int.MaxValue, line.y + textField.y));
                                AdjustCaret(cp, !evt.shift);
                            }
                            else
                            {
                                TextField.CharPosition cp = GetCharPosition(_caretPosition + 1);
                                AdjustCaret(cp, !evt.shift);
                            }
                        }
                        break;
                    }

                case Key.Up:
                    {
                        if (!evt.shift)
                            ClearSelection();

                        TextField.CharPosition cp = GetCharPosition(_caretPosition);
                        if (cp.lineIndex > 0)
                        {
                            TextField.LineInfo line = textField.lines[cp.lineIndex - 1];
                            cp = GetCharPosition(new Vector2(_caret.x, line.y + textField.y));
                            AdjustCaret(cp, !evt.shift);
                        }
                        break;
                    }

                case Key.Down:
                    {
                        if (!evt.shift)
                            ClearSelection();

                        TextField.CharPosition cp = GetCharPosition(_caretPosition);
                        if (cp.lineIndex == textField.lines.Count - 1)
                            cp.charIndex = textField.charPositions.Count - 1;
                        else
                        {
                            TextField.LineInfo line = textField.lines[cp.lineIndex + 1];
                            cp = GetCharPosition(new Vector2(_caret.x, line.y + textField.y));
                        }
                        AdjustCaret(cp, !evt.shift);
                        break;
                    }

                case Key.Pageup:
                    {
                        ClearSelection();
                        break;
                    }

                case Key.Pagedown:
                    {
                        ClearSelection();
                        break;
                    }

                case Key.Home:
                    {
                        if (!evt.shift)
                            ClearSelection();

                        TextField.CharPosition cp = GetCharPosition(_caretPosition);
                        TextField.LineInfo line = textField.lines[cp.lineIndex];
                        cp = GetCharPosition(new Vector2(int.MinValue, line.y + textField.y));
                        AdjustCaret(cp, !evt.shift);
                        break;
                    }

                case Key.End:
                    {
                        if (!evt.shift)
                            ClearSelection();

                        TextField.CharPosition cp = GetCharPosition(_caretPosition);
                        TextField.LineInfo line = textField.lines[cp.lineIndex];
                        cp = GetCharPosition(new Vector2(int.MaxValue, line.y + textField.y));
                        AdjustCaret(cp, !evt.shift);

                        break;
                    }

                //Select All
                case Key.A:
                    {
                        if (evt.ctrlOrCmd)
                        {
                            _selectionStart = 0;
                            AdjustCaret(GetCharPosition(int.MaxValue));
                        }
                        break;
                    }

                //Copy
                case Key.C:
                    {
                        if (evt.ctrlOrCmd && !_displayAsPassword)
                        {
                            string s = GetSelection();
                            if (!string.IsNullOrEmpty(s))
                                DoCopy(s);
                        }
                        break;
                    }

                //Paste
                case Key.V:
                    {
                        if (evt.ctrlOrCmd && _editable)
                            DoPaste();
                        break;
                    }

                //Cut
                case Key.X:
                    {
                        if (evt.ctrlOrCmd && !_displayAsPassword)
                        {
                            string s = GetSelection();
                            if (!string.IsNullOrEmpty(s))
                            {
                                DoCopy(s);
                                if (_editable)
                                    ReplaceSelection(null);
                            }
                        }
                        break;
                    }

                case Key.Z:
                    {
                        if (evt.ctrlOrCmd && _editable)
                        {
                            if (evt.shift)
                                TextInputHistory.inst.Redo(this);
                            else
                                TextInputHistory.inst.Undo(this);
                        }
                        break;
                    }

                case Key.Y:
                    {
                        if (evt.ctrlOrCmd && _editable)
                            TextInputHistory.inst.Redo(this);
                        break;
                    }

                case Key.Enter:
                case Key.KpEnter:
                    {
                        if (textField.singleLine)
                        {
                            Stage.inst.focus = parent;
                            DispatchEvent("onSubmit", null);
                            DispatchEvent("onKeyDown", null); //for backward compatibility
                        }
                        else if (!evt.ctrlOrCmd && !evt.alt)
                            synthesizedChar = '\n'; // [Godot] see synthesizedChar
                        break;
                    }

                case Key.Tab:
                    {
                        if (textField.singleLine)
                        {
                            Stage.inst.DoKeyNavigate(evt.shift);
                            keyCodeHandled = false;
                        }
                        else if (!evt.ctrlOrCmd && !evt.alt)
                            synthesizedChar = evt.shift ? (char)25 : '\t'; // [Godot] see synthesizedChar (25 = shift+tab)
                        break;
                    }

                case Key.Escape:
                    {
                        this.text = _textBeforeEdit;
                        Stage.inst.focus = parent;
                        break;
                    }

                default:
                    // [Godot] Unity: (int)evt.keyCode <= 272 (see IsUnityTextKey).
                    keyCodeHandled = IsUnityTextKey(evt.keyCode) && !evt.ctrlOrCmd;
                    break;
            }

            char c = evt.character;
            if (c == 0)
                c = synthesizedChar; // [Godot] Stage may already have put '\n'/'\t' into evt.character; never insert twice
            if (c != 0)
            {
                // [Godot] Godot has a single input path: the FAIRYGUI_INPUT_SYSTEM branch (keydown/onTextInput frame
                // de-duplication) is not needed because the character is carried by the same InputEventKey.
                if (!evt.ctrlOrCmd)
                    HandleTextInput(c);

                // [Godot] Unity received the key code and the character as two KeyDown events: the character event
                // always stopped propagation, the key code event followed keyCodeHandled. Godot merges both into one
                // event, so an event that has a key code keeps the key code result (single-line Tab and unhandled
                // Ctrl/Cmd shortcuts still bubble); a pure character event (no key code) is consumed as in Unity.
                if (evt.keyCode == Key.None || evt.keyCode == Key.Unknown)
                    return true;
                else
                    return keyCodeHandled;
            }
            else
                return keyCodeHandled;
        }

        void HandleTextInput(char c)
        {
            if (c == '\r' || c == 3)
                c = '\n';

            if (c == 25)/*shift+tab*/
                c = '\t';

            if (c == 27/*escape*/ || textField.singleLine && (c == '\n' || c == '\t'))
                return;

            if (_editable)
            {
                if (char.IsHighSurrogate(c))
                {
                    _highSurrogateChar = c;
                    return;
                }

                if (char.IsLowSurrogate(c))
                    ReplaceSelection(char.ConvertFromUtf32(((int)c & 0x03FF) + ((((int)_highSurrogateChar & 0x03FF) + 0x40) << 10)));
                else
                    ReplaceSelection(c.ToString());
            }
        }

        void CheckComposition()
        {
            if (!_editable || keyboardInput)
                return;

            if (compositionString.Length == 0)
            {
                UpdateText();
            }
            else
            {
                int composing = _composing;
                _composing = compositionString.Length;

                StringBuilder buffer = new StringBuilder();
                GetPartialText(0, _caretPosition, buffer);
                buffer.Append(compositionString);
                GetPartialText(_caretPosition + composing, -1, buffer);

                textField.text = buffer.ToString();
            }
        }

        void __click(EventContext context)
        {
            if (_editing && context.inputEvent.isDoubleClick)
            {
                context.StopPropagation();
                _selectionStart = 0;
                AdjustCaret(GetCharPosition(int.MaxValue));
            }
        }

        void __rightClick(EventContext context)
        {
            if (contextMenu != null)
            {
                context.StopPropagation();
                contextMenu.Show();
            }
        }

        // [Godot] Unity has two variants here: the legacy one (EatKeyEvent(Event) + Input.compositionString) and
        // FAIRYGUI_INPUT_SYSTEM (RegisterEvent/UnregisterEvent + onIMECompositionChange/onTextInput). The Godot port
        // follows the structure of the second one:
        //  - the composition string is read with DisplayServer.ImeGetText(). It is refreshed when a key event arrives
        //    (EatKeyEvent) and once per frame while a field is being edited (Update). StageEngine does not forward
        //    NotificationOsImeUpdate: polling covers it (HandleImeUpdate is kept for a host that wants to call it);
        //  - committed text arrives as InputEventKey(Unicode) through Stage, there is no separate onTextInput;
        //  - the platform layers (Windows/macOS/X11) already drop the key events consumed by the IME, so a key event is
        //    only eaten while a composition string is visible.
        static string _compositionString = string.Empty;
        static bool _IMEEventRegistered = true;
        static InputTextField _IMEOwner;
        const int INVALID_WINDOW_ID = (int)DisplayServer.InvalidWindowId; // [Godot] the constant is a long, window ids are int
        static int _IMEWindowId = INVALID_WINDOW_ID;

        /// <summary>
        /// [Godot] Unity (FAIRYGUI_INPUT_SYSTEM) subscribes to the keyboard IME/text events here. Godot delivers them to
        /// StageEngine, so this only (re-)enables reading the IME composition string. Enabled by default.
        /// </summary>
        public static void RegisterEvent()
        {
            _IMEEventRegistered = true;
        }

        /// <summary>
        /// [Godot] Stops reading the IME composition string. Committed text still arrives as key events.
        /// </summary>
        public static void UnregisterEvent()
        {
            _IMEEventRegistered = false;
            if (RefreshCompositionString() && _IMEOwner != null)
                _IMEOwner.CheckComposition();
        }

        /// <summary>
        /// [Godot] Unity: EatKeyEvent(UnityEngine.Event). Stage calls it for every Godot key event (pressed and released)
        /// before dispatching it. Returns true if the event belongs to the IME and must not be dispatched.
        /// </summary>
        public static bool EatKeyEvent(InputEventKey evt)
        {
            // [Godot] read the composition string again: the IME may have committed or cancelled since the last refresh,
            // and the committed characters that follow must not be eaten.
            if (RefreshCompositionString())
            {
                var focus = Stage.inst.focus;
                if (focus is InputTextField)
                    ((InputTextField)focus).CheckComposition();
            }

            // [Godot] A pressed key with a Unicode character is committed text: the platform layers already drop raw
            // keys while composing. Godot dispatches key events after the OS pump, so when the same pump also started
            // the next composition (Korean syllables, Japanese implicit confirmation), ImeGetText() is already non-empty
            // here: eating the commit would lose the text.
            return compositionString.Length > 0 && !(evt.Pressed && evt.Unicode != 0);
        }

        /// <summary>
        /// [Godot] Refreshes the composition now (Unity FAIRYGUI_INPUT_SYSTEM: OnIMECompositionChange). Not called by
        /// StageEngine: the focused field polls DisplayServer.ImeGetText() every frame. Call it from
        /// NotificationOsImeUpdate if a host needs the composition earlier than the next frame.
        /// </summary>
        internal static void HandleImeUpdate()
        {
            if (Stage.keyboardInput)
                return;

            if (RefreshCompositionString())
            {
                var focus = Stage.inst.focus;
                if (focus is InputTextField)
                    ((InputTextField)focus).CheckComposition();
            }
        }

        /// <summary>
        /// The current IME composition string being typed by the user.
        /// </summary>
        public static string compositionString
        {
            get
            {
                if (Stage.keyboardInput)
                    return String.Empty;

                return _compositionString;
            }
        }

        // [Godot] Reads the composition string of the window whose IME was turned on by a focused InputTextField.
        // Returns true if it changed.
        static bool RefreshCompositionString()
        {
            string s;
            if (_IMEOwner != null && _IMEEventRegistered && !Stage.keyboardInput)
                s = DisplayServer.ImeGetText() ?? string.Empty;
            else
                s = string.Empty;

            if (s == _compositionString)
                return false;

            _compositionString = s;
            return true;
        }

        // [Godot] Unity: Input.imeCompositionMode = On / Off (focus in). Godot's IME state belongs to a window, so the
        // field that turned it on is remembered as the owner. A field getting the focus always takes over.
        void SetIMEActive(bool value)
        {
            if (!DisplayServer.HasFeature(DisplayServer.Feature.Ime))
                return;

            int windowId = GetIMEWindowId();
            if (_IMEOwner != null && _IMEWindowId != windowId && _IMEWindowId != INVALID_WINDOW_ID)
                DisplayServer.WindowSetImeActive(false, _IMEWindowId); //the previous owner is shown in another window

            if (value && windowId != INVALID_WINDOW_ID)
            {
                _IMEOwner = this;
                _IMEWindowId = windowId;
                _compositionString = string.Empty; //a newly focused field starts without composition (_composing = 0)
                DisplayServer.WindowSetImeActive(true, windowId);
                UpdateIMEPosition();
            }
            else
            {
                _IMEOwner = null;
                _IMEWindowId = INVALID_WINDOW_ID;
                if (windowId != INVALID_WINDOW_ID)
                    DisplayServer.WindowSetImeActive(false, windowId);
                RefreshCompositionString(); //no owner: the composition string becomes empty
            }
        }

        // [Godot] Unity: Input.imeCompositionMode = Auto (focus out). Godot has no "auto" mode, so the IME of the window
        // is switched off, as LineEdit does. Only the owner does it: a field removed from the stage while focused gets
        // no onFocusOut and must not switch off the IME of the field focused after it.
        void ReleaseIME()
        {
            if (_IMEOwner != this)
                return;

            int windowId = _IMEWindowId;
            _IMEOwner = null;
            _IMEWindowId = INVALID_WINDOW_ID;
            if (windowId != INVALID_WINDOW_ID && DisplayServer.HasFeature(DisplayServer.Feature.Ime))
                DisplayServer.WindowSetImeActive(false, windowId);
            RefreshCompositionString(); //no owner: the composition string becomes empty
        }

        // [Godot] The id of the native window that displays this field (Unity has a single screen).
        int GetIMEWindowId()
        {
            if (!GodotObject.IsInstanceValid(node) || !node.IsInsideTree())
                return INVALID_WINDOW_ID;

            Godot.Window window = node.GetWindow();
            if (window == null)
                return INVALID_WINDOW_ID;

            return window.GetWindowId();
        }

        // [Godot] Unity: Input.compositionCursorPos = StageCamera.main.WorldToScreenPoint(caret bottom-left).
        // Godot: DisplayServer.WindowSetImePosition in window pixels. The caret is converted with its canvas transform
        // (CanvasLayer included) and the window's screen transform (stretch), like LineEdit does.
        // [Godot] approximation: a field rendered inside a SubViewport is positioned relative to the SubViewport.
        void UpdateIMEPosition()
        {
            if (_IMEOwner != this || _caret == null)
                return;

            DisplayObjectNode caretNode = _caret.node;
            if (!GodotObject.IsInstanceValid(caretNode) || !caretNode.IsInsideTree())
                return;

            Godot.Window window = caretNode.GetWindow();
            if (window == null)
                return;

            Vector2 cursorPos = caretNode.GetGlobalTransformWithCanvas() * new Vector2(0, _caret.height);
            cursorPos = window.GetScreenTransform() * cursorPos;
            DisplayServer.WindowSetImePosition(new Vector2I(UMath.RoundToInt(cursorPos.X), UMath.RoundToInt(cursorPos.Y)), _IMEWindowId);
        }

        // [Godot] Unity: (int)evt.keyCode <= 272, i.e. every key that types a character (KeyCode.None, Backspace, Tab,
        // Return, Escape, Space ... Delete, Keypad0-9, keypad operators, KeypadEnter, KeypadEquals) as opposed to arrows,
        // Insert/Home/End/PageUp/PageDown, F-keys, lock keys and modifiers. In Godot every character key has a value
        // below Key.Special (printable characters use their Unicode value); the remaining ones are listed explicitly.
        static bool IsUnityTextKey(Key keyCode)
        {
            if ((keyCode & Key.Special) == 0)
                return true;

            switch (keyCode)
            {
                case Key.Escape:
                case Key.Tab:
                case Key.Backtab:
                case Key.Backspace:
                case Key.Enter:
                case Key.KpEnter:
                case Key.Delete:
                case Key.Pause:
                case Key.Clear:
                case Key.Kp0:
                case Key.Kp1:
                case Key.Kp2:
                case Key.Kp3:
                case Key.Kp4:
                case Key.Kp5:
                case Key.Kp6:
                case Key.Kp7:
                case Key.Kp8:
                case Key.Kp9:
                case Key.KpPeriod:
                case Key.KpDivide:
                case Key.KpMultiply:
                case Key.KpSubtract:
                case Key.KpAdd:
                case Key.Unknown: //unmapped key carrying a character (e.g. committed IME text): Unity KeyCode.None
                    return true;
                default:
                    return false;
            }
        }
    }

    class TextInputHistory
    {
        static TextInputHistory _inst;
        public static TextInputHistory inst
        {
            get
            {
                if (_inst == null)
                    _inst = new TextInputHistory();
                return _inst;
            }
        }

        List<string> _undoBuffer;
        List<string> _redoBuffer;
        string _currentText;
        InputTextField _textField;
        bool _lock;
        int _changedFrame;

        public const int maxHistoryLength = 5;

        public TextInputHistory()
        {
            _undoBuffer = new List<string>();
            _redoBuffer = new List<string>();
        }

        public void StartRecord(InputTextField textField)
        {
            _undoBuffer.Clear();
            _redoBuffer.Clear();
            _textField = textField;
            _lock = false;
            _currentText = textField.text;
            _changedFrame = 0;
        }

        public void MarkChanged(InputTextField textField)
        {
            if (_textField != textField)
                return;

            if (_lock)
                return;

            string newText = _textField.text;
            if (_currentText == newText)
                return;

            if (_changedFrame != FrameTime.frameCount)
            {
                _changedFrame = FrameTime.frameCount;
                _undoBuffer.Add(_currentText);
                if (_undoBuffer.Count > maxHistoryLength)
                    _undoBuffer.RemoveAt(0);
            }
            else
            {
                int cnt = _undoBuffer.Count;
                if (cnt > 0 && newText == _undoBuffer[cnt - 1])
                    _undoBuffer.RemoveAt(cnt - 1);
            }
            _currentText = newText;
        }

        public void StopRecord(InputTextField textField)
        {
            if (_textField != textField)
                return;

            _undoBuffer.Clear();
            _redoBuffer.Clear();
            _textField = null;
            _currentText = null;
        }

        public void Undo(InputTextField textField)
        {
            if (_textField != textField)
                return;

            if (_undoBuffer.Count == 0)
                return;

            string text = _undoBuffer[_undoBuffer.Count - 1];
            _undoBuffer.RemoveAt(_undoBuffer.Count - 1);
            _redoBuffer.Add(_currentText);
            _lock = true;
            int caretPos = _textField.caretPosition;
            _textField.text = text;
            int dlen = text.Length - _currentText.Length;
            if (dlen < 0)
                _textField.caretPosition = caretPos + dlen;
            _currentText = text;
            _lock = false;
        }

        public void Redo(InputTextField textField)
        {
            if (_textField != textField)
                return;

            if (_redoBuffer.Count == 0)
                return;

            string text = _redoBuffer[_redoBuffer.Count - 1];
            _redoBuffer.RemoveAt(_redoBuffer.Count - 1);
            _undoBuffer.Add(_currentText);
            _lock = true;
            int caretPos = _textField.caretPosition;
            _textField.text = text;
            int dlen = text.Length - _currentText.Length;
            if (dlen > 0)
                _textField.caretPosition = caretPos + dlen;
            _currentText = text;
            _lock = false;
        }
    }
}

