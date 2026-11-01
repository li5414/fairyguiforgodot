// Ported from FairyGUI-unity: Assets/Scripts/Core/ShaderConfig.cs
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    public static class ShaderConfig
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public delegate Shader GetFunction(string name);

        /// <summary>
        /// [Godot] Unity: Shader.Find. The default resolver maps the built-in FairyGUI shader names to the .gdshader files
        /// in <see cref="shaderPath"/>; any other name is treated as a resource path (res://..., uid://...).
        /// </summary>
        public static GetFunction Get = Find;

        /// <summary>
        ///
        /// </summary>
        public static string imageShader = "FairyGUI/Image";

        /// <summary>
        ///
        /// </summary>
        public static string textShader = "FairyGUI/Text";

        /// <summary>
        ///
        /// </summary>
        public static string bmFontShader = "FairyGUI/BMFont";

        /// <summary>
        ///
        /// </summary>
        public static string TMPFontShader = "FairyGUI/TMP";

        /// <summary>
        /// [Godot] Shader used by NGraphics.canvasGroup (painting mode). It samples the CanvasGroup back buffer
        /// instead of a texture, and supports GRAYED, COLOR_FILTER and blend modes like the image shader.
        /// </summary>
        public static string paintingShader = "FairyGUI/Painting";

        /// <summary>
        /// [Godot] Shader used for alpha masks (NGraphics._maskFlag), see MaterialManager.
        /// </summary>
        public static string maskShader = "FairyGUI/Mask";

        /// <summary>
        /// [Godot] Folder of the built-in .gdshader files.
        /// </summary>
        public static string shaderPath = "res://addons/fairygui/Shaders/";

        // [Godot] Unity property ids become shader parameter names. Clip, stencil and blend-factor ids have no
        // meaning in Godot (clipping is done with RenderingServer clip rects, masks with clip_children, blending with
        // render_mode variants); they are kept so code that references them still compiles.
        public static string ID_ClipBox;
        public static string ID_ClipSoftness;
        public static string ID_AlphaTex;
        public static string ID_StencilComp;
        public static string ID_Stencil;
        public static string ID_StencilOp;
        public static string ID_StencilReadMask;
        public static string ID_ColorMask;
        public static string ID_ColorMatrix;
        public static string ID_ColorOffset;
        public static string ID_BlendSrcFactor;
        public static string ID_BlendDstFactor;
        public static string ID_ColorOption;

        public static string ID_Stencil2;

        // [Godot] Parameters of the Godot shaders.
        public static string ID_BlendOp;
        public static string ID_BlurSize;
        public static string ID_MaskReversed;

        static Dictionary<string, Shader> _cache = new Dictionary<string, Shader>();
        static Shader _fallback;

        static ShaderConfig()
        {
            ID_ClipBox = "clip_box";
            ID_ClipSoftness = "clip_softness";
            ID_AlphaTex = "alpha_tex";
            ID_StencilComp = "stencil_comp";
            ID_Stencil = "stencil";
            ID_StencilOp = "stencil_op";
            ID_StencilReadMask = "stencil_read_mask";
            ID_ColorMask = "color_mask";
            ID_ColorMatrix = "color_matrix";
            ID_ColorOffset = "color_offset";
            ID_BlendSrcFactor = "blend_src_factor";
            ID_BlendDstFactor = "blend_dst_factor";
            ID_ColorOption = "color_option";

            ID_Stencil2 = "stencil_ref";

            ID_BlendOp = "blend_op";
            ID_BlurSize = "blur_size";
            ID_MaskReversed = "reversed";
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Shader GetShader(string name)
        {
            Shader shader = Get(name);
            if (shader == null)
            {
                GD.PushWarning("FairyGUI: shader not found: " + name);
                // [Godot] Unity falls back to Shader.Find("UI/Default"); Godot falls back to the FairyGUI image shader.
                shader = Find(imageShader);
                if (shader == null)
                    shader = GetFallbackShader();
            }

            return shader;
        }

        /// <summary>
        /// [Godot] Default implementation of <see cref="Get"/>. Built-in names ("FairyGUI/Image", "FairyGUI/Text",
        /// "FairyGUI/BMFont", "FairyGUI/BlurFilter", "FairyGUI/Painting", "FairyGUI/Mask") map to the files in
        /// <see cref="shaderPath"/>; names starting with res://, uid:// or user:// are loaded as Shader resources.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>null if not found</returns>
        public static Shader Find(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            Shader shader;
            if (_cache.TryGetValue(name, out shader) && shader != null)
                return shader;

            string path = GetBuiltinShaderPath(name);
            if (path == null)
            {
                if (name.StartsWith("res://") || name.StartsWith("uid://") || name.StartsWith("user://"))
                    path = name;
                else
                    return null;
            }

            if (!ResourceLoader.Exists(path))
                return null;

            shader = ResourceLoader.Load(path) as Shader;
            if (shader != null)
                _cache[name] = shader;
            return shader;
        }

        static string GetBuiltinShaderPath(string name)
        {
            string file;
            switch (name)
            {
                case "FairyGUI/Image": file = "fairygui_image.gdshader"; break;
                case "FairyGUI/Text": file = "fairygui_text.gdshader"; break;
                case "FairyGUI/BMFont": file = "fairygui_bmfont.gdshader"; break;
                case "FairyGUI/BlurFilter": file = "fairygui_blur.gdshader"; break;
                case "FairyGUI/Painting": file = "fairygui_painting.gdshader"; break;
                case "FairyGUI/Mask": file = "fairygui_mask.gdshader"; break;
                case "FairyGUI/TMP": file = "fairygui_text.gdshader"; break; // [Godot] TextMeshPro is not ported
                default: return null;
            }
            return shaderPath.PathJoin(file);
        }

        static Shader GetFallbackShader()
        {
            if (_fallback == null)
            {
                // [Godot] Last resort when the addon shaders cannot be loaded: plain textured, vertex colored drawing.
                _fallback = new Shader();
                _fallback.Code = "shader_type canvas_item;\n";
            }
            return _fallback;
        }
    }
}
