// Ported from FairyGUI-unity: Assets/Scripts/Filter/BlurFilter.cs
using System;
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    /// <remarks>
    /// [Godot] Unity renders the object into a RenderTexture (painting mode) and blurs it in onPaint with Graphics.Blit
    /// (4x down sample + two four-tap cone passes). Godot has no blit step: the object is drawn as a CanvasGroup
    /// (painting mode, back buffer mipmaps enabled by DisplayObject) and the paintingGraphics material uses the
    /// "FairyGUI/BlurFilter" shader, which samples the back buffer mipmaps with the same tap pattern in one pass.
    /// This is an approximation of the Unity result.
    /// </remarks>
    public class BlurFilter : IFilter
    {
        //ref http://blog.csdn.net/u011047171/article/details/47947397

        /// <summary>
        ///
        /// </summary>
        public float blurSize;

        DisplayObject _target;
        Material _blitMaterial;
        float _appliedBlurSize; // [Godot] last value sent to the shader

        const string BlurShaderName = "FairyGUI/BlurFilter";

        public BlurFilter()
        {
            blurSize = 1f;
        }

        public DisplayObject target
        {
            get { return _target; }
            set
            {
                _target = value;
                _appliedBlurSize = -1;
                // [Godot] Unity: EnterPaintingMode(1, null). The blur spreads outside the content, so the group is
                // extended by the blur radius (see OnRenderImage).
                _target.EnterPaintingMode(1, GetExtend(GetEffectiveBlurSize()));
                // [Godot] Unity: _target.onPaint += OnRenderImage; _blitMaterial = new Material(BlurFilter shader).
                // The blur shader becomes the shader of the painting graphics (CanvasGroup material).
                _target.paintingGraphics.canvasGroup = true;
                _target.paintingGraphics.shader = BlurShaderName;
                _blitMaterial = _target.paintingGraphics.material;

                OnRenderImage();
            }
        }

        public void Dispose()
        {
            NGraphics pg = _target.paintingGraphics;
            if (pg != null && pg.shader == BlurShaderName)
                pg.shader = ShaderConfig.imageShader;

            _target.LeavePaintingMode(1);
            if (pg != null && !_target.paintingMode)
                pg.canvasGroup = false; // [Godot] no other painting requestor: stop providing the group material
            _target = null;

            // [Godot] Unity destroys _blitMaterial. It is a shared, reference counted material here.
            _blitMaterial = null;
        }

        public void Update()
        {
            // [Godot] Unity does the work in onPaint after every capture. Here the blur parameters are checked every
            // frame, so a changed blurSize takes effect.
            OnRenderImage();
        }

        // [Godot] FourTapCone/DownSample4x removed: Graphics.BlitMultiTap has no Godot counterpart. fairygui_blur.gdshader
        // samples the 1/8 resolution mipmap (Unity: sourceTexture.width / 8) with the taps of the two FourTapCone
        // iterations (offsets blurSize * iteration + 0.5 texels) combined in one pass.

        void OnRenderImage()
        {
            if (_target == null || _target.paintingGraphics == null)
                return;

            float size = GetEffectiveBlurSize();
            if (size != _appliedBlurSize)
            {
                bool first = _appliedBlurSize < 0;
                _appliedBlurSize = size;
                _target.paintingGraphics.SetShaderParameter(ShaderConfig.ID_BlurSize, size);
                if (!first)
                    _target.EnterPaintingMode(1, GetExtend(size)); // [Godot] update the group margin (requestor 1 is already set)
            }
        }

        float GetEffectiveBlurSize()
        {
            return blurSize < 0.01 ? 0 : blurSize; //Unity: if (blurSize < 0.01) return; (no blur)
        }

        // [Godot] The shader reaches (blurSize + 1) texels of the 1/8 mipmap plus the 8 px footprint of a mipmap texel.
        static Margin GetExtend(float size)
        {
            int m = size > 0 ? UMath.CeilToInt((size + 2) * 8) : 0;
            return new Margin() { left = m, right = m, top = m, bottom = m };
        }
    }
}
