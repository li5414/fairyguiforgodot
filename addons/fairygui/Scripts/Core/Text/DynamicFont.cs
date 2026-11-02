// Ported from FairyGUI-unity: Assets/Scripts/Core/Text/DynamicFont.cs
using System;
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    /// <remarks>
    /// [Godot] Rewritten around Godot.Font (FontFile / SystemFont / FontVariation). Unity requests characters into the
    /// font texture and writes glyph quads into the VertexBuffer; here Godot's TextServer owns rasterization, glyph
    /// cache, atlas pages, MSDF and fallback fonts:
    /// - metrics: Font.GetCharSize / GetAscent / GetHeight / GetUnderlinePosition at the requested pixel size;
    /// - drawing: DrawGlyph/DrawLine record TextField.GlyphRun entries; TextField draws them with Font.DrawChar,
    ///   Font.DrawCharOutline and RenderingServer.CanvasItemAddRect in OnNodeDraw;
    /// - bold/italic: cached FontVariation (VariationEmbolden, VariationTransform skew) per style.
    /// Font.DrawChar only uses the font and its configured fallbacks (Font.Fallbacks, the fallbacks of a FontFile, the
    /// default theme font for SystemFont); unlike Unity's OS fonts it does not search other OS fonts for missing glyphs.
    /// </remarks>
    public class DynamicFont : BaseFont
    {
        Godot.Font _font;
        int _size;
        float _ascent;
        float _lineHeight;
        float _scale;
        TextFormat _format;
        FontStyle _style;
        bool _boldVertice;
        int _char; // [Godot] Unity: CharacterInfo _char. The code point of the last GetGlyph.
        Godot.Font _styledFont; // [Godot] font used for the current style (see GetStyledFont)
        Color _runColor; // [Godot] DrawChar takes one modulate color
        List<TextField.GlyphRun> _glyphRuns; // [Godot] target of DrawGlyph/DrawLine (see StartDraw)
        Godot.Font[] _styledFonts; // [Godot] cached FontVariation per FontStyle
        int _metricsSize; // [Godot] the pixel size _ascent/_lineHeight were read for
        Action _textureRebuildCallback;

        /// <summary>
        /// [Godot] Unity: UnityEngine.FontStyle.
        /// </summary>
        enum FontStyle
        {
            Normal = 0,
            Bold = 1,
            Italic = 2,
            BoldAndItalic = 3
        }

        // [Godot] Synthetic styles, same values as the Godot editor fonts (editor_fonts.cpp).
        const float BoldEmbolden = 0.6f;
        const float ItalicSkew = 0.2f;

        public DynamicFont()
        {
            this.canTint = true;
            this.keepCrisp = true;
            this.customOutline = true;
            this.shader = ShaderConfig.textShader;

            _textureRebuildCallback = textureRebuildCallback;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="name"></param>
        /// <param name="font"></param>
        /// <returns></returns>
        public DynamicFont(string name, Godot.Font font) : this()
        {
            this.name = name;
            this.nativeFont = font;
        }

        override public void Dispose()
        {
            if (_font != null && GodotObject.IsInstanceValid(_font))
                _font.Changed -= _textureRebuildCallback;
            _glyphRuns = null;
        }

        public Godot.Font nativeFont
        {
            get { return _font; }
            set
            {
                if (_font != null && GodotObject.IsInstanceValid(_font))
                    _font.Changed -= _textureRebuildCallback;

                _font = value;
                // [Godot] Unity listens to Font.textureRebuilt. Godot rebuilds its glyph atlas transparently; Resource.Changed
                // is emitted when the font itself changes (properties, base font, fallbacks), which may change the metrics.
                if (_font != null)
                    _font.Changed += _textureRebuildCallback;

                // [Godot] The glyph textures belong to the TextServer and are passed by Font.DrawChar. The mesh of a
                // TextField that uses a DynamicFont has no glyph quads, so the shared empty texture is used.
                mainTexture = NTexture.Empty;

                _styledFonts = new Godot.Font[4];
                _metricsSize = 0;
            }
        }

        override public void SetFormat(TextFormat format, float fontSizeScale)
        {
            _format = format;
            float size = format.size * fontSizeScale;
            if (keepCrisp)
                size *= UIContentScaler.scaleFactor;
            if (_format.specialStyle == TextFormat.SpecialStyle.Subscript || _format.specialStyle == TextFormat.SpecialStyle.Superscript)
                size *= SupScale;
            _size = UMath.FloorToInt(size);
            if (_size == 0)
                _size = 1;
            // [Godot] Unity: _scale = _size / _font.fontSize, with _ascent = fontSize and _lineHeight = fontSize * 1.25
            // (approximations, the Unity font metrics are unreliable). Godot reports real metrics for every pixel size,
            // so _ascent/_lineHeight are the metrics at _size and _scale is 1.
            UpdateMetrics();
            _scale = 1;

            if (format.bold && !customBold)
            {
                if (format.italic)
                {
                    if (customBoldAndItalic)
                        _style = FontStyle.Italic;
                    else
                        _style = FontStyle.BoldAndItalic;
                }
                else
                    _style = FontStyle.Bold;
            }
            else
            {
                if (format.italic)
                    _style = FontStyle.Italic;
                else
                    _style = FontStyle.Normal;
            }
            _styledFont = GetStyledFont(_style);

            _boldVertice = format.bold && (customBold || (format.italic && customBoldAndItalic));
            format.FillVertexColors(vertexColors);

            // [Godot] Font.DrawChar has one modulate color per glyph. Gradient glyphs are drawn as quads with the 4 vertex
            // colors (TextField.DrawGradientGlyph); this average is the fallback when that is not possible.
            if (format.gradientColor == null)
                _runColor = format.color;
            else
                _runColor = new Color(
                    (vertexColors[0].R + vertexColors[1].R + vertexColors[2].R + vertexColors[3].R) / 4,
                    (vertexColors[0].G + vertexColors[1].G + vertexColors[2].G + vertexColors[3].G) / 4,
                    (vertexColors[0].B + vertexColors[1].B + vertexColors[2].B + vertexColors[3].B) / 4,
                    (vertexColors[0].A + vertexColors[1].A + vertexColors[2].A + vertexColors[3].A) / 4);
        }

        override public void PrepareCharacters(string text, TextFormat format, float fontSizeScale)
        {
            SetFormat(format, fontSizeScale);

            // [Godot] Unity: _font.RequestCharactersInTexture(text, _size, _style). Godot rasterizes glyphs on demand
            // (first GetCharSize/DrawChar of a glyph at a size), nothing to request.
        }

        override public bool GetGlyph(char ch, out float width, out float height, out float baseline)
        {
            if (!_styledFont.HasChar(ch))
            {
                if (ch == ' ')
                {
                    //space may not be prepared, try again
                    // [Godot] Godot has nothing to prepare. A font without a space glyph: the space has no width,
                    // like a CharacterInfo that could not be requested in Unity.
                    _char = ch;
                    width = 0;
                }
                else
                {
                    width = height = baseline = 0;
                    return false;
                }
            }
            else
            {
                _char = ch;
                width = _styledFont.GetCharSize(ch, _size).X;
            }

            height = _lineHeight * _scale;
            baseline = _ascent * _scale;
            if (_boldVertice)
                width++;

            if (_format.specialStyle == TextFormat.SpecialStyle.Subscript)
            {
                height /= SupScale;
                baseline /= SupScale;
            }
            else if (_format.specialStyle == TextFormat.SpecialStyle.Superscript)
            {
                height = height / SupScale + baseline * SupOffset;
                baseline *= (SupOffset + 1 / SupScale);
            }

            height = UMath.RoundToInt(height);
            baseline = UMath.RoundToInt(baseline);

            if (keepCrisp)
            {
                width /= UIContentScaler.scaleFactor;
                height /= UIContentScaler.scaleFactor;
                baseline /= UIContentScaler.scaleFactor;
            }

            return true;
        }

        static Color[] vertexColors = new Color[4]; // [Godot] Color32[] -> Color[]

        /// <summary>
        /// [Godot] Remembers where DrawGlyph/DrawLine record their runs.
        /// </summary>
        override public void StartDraw(NGraphics graphics, List<TextField.GlyphRun> glyphRuns)
        {
            _glyphRuns = glyphRuns;
            StartDraw(graphics);
        }

        override public void DrawGlyph(VertexBuffer vb, float x, float y)
        {
            // [Godot] Unity writes a quad from CharacterInfo (minX/maxY/maxX/minY relative to the pen at the baseline) plus
            // 4 offset copies for _boldVertice. Here one run is recorded at the baseline origin, which is what
            // Font.DrawChar expects. y is the negated baseline (VertexBuffer convention), the run uses TextField local
            // coordinates (y down). TextField draws the bold copies.
            if (_glyphRuns == null)
                return;

            if (_format.specialStyle == TextFormat.SpecialStyle.Subscript)
                y = y - UMath.RoundToInt(_ascent * _scale * SupOffset) / (keepCrisp ? UIContentScaler.scaleFactor : 1); // [Godot] Unity does not divide by scaleFactor here (the offset doubles with a scale factor of 2)
            else if (_format.specialStyle == TextFormat.SpecialStyle.Superscript)
                y = y + UMath.RoundToInt(_ascent * _scale * (1 / SupScale - 1 + SupOffset)) / (keepCrisp ? UIContentScaler.scaleFactor : 1);

            TextField.GlyphRun run = new TextField.GlyphRun();
            run.font = _styledFont;
            run.ch = _char;
            run.fontSize = _size;
            run.position = new Vector2(x, -y);
            run.color = _runColor;
            run.bold = _boldVertice;
            if (_format.gradientColor != null)
            {
                // Unity's DrawGlyph adds the vertices bottom-left, top-left, top-right, bottom-right with vertexColors[0..3]
                run.gradient = true;
                run.colorBL = vertexColors[0];
                run.colorTL = vertexColors[1];
                run.colorTR = vertexColors[2];
                run.colorBR = vertexColors[3];
            }
            _glyphRuns.Add(run);
        }

        override public void DrawLine(VertexBuffer vb, float x, float y, float width, int fontSize, int type)
        {
            // [Godot] Unity draws a quad sampled from the middle of the '_' glyph. Here a solid rectangle is recorded
            // (RenderingServer.CanvasItemAddRect in TextField.OnNodeDraw).
            if (_glyphRuns == null)
                return;

            float thickness;
            float offset;

            thickness = UMath.Max(1, fontSize / 16f); //guest underline size
            if (type == 0)
            {
                // [Godot] Unity: Mathf.RoundToInt(_lineChar.minY * fontSize / 50 + thickness), the top of the '_' glyph
                // relative to the baseline. Godot reports the underline position (positive = below the baseline).
                offset = -UMath.RoundToInt(_font.GetUnderlinePosition(UMath.Max(1, fontSize)));
            }
            else
            {
                // [Godot] Unity: Mathf.RoundToInt(_ascent * 0.4f * fontSize / _font.fontSize) with _ascent = fontSize.
                offset = UMath.RoundToInt(_font.GetAscent(UMath.Max(1, fontSize)) * 0.4f);
            }
            if (thickness < 1)
                thickness = 1;

            // Unity: topLeft = (x, y + offset), bottomRight = (x + width, topLeft.y - thickness), y up.
            TextField.GlyphRun run = new TextField.GlyphRun();
            run.position = new Vector2(x, -(y + offset));
            run.lineSize = new Vector2(width, thickness);
            run.color = _runColor;
            run.bold = _boldVertice;
            _glyphRuns.Add(run);
        }

        override public bool HasCharacter(char ch)
        {
            return _font.HasChar(ch);
        }

        override public int GetLineHeight(int size)
        {
            // [Godot] Unity: Mathf.RoundToInt(_lineHeight * size / _font.fontSize), _lineHeight = fontSize * 1.25.
            return UMath.RoundToInt(_font.GetHeight(UMath.Max(1, size)));
        }

        void textureRebuildCallback()
        {
            // [Godot] Called by Resource.Changed of the native font (Unity: Font.textureRebuilt). Metrics may have changed.
            // The style variations follow their base font by themselves.
            _metricsSize = 0;

            textRebuildFlag = true;
            version++;

            //Debug.Log("Font texture rebuild: " + name + "," + mainTexture.width + "," + mainTexture.height);
        }

        /// <summary>
        /// [Godot] Reads ascent and line height of the native font at the current pixel size.
        /// </summary>
        void UpdateMetrics()
        {
            if (_metricsSize == _size)
                return;

            _metricsSize = _size;
            _ascent = _font.GetAscent(_size);
            _lineHeight = _font.GetHeight(_size);
        }

        /// <summary>
        /// [Godot] Unity passes FontStyle to the OS font. Godot synthesizes the style with a FontVariation
        /// wrapping the native font: VariationEmbolden for bold, a skew VariationTransform for italic.
        /// </summary>
        Godot.Font GetStyledFont(FontStyle style)
        {
            if (style == FontStyle.Normal)
                return _font;

            Godot.Font f = _styledFonts[(int)style];
            if (f == null)
            {
                FontVariation fv = new FontVariation();
                fv.BaseFont = _font;
                if (style == FontStyle.Bold || style == FontStyle.BoldAndItalic)
                    fv.VariationEmbolden = BoldEmbolden;
                if (style == FontStyle.Italic || style == FontStyle.BoldAndItalic)
                    fv.VariationTransform = new Transform2D(1f, ItalicSkew, 0f, 1f, 0f, 0f);
                f = fv;
                _styledFonts[(int)style] = f;
            }
            return f;
        }
    }
}
