// Ported from FairyGUI-unity: Assets/Scripts/Core/NTexture.cs
using System;
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    /// <remarks>
    /// [Godot] Godot textures are reference counted resources; every DestroyMethod except Custom just drops the reference.
    /// </remarks>
    public enum DestroyMethod
    {
        Destroy,
        Unload,
        None,
        ReleaseTemp,
        Custom
    }

    /// <summary>
    ///
    /// </summary>
    /// <remarks>
    /// [Godot] Wraps a Godot.Texture2D. uvRect keeps the Unity convention (origin bottom-left, v up); NGraphics converts
    /// it when submitting (v -> 1 - v). alphaTexture is always null (Godot textures carry their own alpha).
    /// </remarks>
    public class NTexture
    {
        /// <summary>
        /// This event will trigger when a texture is destroying if its destroyMethod is Custom
        /// </summary>
        public static event Action<Texture2D> CustomDestroyMethod;

        /// <summary>
        ///
        /// </summary>
        public Rect uvRect;

        /// <summary>
        ///
        /// </summary>
        public bool rotated;

        /// <summary>
        ///
        /// </summary>
        public int refCount;

        /// <summary>
        ///
        /// </summary>
        public float lastActive;

        /// <summary>
        ///
        /// </summary>
        public DestroyMethod destroyMethod;

        /// <summary>
        /// This event will trigger when texture reloaded and size changed.
        /// </summary>
        public event Action<NTexture> onSizeChanged;

        /// <summary>
        /// This event will trigger when ref count is zero.
        /// </summary>
        public event Action<NTexture> onRelease;

        Texture2D _nativeTexture;
        Texture2D _alphaTexture;

        Rect _region;
        Vector2 _offset;
        Vector2 _originalSize;

        NTexture _root;
        Dictionary<string, MaterialManager> _materialManagers;

        internal static Texture2D CreateEmptyTexture()
        {
            // [Godot] Unity: 1x1 RGB24 white Texture2D.
            Godot.Image img = Godot.Image.CreateEmpty(1, 1, false, Godot.Image.Format.Rgb8);
            img.SetPixel(0, 0, UColor.white);
            ImageTexture emptyTexture = ImageTexture.CreateFromImage(img);
            emptyTexture.ResourceName = "White Texture";
            return emptyTexture;
        }

        static NTexture _empty;

        /// <summary>
        ///
        /// </summary>
        public static NTexture Empty
        {
            get
            {
                if (_empty == null)
                    _empty = new NTexture(CreateEmptyTexture());

                return _empty;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public static void DisposeEmpty()
        {
            if (_empty != null)
            {
                NTexture tmp = _empty;
                _empty = null;
                tmp.Dispose();
            }
        }

        /// <summary>
        /// [Godot] True for NTexture.Empty and textures cut from it. NGraphics draws them without a texture (default RID),
        /// which Godot renders as solid white, the same result as Unity's 1x1 white texture.
        /// </summary>
        internal bool isEmptyTexture
        {
            get { return _empty != null && _root == _empty; }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="texture"></param>
        public NTexture(Texture2D texture) : this(texture, null, 1, 1)
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="alphaTexture">[Godot] ignored, Godot textures carry their own alpha</param>
        /// <param name="xScale"></param>
        /// <param name="yScale"></param>
        public NTexture(Texture2D texture, Texture2D alphaTexture, float xScale, float yScale)
        {
            _root = this;
            _nativeTexture = texture;
            _alphaTexture = null; // [Godot] alphaTexture is always null
            uvRect = new Rect(0, 0, xScale, yScale);
            if (yScale < 0)
            {
                uvRect.y = -yScale;
                uvRect.yMax = 0;
            }
            if (xScale < 0)
            {
                uvRect.x = -xScale;
                uvRect.xMax = 0;
            }
            if (_nativeTexture != null)
                _originalSize = new Vector2(_nativeTexture.GetWidth(), _nativeTexture.GetHeight());
            _region = new Rect(0, 0, _originalSize.X, _originalSize.Y);

            // [Godot] An AtlasTexture is drawn with the RID of its atlas, so draw from the atlas with the sub-region uv
            // (the Godot counterpart of Unity's NTexture(Sprite)). Margin becomes offset/originalSize.
            if (texture is AtlasTexture at && at.Atlas != null)
            {
                Texture2D atlas = at.Atlas;
                float aw = atlas.GetWidth();
                float ah = atlas.GetHeight();
                Rect r = at.Region;
                if (r.width <= 0 || r.height <= 0)
                    r = new Rect(0, 0, aw, ah);

                _nativeTexture = atlas;
                Rect sub = new Rect(r.x / aw, 1 - r.yMax / ah, r.width / aw, r.height / ah);
                uvRect = new Rect(sub.x + uvRect.x * sub.width, sub.y + uvRect.y * sub.height,
                    uvRect.width * sub.width, uvRect.height * sub.height);
                _region = new Rect(0, 0, r.width, r.height);
                _offset = at.Margin.Position;
                _originalSize = new Vector2(r.width + at.Margin.Size.X, r.height + at.Margin.Size.Y);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="region"></param>
        public NTexture(Texture2D texture, Rect region)
        {
            _root = this;
            _nativeTexture = texture;
            _region = region;
            _originalSize = new Vector2(_region.width, _region.height);
            if (_nativeTexture != null)
                uvRect = new Rect(region.x / _nativeTexture.GetWidth(), 1 - region.yMax / _nativeTexture.GetHeight(),
                    region.width / _nativeTexture.GetWidth(), region.height / _nativeTexture.GetHeight());
            else
                uvRect.Set(0, 0, 1, 1);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="root"></param>
        /// <param name="region"></param>
        /// <param name="rotated"></param>
        public NTexture(NTexture root, Rect region, bool rotated)
        {
            _root = root;
            this.rotated = rotated;
            region.x += root._region.x;
            region.y += root._region.y;
            uvRect = new Rect(region.x * root.uvRect.width / root.width, 1 - region.yMax * root.uvRect.height / root.height,
                region.width * root.uvRect.width / root.width, region.height * root.uvRect.height / root.height);
            if (rotated)
            {
                float tmp = region.width;
                region.width = region.height;
                region.height = tmp;

                tmp = uvRect.width;
                uvRect.width = uvRect.height;
                uvRect.height = tmp;
            }
            _region = region;
            _originalSize = _region.size;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="root"></param>
        /// <param name="region"></param>
        /// <param name="rotated"></param>
        /// <param name="originalSize"></param>
        /// <param name="offset"></param>
        public NTexture(NTexture root, Rect region, bool rotated, Vector2 originalSize, Vector2 offset)
            : this(root, region, rotated)
        {
            _originalSize = originalSize;
            _offset = offset;
        }

        // [Godot] NTexture(Sprite) removed: Godot has no Sprite asset. Pass an AtlasTexture to NTexture(Texture2D) instead.

        /// <summary>
        ///
        /// </summary>
        public int width
        {
            get { return (int)_region.width; }
        }

        /// <summary>
        ///
        /// </summary>
        public int height
        {
            get { return (int)_region.height; }
        }

        /// <summary>
        ///
        /// </summary>
        public Vector2 offset
        {
            get { return _offset; }
            set { _offset = value; }
        }

        /// <summary>
        ///
        /// </summary>
        public Vector2 originalSize
        {
            get { return _originalSize; }
            set { _originalSize = value; }
        }

        /// <summary>
        /// [Godot] Region of this texture inside the root texture, in pixels (top-left origin, as in the FairyGUI atlas).
        /// </summary>
        public Rect region
        {
            get { return _region; }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="drawRect"></param>
        /// <returns></returns>
        public Rect GetDrawRect(Rect drawRect)
        {
            return GetDrawRect(drawRect, FlipType.None);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="drawRect"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public Rect GetDrawRect(Rect drawRect, FlipType flip)
        {
            if (_originalSize.X == _region.width && _originalSize.Y == _region.height)
                return drawRect;

            float sx = drawRect.width / _originalSize.X;
            float sy = drawRect.height / _originalSize.Y;
            Rect rect = new Rect(_offset.X * sx, _offset.Y * sy, _region.width * sx, _region.height * sy);

            if (flip != FlipType.None)
            {
                if (flip == FlipType.Horizontal || flip == FlipType.Both)
                {
                    rect.x = drawRect.width - rect.xMax;
                }
                if (flip == FlipType.Vertical || flip == FlipType.Both)
                {
                    rect.y = drawRect.height - rect.yMax;
                }
            }

            return rect;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="uv"></param>
        public void GetUV(Vector2[] uv)
        {
            uv[0] = uvRect.position;
            uv[1] = new Vector2(uvRect.xMin, uvRect.yMax);
            uv[2] = new Vector2(uvRect.xMax, uvRect.yMax);
            uv[3] = new Vector2(uvRect.xMax, uvRect.yMin);
            if (rotated)
            {
                float xMin = uvRect.xMin;
                float yMin = uvRect.yMin;
                float yMax = uvRect.yMax;

                float tmp;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 m = uv[i];
                    tmp = m.Y;
                    m.Y = yMin + m.X - xMin;
                    m.X = xMin + yMax - tmp;
                    uv[i] = m;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public NTexture root
        {
            get { return _root; }
        }

        /// <summary>
        ///
        /// </summary>
        public bool disposed
        {
            get { return _root == null; }
        }

        /// <summary>
        ///
        /// </summary>
        public Texture2D nativeTexture
        {
            get { return _root != null ? _root._nativeTexture : null; }
        }

        /// <summary>
        ///
        /// </summary>
        public Texture2D alphaTexture
        {
            get { return _root != null ? _root._alphaTexture : null; }
        }

        /// <summary>
        ///
        /// </summary>
        public MaterialManager GetMaterialManager(string shaderName)
        {
            if (_root != this)
            {
                if (_root == null)
                    return null;
                else
                    return _root.GetMaterialManager(shaderName);
            }

            if (_materialManagers == null)
                _materialManagers = new Dictionary<string, MaterialManager>();

            MaterialManager mm;
            if (!_materialManagers.TryGetValue(shaderName, out mm))
            {
                mm = new MaterialManager(this, ShaderConfig.GetShader(shaderName));
                _materialManagers.Add(shaderName, mm);
            }

            return mm;
        }

        /// <summary>
        ///
        /// </summary>
        public void Unload()
        {
            Unload(false);
        }

        /// <summary>
        ///
        /// </summary>
        public void Unload(bool destroyMaterials)
        {
            if (this == _empty)
                return;

            if (_root != this)
                throw new Exception("Unload is not allow to call on none root NTexture.");

            if (_nativeTexture != null)
            {
                DestroyTexture();

                if (destroyMaterials)
                    DestroyMaterials();
                else
                    RefreshMaterials();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="nativeTexture"></param>
        /// <param name="alphaTexture">[Godot] ignored, alphaTexture is always null</param>
        public void Reload(Texture2D nativeTexture, Texture2D alphaTexture)
        {
            if (_root != this)
                throw new System.Exception("Reload is not allow to call on none root NTexture.");

            if (_nativeTexture != null && _nativeTexture != nativeTexture)
                DestroyTexture();

            _nativeTexture = nativeTexture;
            _alphaTexture = null; // [Godot] alphaTexture is always null

            Vector2 lastSize = _originalSize;
            if (_nativeTexture != null)
                _originalSize = new Vector2(_nativeTexture.GetWidth(), _nativeTexture.GetHeight());
            else
                _originalSize = Vector2.Zero;
            _region = new Rect(0, 0, _originalSize.X, _originalSize.Y);

            RefreshMaterials();

            if (onSizeChanged != null && lastSize != _originalSize)
                onSizeChanged(this);
        }

        void DestroyTexture()
        {
            switch (destroyMethod)
            {
                case DestroyMethod.Destroy:
                case DestroyMethod.Unload:
                case DestroyMethod.ReleaseTemp:
                    // [Godot] Unity: DestroyImmediate / Resources.UnloadAsset / RenderTexture.ReleaseTemporary.
                    // Godot textures are reference counted; dropping the reference below releases them once unused.
                    break;
                case DestroyMethod.Custom:
                    if (CustomDestroyMethod == null)
                        GD.PushWarning("NTexture.CustomDestroyMethod must be set to handle DestroyMethod.Custom");
                    else
                    {
                        CustomDestroyMethod(_nativeTexture);
                        if (_alphaTexture != null)
                            CustomDestroyMethod(_alphaTexture);
                    }
                    break;
            }

            _nativeTexture = null;
            _alphaTexture = null;
        }

        void RefreshMaterials()
        {
            if (_materialManagers != null && _materialManagers.Count > 0)
            {
                Dictionary<string, MaterialManager>.Enumerator iter = _materialManagers.GetEnumerator();
                while (iter.MoveNext())
                    iter.Current.Value.RefreshMaterials();
                iter.Dispose();
            }
        }

        void DestroyMaterials()
        {
            if (_materialManagers != null && _materialManagers.Count > 0)
            {
                Dictionary<string, MaterialManager>.Enumerator iter = _materialManagers.GetEnumerator();
                while (iter.MoveNext())
                    iter.Current.Value.DestroyMaterials();
                iter.Dispose();
            }
        }

        public void AddRef()
        {
            if (_root == null) //disposed
                return;

            if (_root != this && refCount == 0)
                _root.AddRef();

            refCount++;
        }

        public void ReleaseRef()
        {
            if (_root == null) //disposed
                return;

            refCount--;

            if (refCount == 0)
            {
                if (_root != this)
                    _root.ReleaseRef();

                if (onRelease != null)
                    onRelease(this);
            }
        }

        /// <summary>
        ///
        /// </summary>
        public void Dispose()
        {
            if (this == _empty)
                return;

            if (_root == this)
                Unload(true);
            _root = null;
            onSizeChanged = null;
            onRelease = null;
        }
    }
}
