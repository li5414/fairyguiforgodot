// Ported from FairyGUI-unity: Assets/Scripts/Core/BlendMode.cs
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;

namespace FairyGUI
{
    /*关于BlendMode.Off, 这种模式相当于Blend Off指令的效果。当然，在着色器里使用Blend Off指令可以获得更高的效率，
        但因为Image着色器本身就有多个关键字，复制一个这样的着色器代价太大，所有为了节省Shader数量便增加了这样一种模式，也是可以接受的。
    */

    // [Godot] Godot canvas_item shaders cannot take arbitrary blend factors. The blend equation is chosen with
    // `render_mode blend_mix|blend_add|blend_sub|blend_mul|blend_premul_alpha|blend_disabled`, which is a compile-time
    // property of the Shader. BlendModeUtils therefore keeps the Unity factor table (API compatibility, Override still
    // works) and maps every factor pair to: (a) a shader variant with the matching render_mode, plus (b) the integer
    // uniform `blend_op`, which tells the FairyGUI shaders how to pre-transform COLOR so the fixed Godot equation gives
    // the Unity result. Variants are generated at runtime from the base shader code and cached.
    //   Normal  -> blend_mix                     (exact)
    //   None    -> blend_disabled                (as specified by the port contract; Unity uses One,One - see ResolveBlend)
    //   Add     -> blend_add                     (exact)
    //   Multiply-> blend_mul + blend_op 2        (exact for rgb: dst * (src*a + 1 - a); dst alpha kept)
    //   Screen  -> blend_premul_alpha + op 3     (approximation: Godot has no per-channel OneMinusSrcColor)
    //   Erase   -> blend_mul + op 4              (dst * (1 - srcA), rgb and alpha)
    //   Mask    -> blend_mul + op 5              (dst * srcA, rgb and alpha)
    //   Below   -> blend_mix                     (approximation: DstAlpha factors are not available)
    //   Off     -> blend_disabled                (exact)
    //   One_OneMinusSrcAlpha -> blend_premul_alpha (exact)

    /// <summary>
    ///
    /// </summary>
    public enum BlendMode
    {
        Normal,
        None,
        Add,
        Multiply,
        Screen,
        Erase,
        Mask,
        Below,
        Off,
        One_OneMinusSrcAlpha,
        Custom1,
        Custom2,
        Custom3
    }

    /// <summary>
    /// [Godot] Replacement for UnityEngine.Rendering.BlendMode (the native blend factor enum). Values match Unity.
    /// </summary>
    public enum NativeBlendMode
    {
        Zero = 0,
        One = 1,
        DstColor = 2,
        SrcColor = 3,
        OneMinusDstColor = 4,
        SrcAlpha = 5,
        OneMinusSrcColor = 6,
        DstAlpha = 7,
        OneMinusDstAlpha = 8,
        SrcAlphaSaturate = 9,
        OneMinusSrcAlpha = 10
    }

    /// <summary>
    ///
    /// </summary>
    public class BlendModeUtils
    {
        public class BlendFactor
        {
            public NativeBlendMode srcFactor;
            public NativeBlendMode dstFactor;
            public bool pma;

            public BlendFactor(NativeBlendMode srcFactor, NativeBlendMode dstFactor, bool pma = false)
            {
                this.srcFactor = srcFactor;
                this.dstFactor = dstFactor;
                this.pma = pma;
            }
        }

        //Source指的是被计算的颜色，Destination是已经在屏幕上的颜色。
        //混合结果=Source * factor1 + Destination * factor2
        public static BlendFactor[] Factors = new BlendFactor[] {
            //Normal
            new BlendFactor(NativeBlendMode.SrcAlpha, NativeBlendMode.OneMinusSrcAlpha),
            //None
            new BlendFactor(NativeBlendMode.One, NativeBlendMode.One),
            //Add
            new BlendFactor(NativeBlendMode.SrcAlpha, NativeBlendMode.One),
            //Multiply
            new BlendFactor(NativeBlendMode.DstColor, NativeBlendMode.OneMinusSrcAlpha, true),
            //Screen
            new BlendFactor(NativeBlendMode.One, NativeBlendMode.OneMinusSrcColor, true),
            //Erase
            new BlendFactor(NativeBlendMode.Zero, NativeBlendMode.OneMinusSrcAlpha),
            //Mask
            new BlendFactor(NativeBlendMode.Zero, NativeBlendMode.SrcAlpha),
            //Below
            new BlendFactor(NativeBlendMode.OneMinusDstAlpha, NativeBlendMode.DstAlpha),
            //Off
            new BlendFactor(NativeBlendMode.One, NativeBlendMode.Zero),
            //One_OneMinusSrcAlpha
            new BlendFactor(NativeBlendMode.One, NativeBlendMode.OneMinusSrcAlpha),
            //Custom1
            new BlendFactor(NativeBlendMode.SrcAlpha, NativeBlendMode.OneMinusSrcAlpha),
            //Custom2
            new BlendFactor(NativeBlendMode.SrcAlpha, NativeBlendMode.OneMinusSrcAlpha),
            //Custom3
            new BlendFactor(NativeBlendMode.SrcAlpha, NativeBlendMode.OneMinusSrcAlpha)
        };

        /// <summary>
        ///
        /// </summary>
        /// <param name="mat"></param>
        /// <param name="blendMode"></param>
        public static void Apply(Material mat, BlendMode blendMode)
        {
            BlendFactor bf = Factors[(int)blendMode];
            // [Godot] Unity: mat.SetFloat(ID_BlendSrcFactor/ID_BlendDstFactor, ...). Godot selects a shader variant whose
            // render_mode matches the factors and sets the blend_op uniform (see ResolveBlend).
            string renderMode;
            int blendOp;
            ResolveBlend(blendMode, out renderMode, out blendOp);

            if (mat is ShaderMaterial)
            {
                ShaderMaterial sm = (ShaderMaterial)mat;
                Shader baseShader = GetBaseShader(sm.Shader);
                if (baseShader != null)
                {
                    Shader variant = GetShaderVariant(baseShader, renderMode);
                    if (variant != null && sm.Shader != variant)
                        sm.Shader = variant;
                }
                sm.SetShaderParameter(ShaderConfig.ID_BlendOp, blendOp);

                if (bf.pma)
                    sm.SetShaderParameter(ShaderConfig.ID_ColorOption, 1);
                else
                    sm.SetShaderParameter(ShaderConfig.ID_ColorOption, 0);
            }
            else if (mat is CanvasItemMaterial)
            {
                // [Godot] CanvasItemMaterial has no "disabled" mode and no blend_op pre-transform: closest built-in mode.
                ((CanvasItemMaterial)mat).BlendMode = ToCanvasItemBlendMode(renderMode);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="blendMode"></param>
        /// <param name="srcFactor"></param>
        /// <param name="dstFactor"></param>
        public static void Override(BlendMode blendMode, NativeBlendMode srcFactor, NativeBlendMode dstFactor)
        {
            BlendFactor bf = Factors[(int)blendMode];
            bf.srcFactor = srcFactor;
            bf.dstFactor = dstFactor;
        }

        // [Godot] Values of the `blend_op` uniform understood by fairygui_common.gdshaderinc (fgui_blend_op).
        public const int BlendOpNone = 0;           //COLOR unchanged
        public const int BlendOpForceOpaque = 1;    //alpha = 1 (One,One emulated with blend_add)
        public const int BlendOpMultiply = 2;       //rgb = mix(1, rgb, a), a = 1 (with blend_mul)
        public const int BlendOpScreen = 3;         //rgb *= a, a = avg(rgb) (with blend_premul_alpha, approximation)
        public const int BlendOpErase = 4;          //rgba = 1 - a (with blend_mul)
        public const int BlendOpMask = 5;           //rgba = a (with blend_mul)
        public const int BlendOpPremultiply = 6;    //rgb *= a (with blend_premul_alpha)

        static int _warnedBlendModes;

        /// <summary>
        /// [Godot] Maps a FairyGUI blend mode (via its current factors) to a Godot render_mode blend token and a blend_op.
        /// </summary>
        /// <param name="blendMode"></param>
        /// <param name="renderMode">One of "blend_mix", "blend_add", "blend_sub", "blend_mul", "blend_premul_alpha", "blend_disabled"</param>
        /// <param name="blendOp">Value for the blend_op shader uniform</param>
        public static void ResolveBlend(BlendMode blendMode, out string renderMode, out int blendOp)
        {
            BlendFactor bf = Factors[(int)blendMode];
            NativeBlendMode src = bf.srcFactor;
            NativeBlendMode dst = bf.dstFactor;

            // [Godot] The port contract maps BlendMode.None to "blend disabled". Unity's table uses (One, One) for None;
            // an explicit Override of the None factors still takes precedence.
            if (blendMode == BlendMode.None && src == NativeBlendMode.One && dst == NativeBlendMode.One)
            {
                renderMode = "blend_disabled";
                blendOp = BlendOpNone;
                return;
            }

            if (src == NativeBlendMode.SrcAlpha && dst == NativeBlendMode.OneMinusSrcAlpha)
            {
                renderMode = "blend_mix";
                blendOp = BlendOpNone;
            }
            else if (src == NativeBlendMode.SrcAlpha && dst == NativeBlendMode.One)
            {
                renderMode = "blend_add";
                blendOp = BlendOpNone;
            }
            else if (src == NativeBlendMode.One && dst == NativeBlendMode.One)
            {
                renderMode = "blend_add";
                blendOp = BlendOpForceOpaque;
            }
            else if (src == NativeBlendMode.DstColor && dst == NativeBlendMode.OneMinusSrcAlpha)
            {
                renderMode = "blend_mul";
                blendOp = BlendOpMultiply;
            }
            else if (src == NativeBlendMode.One && dst == NativeBlendMode.OneMinusSrcColor)
            {
                renderMode = "blend_premul_alpha";
                blendOp = BlendOpScreen;
            }
            else if (src == NativeBlendMode.Zero && dst == NativeBlendMode.OneMinusSrcAlpha)
            {
                renderMode = "blend_mul";
                blendOp = BlendOpErase;
            }
            else if (src == NativeBlendMode.Zero && dst == NativeBlendMode.SrcAlpha)
            {
                renderMode = "blend_mul";
                blendOp = BlendOpMask;
            }
            else if (src == NativeBlendMode.One && dst == NativeBlendMode.Zero)
            {
                renderMode = "blend_disabled";
                blendOp = BlendOpNone;
            }
            else if (src == NativeBlendMode.One && dst == NativeBlendMode.OneMinusSrcAlpha)
            {
                renderMode = "blend_premul_alpha";
                blendOp = bf.pma ? BlendOpPremultiply : BlendOpNone;
            }
            else if (src == NativeBlendMode.DstColor && dst == NativeBlendMode.Zero)
            {
                renderMode = "blend_mul";
                blendOp = BlendOpNone;
            }
            else
            {
                // [Godot] Factor pairs that need DstAlpha/OneMinusDstAlpha (e.g. BlendMode.Below) or other combinations
                // cannot be expressed with Godot's fixed canvas blend equations. Fall back to normal blending.
                renderMode = "blend_mix";
                blendOp = BlendOpNone;
                int bit = 1 << (int)blendMode;
                if ((_warnedBlendModes & bit) == 0)
                {
                    _warnedBlendModes |= bit;
                    GD.PushWarning("FairyGUI: blend mode " + blendMode + " (" + src + ", " + dst + ") is approximated with normal blending in Godot.");
                }
            }
        }

        static CanvasItemMaterial.BlendModeEnum ToCanvasItemBlendMode(string renderMode)
        {
            switch (renderMode)
            {
                case "blend_add":
                    return CanvasItemMaterial.BlendModeEnum.Add;
                case "blend_sub":
                    return CanvasItemMaterial.BlendModeEnum.Sub;
                case "blend_mul":
                    return CanvasItemMaterial.BlendModeEnum.Mul;
                case "blend_premul_alpha":
                    return CanvasItemMaterial.BlendModeEnum.PremultAlpha;
                default:
                    return CanvasItemMaterial.BlendModeEnum.Mix;
            }
        }

        // [Godot] Shader variants per (base shader, blend render_mode). Variants are created from the base shader code with
        // the blend_* token of its render_mode replaced, so any custom canvas_item shader gets blend mode support too.
        static Dictionary<Shader, Dictionary<string, Shader>> _variants = new Dictionary<Shader, Dictionary<string, Shader>>();
        static Dictionary<Shader, Shader> _variantBase = new Dictionary<Shader, Shader>();
        static readonly Regex _renderModeRegex = new Regex(@"\brender_mode\s+([^;]*);", RegexOptions.Compiled);
        static readonly Regex _shaderTypeRegex = new Regex(@"\bshader_type\s+\w+\s*;", RegexOptions.Compiled);
        static readonly Regex _includeRegex = new Regex("#include\\s+\"([^\"]+)\"", RegexOptions.Compiled);

        /// <summary>
        /// [Godot] Returns the shader a blend variant was generated from (or the shader itself).
        /// </summary>
        public static Shader GetBaseShader(Shader shader)
        {
            if (shader == null)
                return null;

            Shader baseShader;
            if (_variantBase.TryGetValue(shader, out baseShader))
                return baseShader;
            return shader;
        }

        /// <summary>
        /// [Godot] Returns a variant of baseShader whose render_mode uses the given blend token.
        /// </summary>
        /// <param name="baseShader"></param>
        /// <param name="blendRenderMode">e.g. "blend_add"</param>
        /// <returns></returns>
        public static Shader GetShaderVariant(Shader baseShader, string blendRenderMode)
        {
            if (baseShader == null)
                return null;

            Dictionary<string, Shader> dict;
            if (!_variants.TryGetValue(baseShader, out dict))
            {
                dict = new Dictionary<string, Shader>();
                _variants[baseShader] = dict;
            }

            Shader variant;
            if (dict.TryGetValue(blendRenderMode, out variant))
                return variant;

            string code = baseShader.Code;
            if (string.IsNullOrEmpty(code) || GetBlendRenderMode(code) == blendRenderMode)
                variant = baseShader;
            else
            {
                variant = new Shader();
                variant.Code = CreateVariantCode(code, baseShader.ResourcePath, blendRenderMode);
                _variantBase[variant] = baseShader;
            }

            dict[blendRenderMode] = variant;
            return variant;
        }

        static string GetBlendRenderMode(string code)
        {
            Match m = _renderModeRegex.Match(code);
            if (m.Success)
            {
                foreach (string s in m.Groups[1].Value.Split(','))
                {
                    string t = s.Trim();
                    if (t.StartsWith("blend_"))
                        return t;
                }
            }
            return "blend_mix";
        }

        static string CreateVariantCode(string code, string basePath, string blendRenderMode)
        {
            //relative includes are resolved against the shader file path, which a runtime-created shader does not have
            if (!string.IsNullOrEmpty(basePath))
            {
                string baseDir = basePath.GetBaseDir();
                code = _includeRegex.Replace(code, (Match m) =>
                {
                    string path = m.Groups[1].Value;
                    if (path.StartsWith("res://") || path.StartsWith("user://") || path.StartsWith("uid://"))
                        return m.Value;
                    return "#include \"" + baseDir.PathJoin(path).SimplifyPath() + "\"";
                });
            }

            Match rm = _renderModeRegex.Match(code);
            if (rm.Success)
            {
                List<string> modes = new List<string>();
                foreach (string s in rm.Groups[1].Value.Split(','))
                {
                    string t = s.Trim();
                    if (t.Length > 0 && !t.StartsWith("blend_"))
                        modes.Add(t);
                }
                modes.Add(blendRenderMode);
                return code.Substring(0, rm.Index) + "render_mode " + string.Join(", ", modes) + ";" + code.Substring(rm.Index + rm.Length);
            }

            Match st = _shaderTypeRegex.Match(code);
            if (st.Success)
            {
                int pos = st.Index + st.Length;
                return code.Substring(0, pos) + "\nrender_mode " + blendRenderMode + ";" + code.Substring(pos);
            }

            return "shader_type canvas_item;\nrender_mode " + blendRenderMode + ";\n" + code;
        }
    }
}
