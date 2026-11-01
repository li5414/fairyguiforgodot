// Ported from FairyGUI-unity: Assets/Scripts/Core/MaterialManager.cs
using System;
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    [Flags]
    public enum MaterialFlags
    {
        Clipped = 1,
        SoftClipped = 2,
        StencilTest = 4,
        AlphaMask = 8,
        Grayed = 16,
        ColorFilter = 32
    }

    /// <summary>
    /// Every texture-shader combination has a MaterialManager.
    /// </summary>
    /// <remarks>
    /// [Godot] Materials are ShaderMaterials. Godot passes the texture with every draw command, so the materials hold no
    /// texture; one manager per (texture root, shader) is still kept so the Unity structure and events stay the same.
    /// Shader keywords become bool uniforms with the keyword as name (e.g. `uniform bool GRAYED;`); a custom shader that
    /// wants a keyword declares the uniform. Clip/stencil keywords are not used in Godot (see NGraphics.Update).
    /// </remarks>
    public class MaterialManager
    {
        public event Action<Material> onCreateNewMaterial;

        public bool firstMaterialInFrame;

        NTexture _texture;
        Shader _shader;
        List<string> _addKeywords;
        Dictionary<int, List<MaterialRef>> _materials;
        bool _combineTexture;

        class MaterialRef
        {
            public Material material;
            public int frame;
            public BlendMode blendMode;
            public uint group;
        }

        const int internalKeywordsCount = 6;
        static string[] internalKeywords = new[] { "CLIPPED", "SOFT_CLIPPED", null, "ALPHA_MASK", "GRAYED", "COLOR_FILTER" };

        // [Godot] Managers for NGraphics.canvasGroup (painting mode), which has no texture: one per shader name.
        static Dictionary<string, MaterialManager> _canvasGroupManagers = new Dictionary<string, MaterialManager>();

        /// <summary>
        ///
        /// </summary>
        /// <param name="texture">[Godot] null for canvas group managers</param>
        /// <param name="shader"></param>
        internal MaterialManager(NTexture texture, Shader shader)
        {
            _texture = texture;
            _shader = shader;
            _materials = new Dictionary<int, List<MaterialRef>>();
            _combineTexture = texture != null && texture.alphaTexture != null;
        }

        /// <summary>
        /// [Godot] Shared manager used by NGraphics.canvasGroup, which samples the CanvasGroup back buffer instead of a texture.
        /// </summary>
        /// <param name="shaderName"></param>
        /// <returns></returns>
        internal static MaterialManager GetCanvasGroupManager(string shaderName)
        {
            MaterialManager mm;
            if (!_canvasGroupManagers.TryGetValue(shaderName, out mm))
            {
                mm = new MaterialManager(null, ShaderConfig.GetShader(shaderName));
                _canvasGroupManagers.Add(shaderName, mm);
            }
            return mm;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="keywords"></param>
        /// <returns></returns>
        public int GetFlagsByKeywords(IList<string> keywords)
        {
            if (_addKeywords == null)
                _addKeywords = new List<string>();

            int flags = 0;
            for (int i = 0; i < keywords.Count; i++)
            {
                string s = keywords[i];
                if (string.IsNullOrEmpty(s))
                    continue;
                int j = _addKeywords.IndexOf(s);
                if (j == -1)
                {
                    j = _addKeywords.Count;
                    _addKeywords.Add(s);
                }
                flags += (1 << (j + internalKeywordsCount));
            }

            return flags;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="flags"></param>
        /// <param name="blendMode"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public Material GetMaterial(int flags, BlendMode blendMode, uint group)
        {
            if (blendMode != BlendMode.Normal && BlendModeUtils.Factors[(int)blendMode].pma)
                flags |= (int)MaterialFlags.ColorFilter;

            List<MaterialRef> items;
            if (!_materials.TryGetValue(flags, out items))
            {
                items = new List<MaterialRef>();
                _materials[flags] = items;
            }

            int frameId = FrameTime.frameCount;
            int cnt = items.Count;
            MaterialRef result = null;
            for (int i = 0; i < cnt; i++)
            {
                MaterialRef item = items[i];

                if (item.group == group && item.blendMode == blendMode)
                {
                    if (item.frame != frameId)
                    {
                        firstMaterialInFrame = true;
                        item.frame = frameId;
                    }
                    else
                        firstMaterialInFrame = false;

                    if (_combineTexture)
                        SetParameter(item.material, ShaderConfig.ID_AlphaTex, _texture.alphaTexture);

                    return item.material;
                }
                // [Godot] Unity recycles a material that was unused in the last frame for another group/blend mode
                // ("collect materials if it is unused in last frame"). In Godot a material stays assigned to its node
                // (CanvasItem.Material) while that node is not updated (invisible, cacheAsBitmap...), so recycling it would
                // silently change how that node looks. Materials are not recycled; the pool only holds distinct
                // (flags, blendMode, group) combinations, and group is always 0 in the Godot port.
            }

            if (result == null)
            {
                result = new MaterialRef() { material = CreateMaterial(flags) };
                items.Add(result);
            }
            else if (_combineTexture)
                SetParameter(result.material, ShaderConfig.ID_AlphaTex, _texture.alphaTexture);

            if (result.blendMode != blendMode)
            {
                BlendModeUtils.Apply(result.material, blendMode);
                result.blendMode = blendMode;
            }

            result.group = group;
            result.frame = frameId;
            firstMaterialInFrame = true;
            return result.material;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        Material CreateMaterial(int flags)
        {
            // [Godot] Alpha masks need to sample the clip_children back buffer (screen_texture), which would force a
            // back buffer copy for every user of the shader, so masks use a dedicated shader instead of a keyword variant.
            Shader shader = _shader;
            if ((flags & (int)MaterialFlags.AlphaMask) != 0)
                shader = ShaderConfig.GetShader(ShaderConfig.maskShader);

            ShaderMaterial mat = new ShaderMaterial();
            mat.Shader = shader;

            // [Godot] Unity: mat.mainTexture = _texture.nativeTexture. Godot passes the texture with each draw command.
            if (_texture != null && _texture.alphaTexture != null)
            {
                SetKeyword(mat, "COMBINED", true);
                SetParameter(mat, ShaderConfig.ID_AlphaTex, _texture.alphaTexture);
            }

            for (int i = 0; i < internalKeywordsCount; i++)
            {
                if ((flags & (1 << i)) != 0)
                {
                    string s = internalKeywords[i];
                    if (s != null)
                        SetKeyword(mat, s, true);
                }
            }
            if (_addKeywords != null)
            {
                int keywordCnt = _addKeywords.Count;
                for (int i = 0; i < keywordCnt; i++)
                {
                    if ((flags & (1 << (i + internalKeywordsCount))) != 0)
                        SetKeyword(mat, _addKeywords[i], true);
                }
            }

            if (onCreateNewMaterial != null)
                onCreateNewMaterial(mat);

            return mat;
        }

        /// <summary>
        ///
        /// </summary>
        public void DestroyMaterials()
        {
            var iter = _materials.GetEnumerator();
            while (iter.MoveNext())
            {
                List<MaterialRef> items = iter.Current.Value;
                // [Godot] Materials are reference counted resources: dropping the references releases them once no
                // node uses them any more (Unity destroys them immediately).
                items.Clear();
            }
            iter.Dispose();
        }

        /// <summary>
        ///
        /// </summary>
        public void RefreshMaterials()
        {
            _combineTexture = _texture != null && _texture.alphaTexture != null;
            var iter = _materials.GetEnumerator();
            while (iter.MoveNext())
            {
                List<MaterialRef> items = iter.Current.Value;
                int cnt = items.Count;
                for (int j = 0; j < cnt; j++)
                {
                    Material mat = items[j].material;
                    // [Godot] Unity: mat.mainTexture = _texture.nativeTexture. Not needed, see CreateMaterial.
                    if (_combineTexture)
                    {
                        SetKeyword(mat, "COMBINED", true);
                        SetParameter(mat, ShaderConfig.ID_AlphaTex, _texture.alphaTexture);
                    }
                }
            }
            iter.Dispose();
        }

        /// <summary>
        /// [Godot] Unity Material.EnableKeyword/DisableKeyword: sets the bool uniform named after the keyword.
        /// Does nothing for materials that are not ShaderMaterials.
        /// </summary>
        public static void SetKeyword(Material mat, string keyword, bool enabled)
        {
            if (mat is ShaderMaterial sm && !string.IsNullOrEmpty(keyword))
                sm.SetShaderParameter(keyword, enabled);
        }

        static void SetParameter(Material mat, string name, Texture2D value)
        {
            if (mat is ShaderMaterial sm)
                sm.SetShaderParameter(name, value);
        }
    }
}
