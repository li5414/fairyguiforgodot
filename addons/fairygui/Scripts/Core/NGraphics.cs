// Ported from FairyGUI-unity: Assets/Scripts/Core/NGraphics.cs
using System;
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    /// <remarks>
    /// [Godot] NGraphics renders into the CanvasItem of a Node2D instead of a MeshRenderer:
    /// - Update() rebuilds and caches the mesh when it is dirty (Unity conventions: y negated, uv origin bottom-left),
    ///   selects the material and calls node.QueueRedraw() when something visible changed.
    /// - Draw() must be called from the node's _Draw (DisplayObject.OnNodeDraw). It is the only place where the mesh is
    ///   converted to Godot conventions: (x, y) -> (x, -y), (u, v) -> (u, 1 - v).
    /// - The object alpha is NOT multiplied into the vertex colors: it is applied by node.Modulate. grayed only selects
    ///   the material.
    /// - NGraphics owns node.Material: it assigns the shared material (MaterialManager), the custom material, or an
    ///   instance-owned copy when SetShaderParameter was used. Do not assign node.Material directly on such nodes.
    /// </remarks>
    public class NGraphics : IMeshFactory, IBatchable
    {
        /// <summary>
        /// [Godot] Unity: gameObject. The node whose CanvasItem receives the draw commands and the material.
        /// </summary>
        public Node2D node { get; private set; }

        // [Godot] meshFilter / meshRenderer / mesh removed: the mesh is cached in this object and submitted by Draw().

        /// <summary>
        ///
        /// </summary>
        public BlendMode blendMode;

        /// <summary>
        /// 不参与剪裁
        /// </summary>
        public bool dontClip;

        /// <summary>
        /// 当Mesh更新时触发
        /// </summary>
        public event Action meshModifier;

        /// <summary>
        ///
        /// </summary>
        public List<NGraphics> subInstances;

        /// <summary>
        ///
        /// </summary>
        public Vector4 userData;

        NTexture _texture;
        string _shader;
        Material _material;
        int _customMatarial; //0-none, 1-common, 2-support internal mask, 128-owns material
        MaterialManager _manager;
        string[] _shaderKeywords;
        int _materialFlags;
        IMeshFactory _meshFactory;

        float _alpha;
        Color _color;
        bool _meshDirty;
        Rect _contentRect;
        FlipType _flip;

        public class VertexMatrix
        {
            public Vector3 cameraPos;
            public Projection matrix; // [Godot] Matrix4x4 -> Godot.Projection (column-major, `matrix * point` = MultiplyPoint)
        }
        VertexMatrix _vertexMatrix;

        bool hasAlphaBackup;
        List<float> _alphaBackup; //透明度改变需要通过修改顶点颜色实现，但顶点颜色本身可能就带有透明度，所以这里要有一个备份

        internal int _maskFlag;
        // [Godot] StencilEraser removed: Godot masks with clip_children, which needs no stencil erase pass.
        int _stencilEraserOrder;

        // [Godot] MaterialPropertyBlock -> parameters applied to an instance-owned copy of the material (SetShaderParameter)
        Dictionary<string, Variant> _propertyBlock;
        bool _blockUpdated;

        internal BatchElement _batchElement;

        // [Godot] state of the Godot renderer
        bool _enabled;
        int _renderingOrder;
        bool _canvasGroup;
        ShaderMaterial _ownMaterial;
        Material _ownMaterialSource;
        Material _appliedMaterial;
        string[] _appliedKeywords;
        Texture2D _drawnTexture;

        // [Godot] cached mesh (replaces UnityEngine.Mesh), Unity conventions
        Vector3[] _vertices;
        Vector2[] _uvs;
        Color[] _colors;
        int[] _triangles;
        int _vertCount;
        int _indexCount;

        // [Godot] scratch buffers for the converted arrays (Draw runs on the main thread only)
        static Vector2[] _sPoints;
        static Vector2[] _sUVs;

        /// <summary>
        ///
        /// </summary>
        /// <param name="node">[Godot] Unity: GameObject. Usually DisplayObject.node.</param>
        public NGraphics(Node2D node)
        {
            this.node = node;

            _alpha = 1f;
            _shader = ShaderConfig.imageShader;
            _color = UColor.white;
            _meshFactory = this;
            _enabled = true;

            // [Godot] Unity adds MeshFilter/MeshRenderer components and creates a Mesh here. Godot draws into the
            // node's CanvasItem (see Draw), nothing has to be created.

            Stats.LatestGraphicsCreation++;
        }

        /// <summary>
        ///
        /// </summary>
        public IMeshFactory meshFactory
        {
            get { return _meshFactory; }
            set
            {
                if (_meshFactory != value)
                {
                    _meshFactory = value;
                    _meshDirty = true;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetMeshFactory<T>() where T : IMeshFactory, new()
        {
            if (!(_meshFactory is T))
            {
                _meshFactory = new T();
                _meshDirty = true;
            }
            return (T)_meshFactory;
        }

        /// <summary>
        ///
        /// </summary>
        public Rect contentRect
        {
            get { return _contentRect; }
            set
            {
                _contentRect = value;
                _meshDirty = true;

                if (subInstances != null)
                {
                    foreach (var sub in subInstances)
                        sub.contentRect = value;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public FlipType flip
        {
            get { return _flip; }
            set
            {
                if (_flip != value)
                {
                    _flip = value;
                    _meshDirty = true;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public NTexture texture
        {
            get { return _texture; }
            set
            {
                if (_texture != value)
                {
                    if (value != null)
                        value.AddRef();
                    if (_texture != null)
                        _texture.ReleaseRef();

                    _texture = value;
                    // [Godot] Unity: _material.mainTexture = ... for custom materials. Godot passes the texture with each
                    // draw command (see Draw), materials hold no texture.
                    _meshDirty = true;
                    UpdateManager();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public string shader
        {
            get { return _shader; }
            set
            {
                // [Godot] the BMFont channel is encoded into the uv (see UpdateMeshNow): rebuild when switching shader kind
                if ((_shader == ShaderConfig.bmFontShader) != (value == ShaderConfig.bmFontShader))
                    _meshDirty = true;
                _shader = value;
                UpdateManager();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="shader"></param>
        /// <param name="texture"></param>
        public void SetShaderAndTexture(string shader, NTexture texture)
        {
            if ((_shader == ShaderConfig.bmFontShader) != (shader == ShaderConfig.bmFontShader))
                _meshDirty = true; // [Godot] see shader
            _shader = shader;
            if (_texture != texture)
                this.texture = texture;
            else
                UpdateManager();
        }

        /// <summary>
        ///
        /// </summary>
        public Material material
        {
            get
            {
                if (_customMatarial == 0 && _material == null && _manager != null)
                    _material = _manager.GetMaterial(_materialFlags, blendMode, 0);
                return _material;
            }
            set
            {
                // [Godot] Unity destroys an owned material here (DestroyImmediate). Godot materials are reference counted
                // and are released when the last reference is dropped.
                _material = value;
                _appliedKeywords = null;
                if (_material != null)
                {
                    _customMatarial = 1;
                    // [Godot] Unity adds flag 2 when the material has _Stencil/_ClipBox properties (clipping done by the
                    // material). Godot clips with RenderingServer clip rects and clip_children, so flag 2 is never used.

                    ApplyMaterial(_enabled ? _material : null);
                    // [Godot] Unity: _material.mainTexture = _texture.nativeTexture. Not needed, see texture.
                }
                else
                {
                    _customMatarial = 0;
                    ApplyMaterial(null);
                }
            }
        }

        /// <summary>
        /// Same as material property except that ownership is transferred to this object.
        /// </summary>
        /// <param name="material"></param>
        public void SetMaterial(Material material)
        {
            this.material = material;
            _customMatarial |= 128;
        }

        /// <summary>
        ///
        /// </summary>
        public string[] materialKeywords
        {
            get { return _shaderKeywords; }
            set
            {
                _shaderKeywords = value;
                UpdateMaterialFlags();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="keyword"></param>
        /// <param name="enabled"></param>
        /// <remarks>[Godot] A keyword is a bool uniform with the same name in the shader (e.g. `uniform bool COLOR_FILTER;`).</remarks>
        public void ToggleKeyword(string keyword, bool enabled)
        {
            if (enabled)
            {
                if (_shaderKeywords == null)
                {
                    _shaderKeywords = new string[] { keyword };
                    UpdateMaterialFlags();
                }
                else if (Array.IndexOf(_shaderKeywords, keyword) == -1)
                {
                    Array.Resize(ref _shaderKeywords, _shaderKeywords.Length + 1);
                    _shaderKeywords[_shaderKeywords.Length - 1] = keyword;
                    UpdateMaterialFlags();
                }
            }
            else
            {
                if (_shaderKeywords != null)
                {
                    int i = Array.IndexOf(_shaderKeywords, keyword);
                    if (i != -1)
                    {
                        _shaderKeywords[i] = null;
                        UpdateMaterialFlags();
                    }
                }
            }
        }

        void UpdateManager()
        {
            if (_canvasGroup)
            {
                // [Godot] painting mode: no texture, the material samples the CanvasGroup back buffer
                string shaderName = _shader == ShaderConfig.imageShader ? ShaderConfig.paintingShader : _shader;
                _manager = MaterialManager.GetCanvasGroupManager(shaderName);
            }
            else if (_texture != null)
                _manager = _texture.GetMaterialManager(_shader);
            else
                _manager = null;
            UpdateMaterialFlags();
        }

        void UpdateMaterialFlags()
        {
            if (_customMatarial != 0)
            {
                if (material != null)
                    ApplyKeywordsToCustomMaterial(); // [Godot] Unity: material.shaderKeywords = _shaderKeywords;
            }
            else if (_shaderKeywords != null && _manager != null)
                _materialFlags = _manager.GetFlagsByKeywords(_shaderKeywords);
            else
                _materialFlags = 0;
        }

        // [Godot] Material.shaderKeywords replaces the keyword set: keywords that were set before and are gone now are
        // switched off (bool uniform = false), the current ones switched on.
        void ApplyKeywordsToCustomMaterial()
        {
            if (_appliedKeywords != null)
            {
                foreach (string s in _appliedKeywords)
                {
                    if (!string.IsNullOrEmpty(s) && (_shaderKeywords == null || Array.IndexOf(_shaderKeywords, s) == -1))
                        SetKeywordOnCustomMaterial(s, false);
                }
            }
            if (_shaderKeywords != null)
            {
                foreach (string s in _shaderKeywords)
                {
                    if (!string.IsNullOrEmpty(s))
                        SetKeywordOnCustomMaterial(s, true);
                }
                _appliedKeywords = (string[])_shaderKeywords.Clone();
            }
            else
                _appliedKeywords = null;
        }

        void SetKeywordOnCustomMaterial(string keyword, bool enabled)
        {
            MaterialManager.SetKeyword(_material, keyword, enabled);
            if (_ownMaterial != null && _ownMaterialSource == _material)
                _ownMaterial.SetShaderParameter(keyword, enabled);
        }

        /// <summary>
        ///
        /// </summary>
        public bool enabled
        {
            get { return _enabled; }
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    // [Godot] Unity: meshRenderer.enabled. Draw() submits nothing while disabled; the material is removed
                    // from the node so a disabled painting graphics does not keep affecting it.
                    if (!_enabled)
                        ApplyMaterial(null);
                    else if (_customMatarial != 0)
                        ApplyMaterial(_material);
                    Redraw();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        [Obsolete("Use renderingOrder")]
        public int sortingOrder
        {
            get { return _renderingOrder; }
            set { _renderingOrder = value; }
        }

        /// <summary>
        ///
        /// </summary>
        /// <remarks>[Godot] Kept for API compatibility. Godot draws in node tree order; the value does not affect rendering.</remarks>
        public int renderingOrder
        {
            get { return _renderingOrder; }
            set { _renderingOrder = value; }
        }

        public void SetRenderingOrder(UpdateContext context, bool inBatch)
        {
            _renderingOrder = context.renderingOrder++;

            if (subInstances != null && !inBatch)
            {
                foreach (var sub in subInstances)
                {
                    sub._renderingOrder = context.renderingOrder++;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value"></param>
        internal void _SetStencilEraserOrder(int value)
        {
            // [Godot] no stencil eraser (clip_children masks need no erase pass); the order is only recorded.
            _stencilEraserOrder = value;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value"></param>
        public Color color
        {
            get { return _color; }
            set { _color = value; }
        }

        /// <summary>
        ///
        /// </summary>
        public void Tint()
        {
            if (_meshDirty)
                return;

            int vertCount = _vertCount;
            if (vertCount == 0)
                return;

            Color[] colors = _colors;
            for (int i = 0; i < vertCount; i++)
            {
                Color col = _color;
                // [Godot] Unity: col.a = _alpha * backup. The object alpha is applied by node.Modulate, not baked here.
                col.A = hasAlphaBackup ? _alphaBackup[i] : 1f;
                colors[i] = col;
            }

            Redraw();
        }

        void ChangeAlpha(float value)
        {
            // [Godot] Unity rewrites the vertex alpha here. In Godot the object alpha lives in node.Modulate (inherited by
            // the subtree), multiplying it into the vertex colors would apply it twice. Only the value is recorded.
            _alpha = value;
        }

        /// <summary>
        ///
        /// </summary>
        public VertexMatrix vertexMatrix
        {
            get { return _vertexMatrix; }
            set
            {
                _vertexMatrix = value;
                _meshDirty = true;

                if (subInstances != null)
                {
                    foreach (var sub in subInstances)
                        sub._vertexMatrix = value;
                }
            }
        }

        /// <summary>
        /// [Godot] Replaces Unity's materialPropertyBlock. Sets a shader parameter for this object only: on first use the
        /// current (shared or custom) material is copied into an instance-owned ShaderMaterial, and the parameters are
        /// re-applied whenever the source material changes (e.g. grayed on/off). Pass a default Variant to remove it.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetShaderParameter(string name, Variant value)
        {
            if (_propertyBlock == null)
                _propertyBlock = new Dictionary<string, Variant>();

            if (value.VariantType == Variant.Type.Nil)
                _propertyBlock.Remove(name);
            else
                _propertyBlock[name] = value;
            _blockUpdated = true;

            if (_ownMaterial != null)
                _ownMaterial.SetShaderParameter(name, value);
        }

        /// <summary>
        ///
        /// </summary>
        public void SetMeshDirty()
        {
            _meshDirty = true;

            if (subInstances != null)
            {
                foreach (var g in subInstances)
                    g._meshDirty = true;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public bool UpdateMesh()
        {
            bool ret = false;
            if (_meshDirty)
            {
                UpdateMeshNow();
                ret = true;
            }

            if (subInstances != null)
            {
                foreach (var g in subInstances)
                {
                    if (g.UpdateMesh())
                        ret = true;
                }
            }

            return ret;
        }

        /// <summary>
        ///
        /// </summary>
        public void Dispose()
        {
            // [Godot] Unity destroys the Mesh and an owned material. Godot: release the cached arrays and stop
            // assigning the material to the node (resources are reference counted).
            _vertices = null;
            _uvs = null;
            _colors = null;
            _triangles = null;
            _vertCount = 0;
            _indexCount = 0;

            if (_appliedMaterial != null)
                ApplyMaterial(null);

            if (_texture != null)
            {
                _texture.ReleaseRef();
                _texture = null;
            }

            _manager = null;
            _material = null;
            _ownMaterial = null;
            _ownMaterialSource = null;
            _drawnTexture = null;
            meshModifier = null;

            if (subInstances != null)
            {
                foreach (var sub in subInstances)
                    sub.Dispose();
                subInstances.Clear();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="context"></param>
        /// <param name="alpha">[Godot] recorded only, the object alpha is applied by node.Modulate</param>
        /// <param name="grayed"></param>
        public void Update(UpdateContext context, float alpha, bool grayed)
        {
            Stats.GraphicsCount++;

            if (_meshDirty)
            {
                _alpha = alpha;
                UpdateMeshNow();
            }
            else if (_alpha != alpha)
                ChangeAlpha(alpha);

            // [Godot] Unity: meshRenderer.SetPropertyBlock(_propertyBlock) when _blockUpdated. Here the parameters are
            // applied to the instance-owned material by ApplyMaterial below.

            if (_customMatarial != 0)
            {
                // [Godot] Unity: context.ApplyClippingProperties(_material) for materials that clip themselves (flag 2).
                // Godot clips with RenderingServer clip rects on the container, no material property is needed.
                if (_enabled)
                    ApplyMaterial(_material);
            }
            else
            {
                if (_manager != null)
                {
                    if (_maskFlag == 1)
                    {
                        // [Godot] clipId is not needed (no stencil state per clip) and no alpha mask properties are applied.
                        _material = _manager.GetMaterial((int)MaterialFlags.AlphaMask | _materialFlags, BlendMode.Normal, 0);
                    }
                    else
                    {
                        int matFlags = _materialFlags;
                        if (grayed)
                            matFlags |= (int)MaterialFlags.Grayed;

                        // [Godot] Unity adds StencilTest/Clipped/SoftClipped flags when context.clipped and applies the clip
                        // box to the material. Godot clips with RenderingServer clip rects and clip_children, so the
                        // material does not depend on clipping (clipSoftness is not supported).
                        _material = _manager.GetMaterial(matFlags, blendMode, 0);
                    }
                }
                else
                    _material = null;

                if (_enabled)
                    ApplyMaterial(_material);
            }

            if (_maskFlag != 0)
            {
                if (_maskFlag == 1)
                    _maskFlag = 2;
                else
                {
                    // [Godot] Unity disables the stencil eraser here. The mask is no longer used: draw it normally again.
                    _maskFlag = 0;
                    Redraw();
                }
            }

            // [Godot] the draw command stores the texture RID: redraw when the texture was reloaded/unloaded
            if (_vertCount > 0)
            {
                Texture2D tex = _texture != null ? _texture.nativeTexture : null;
                if (tex != _drawnTexture)
                    Redraw();
            }

            if (subInstances != null)
            {
                foreach (var sub in subInstances)
                    sub.Update(context, alpha, grayed);
            }
        }

        internal void _PreUpdateMask(UpdateContext context, uint maskId)
        {
            //_maskFlag: 0-new mask, 1-active mask, 2-mask complete
            if (_maskFlag == 0)
            {
                // [Godot] Unity creates/enables the StencilEraser here. While _maskFlag != 0 the mask is not drawn by its
                // own node (see Draw()); the caller draws it into a clip_children node with Draw(Rid, Transform2D) and
                // can use `material` (FairyGUI/Mask shader: hard edge at alpha 0.001 like the Unity stencil) there.
                Redraw();
            }

            _maskFlag = 1;

            // [Godot] Unity assigns the AlphaMask material (keyed by maskId) to the stencil eraser and applies the alpha mask
            // stencil properties. The AlphaMask material is selected in Update (see `material`), maskId is not needed.
        }

        void UpdateMeshNow()
        {
            _meshDirty = false;

            if (_texture == null || _meshFactory == null)
            {
                if (_vertCount > 0)
                {
                    ClearMesh();

                    if (meshModifier != null)
                        meshModifier();
                }
                return;
            }

            VertexBuffer vb = VertexBuffer.Begin();
            vb.contentRect = _contentRect;
            vb.uvRect = _texture.uvRect;
            if (_texture != null)
                vb.textureSize = new Vector2(_texture.width, _texture.height);
            else
                vb.textureSize = new Vector2(0, 0);
            // [Godot] Unity flips by swapping vb.uvRect.xMin/xMax (yMin/yMax), i.e. an inverted Rect. FairyGUI.Rect clamps
            // xMin/xMax with Min/Max, so an inverted rect cannot be represented. The mesh is built with the normal uvRect
            // and the uvs are mirrored inside it afterwards, which gives the same result because every uv generated by
            // the mesh factories is a linear function of vb.uvRect.
            bool flipH = _flip == FlipType.Horizontal || _flip == FlipType.Both;
            bool flipV = _flip == FlipType.Vertical || _flip == FlipType.Both;
            Rect flipRect = vb.uvRect;
            vb.vertexColor = _color;
            _meshFactory.OnPopulateMesh(vb);

            int vertCount = vb.currentVertCount;
            if (vertCount == 0)
            {
                if (_vertCount > 0)
                {
                    ClearMesh();

                    if (meshModifier != null)
                        meshModifier();
                }
                vb.End();
                return;
            }

            if (flipH || flipV)
            {
                float sumX = flipRect.xMin + flipRect.xMax;
                float sumY = flipRect.yMin + flipRect.yMax;
                for (int i = 0; i < vertCount; i++)
                {
                    Vector2 vec = vb.uvs[i];
                    if (flipH)
                        vec.X = sumX - vec.X;
                    if (flipV)
                        vec.Y = sumY - vec.Y;
                    vb.uvs[i] = vec;
                }
            }

            if (_texture.rotated)
            {
                float xMin = _texture.uvRect.xMin;
                float yMin = _texture.uvRect.yMin;
                float yMax = _texture.uvRect.yMax;
                float tmp;
                for (int i = 0; i < vertCount; i++)
                {
                    Vector2 vec = vb.uvs[i];
                    tmp = vec.Y;
                    vec.Y = yMin + vec.X - xMin;
                    vec.X = xMin + yMax - tmp;
                    vb.uvs[i] = vec;
                }
            }

            hasAlphaBackup = vb._alphaInVertexColor;
            if (hasAlphaBackup)
            {
                if (_alphaBackup == null)
                    _alphaBackup = new List<float>();
                else
                    _alphaBackup.Clear();
                for (int i = 0; i < vertCount; i++)
                {
                    Color col = vb.colors[i];
                    _alphaBackup.Add(col.A);
                    // [Godot] Unity: col.a *= _alpha. The object alpha is applied by node.Modulate.
                }
            }
            // [Godot] Unity: else if (_alpha != 1) multiply every vertex alpha by _alpha. Not done, see above.

            if (_vertexMatrix != null)
            {
                Vector3 camPos = _vertexMatrix.cameraPos;
                Vector3 center = new Vector3(camPos.X, camPos.Y, 0);
                center -= _vertexMatrix.matrix * center;
                for (int i = 0; i < vertCount; i++)
                {
                    Vector3 pt = vb.vertices[i];
                    pt = _vertexMatrix.matrix * pt;
                    pt += center;
                    Vector3 vec = pt - camPos;
                    float lambda = -camPos.Z / vec.Z;
                    pt.X = camPos.X + lambda * vec.X;
                    pt.Y = camPos.Y + lambda * vec.Y;
                    pt.Z = 0;

                    vb.vertices[i] = pt;
                }
            }

            // [Godot] Unity: mesh.Clear(); mesh.SetVertices/SetUVs/SetColors/SetTriangles/SetUVs(1, uvs2).
            // The arrays are cached here (Unity conventions) and submitted by Draw().
            if (_vertices == null || _vertices.Length < vertCount)
            {
                int cap = Math.Max(vertCount, 4);
                _vertices = new Vector3[cap];
                _uvs = new Vector2[cap];
                _colors = new Color[cap];
            }
            vb.vertices.CopyTo(_vertices);
            vb.colors.CopyTo(_colors);
            // [Godot] vb._isArbitraryQuad: Unity uploads 4D uvs (FixUVForArbitraryQuad) for perspective-correct texturing.
            // Godot triangle arrays only take 2D uvs, so plain uvs are used (affine texture interpolation).
            vb.uvs.CopyTo(_uvs);

            // [Godot] Unity uploads vb.uvs2 as a second uv channel (BMFont channel). Godot triangle arrays have one uv
            // channel, so for the BMFont shader the channel index is encoded into u (u + 2 * channel) and decoded by
            // fairygui_bmfont.gdshader. Other shaders ignore uvs2.
            if (vb.uvs2.Count == vb.uvs.Count && _shader == ShaderConfig.bmFontShader && _customMatarial == 0)
            {
                for (int i = 0; i < vertCount; i++)
                {
                    Vector2 uv = _uvs[i];
                    uv.X += 2 * UMath.Round(vb.uvs2[i].X);
                    _uvs[i] = uv;
                }
            }

            int indexCount = vb.triangles.Count;
            if (_triangles == null || _triangles.Length < indexCount)
                _triangles = new int[Math.Max(indexCount, 6)];
            vb.triangles.CopyTo(_triangles);

            _vertCount = vertCount;
            _indexCount = indexCount;
            vb.End();

            Redraw();

            if (meshModifier != null)
                meshModifier();
        }

        void ClearMesh()
        {
            // [Godot] Unity: mesh.Clear()
            _vertCount = 0;
            _indexCount = 0;
            Redraw();
        }

        void Redraw()
        {
            if (node != null && GodotObject.IsInstanceValid(node))
                node.QueueRedraw();
        }

        public void OnPopulateMesh(VertexBuffer vb)
        {
            Rect rect = texture.GetDrawRect(vb.contentRect, flip);

            vb.AddQuad(rect, vb.vertexColor, vb.uvRect);
            vb.AddTriangles();
            vb._isArbitraryQuad = _vertexMatrix != null;
        }

        public NGraphics CreateSubInstance(string name)
        {
            if (subInstances == null)
                subInstances = new List<NGraphics>();

            // [Godot] Unity: new GameObject(name) parented to this gameObject (layer and hideFlags copied).
            Node2D newNode = new Node2D();
            newNode.Name = SanitizeNodeName(name);
            newNode.VisibilityLayer = node.VisibilityLayer;
            node.AddChild(newNode, false, Node.InternalMode.Front);

            var newGraphics = new NGraphics(newNode);
            newGraphics._vertexMatrix = _vertexMatrix;
            // [Godot] a plain Node2D has no _Draw override: submit the sub mesh from its draw signal
            newNode.Draw += () => newGraphics.Draw();
            return newGraphics;
        }

        static string SanitizeNodeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Graphics";
            return name.Replace('.', '_').Replace(':', '_').Replace('@', '_').Replace('/', '_').Replace('"', '_').Replace('%', '_');
        }

        /// <summary>
        /// [Godot] Submits the cached mesh to the node's CanvasItem. Only valid inside the node's _Draw (Godot clears the
        /// CanvasItem commands before a redraw). Does nothing while disabled, or while this graphics is a stencil-style
        /// mask (_PreUpdateMask was called this frame: Unity draws such masks into the stencil only, the caller then
        /// draws them into its own clip node with Draw(Rid, Transform2D)). Masks that use the node itself as the
        /// clip_children parent must not call _PreUpdateMask.
        /// </summary>
        public void Draw()
        {
            if (_maskFlag != 0)
            {
                _drawnTexture = _texture != null ? _texture.nativeTexture : null;
                return;
            }

            Draw(node.GetCanvasItem(), null);
        }

        /// <summary>
        /// [Godot] Submits the cached mesh to another CanvasItem (e.g. a clip_children node that draws a mask).
        /// Must be called while that CanvasItem is being drawn.
        /// </summary>
        /// <param name="canvasItem"></param>
        /// <param name="transform">Optional transform applied to the mesh (e.g. the mask transform relative to the clip node).</param>
        public void Draw(Rid canvasItem, Transform2D? transform)
        {
            Texture2D tex = _texture != null ? _texture.nativeTexture : null;
            _drawnTexture = tex;

            if (!_enabled || _vertCount == 0 || _indexCount == 0 || _texture == null)
                return;

            // [Godot] Empty/solid textures are drawn without a texture: Godot uses its default white texture.
            // A texture that is not loaded (yet) draws nothing; Unity samples a black/transparent default there.
            Rid textureRid;
            if (_texture.isEmptyTexture)
                textureRid = default(Rid);
            else if (tex == null)
                return;
            else
                textureRid = tex.GetRid();

            int vertCount = _vertCount;
            if (_sPoints == null || _sPoints.Length < vertCount)
            {
                int cap = Math.Max(vertCount, 64);
                _sPoints = new Vector2[cap];
                _sUVs = new Vector2[cap];
            }

            Vector3[] vertices = _vertices;
            Vector2[] uvs = _uvs;
            Vector2[] points = _sPoints;
            Vector2[] outUVs = _sUVs;
            for (int i = 0; i < vertCount; i++)
            {
                // the only conversion point: vertex (x, y) -> (x, -y), uv (u, v) -> (u, 1 - v)
                Vector3 v = vertices[i];
                points[i] = new Vector2(v.X, -v.Y);
                Vector2 uv = uvs[i];
                outUVs[i] = new Vector2(uv.X, 1 - uv.Y);
            }

            if (transform != null)
                RenderingServer.CanvasItemAddSetTransform(canvasItem, (Transform2D)transform);

            RenderingServer.CanvasItemAddTriangleArray(canvasItem,
                new ReadOnlySpan<int>(_triangles, 0, _indexCount),
                new ReadOnlySpan<Vector2>(points, 0, vertCount),
                new ReadOnlySpan<Color>(_colors, 0, vertCount),
                new ReadOnlySpan<Vector2>(outUVs, 0, vertCount),
                ReadOnlySpan<int>.Empty,
                ReadOnlySpan<float>.Empty,
                textureRid,
                -1);

            if (transform != null)
                RenderingServer.CanvasItemAddSetTransform(canvasItem, Transform2D.Identity);
        }

        // [Godot] Assigns the material to node.Material. When SetShaderParameter was used, an instance-owned copy of the
        // material receives the parameters (Unity: MaterialPropertyBlock on the renderer). The copy is made again when
        // the source material changes (grayed, blend mode, keywords...).
        void ApplyMaterial(Material target)
        {
            if (target != null && _propertyBlock != null && _propertyBlock.Count > 0 && target is ShaderMaterial)
            {
                if (_ownMaterial == null || _ownMaterialSource != target)
                {
                    _ownMaterial = (ShaderMaterial)target.Duplicate();
                    _ownMaterialSource = target;
                    _blockUpdated = true;
                }
                if (_blockUpdated)
                {
                    foreach (var kv in _propertyBlock)
                        _ownMaterial.SetShaderParameter(kv.Key, kv.Value);
                    _blockUpdated = false;
                }
                target = _ownMaterial;
            }

            if (object.ReferenceEquals(target, _appliedMaterial))
                return;

            _appliedMaterial = target;
            if (node != null && GodotObject.IsInstanceValid(node))
                node.Material = target;
        }

        /// <summary>
        /// [Godot] Set on DisplayObject.paintingGraphics when its node hosts a CanvasGroup (painting mode, see
        /// DisplayObject.EnterPaintingMode). Such a graphics has no texture and no mesh (Godot draws the group rect
        /// itself); its material comes from ShaderConfig.paintingShader (or the shader set on this graphics, e.g.
        /// "FairyGUI/BlurFilter") and samples the group back buffer. ColorFilter, BlurFilter and blend modes of
        /// containers work through this material.
        /// </summary>
        public bool canvasGroup
        {
            get { return _canvasGroup; }
            set
            {
                if (_canvasGroup != value)
                {
                    _canvasGroup = value;
                    _material = null;
                    UpdateManager();
                    if (!_canvasGroup && _customMatarial == 0)
                        ApplyMaterial(null);
                }
            }
        }

        // [Godot] class StencilEraser removed: Godot masks with clip_children and needs no stencil erase pass.
    }
}
