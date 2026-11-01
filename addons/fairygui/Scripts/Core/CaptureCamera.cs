// Ported from FairyGUI-unity: Assets/Scripts/Core/CaptureCamera.cs
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    /// <remarks>
    /// [Godot] Godot 2D rendering needs no capture camera: painting mode (filters, blend modes, cacheAsBitmap) uses
    /// CanvasGroup and UIPainter uses a SubViewport. Unity's CaptureCamera MonoBehaviour (camera creation, CheckMain,
    /// CreateRenderTexture, Capture) is removed. Only the static layer members are kept, as inert values, because
    /// display code refers to them (e.g. SetChildrenLayer(CaptureCamera.hiddenLayer) when entering painting mode).
    /// They are NOT Godot visibility layers and must not hide anything.
    /// </remarks>
    public static class CaptureCamera
    {
        static int _layer = -1;
        static int _hiddenLayer = -1;

        public const string Name = "Capture Camera";
        public const string LayerName = "VUI";
        public const string HiddenLayerName = "Hidden VUI";

        /// <summary>
        ///
        /// </summary>
        public static int layer
        {
            get
            {
                if (_layer == -1)
                {
                    // [Godot] Unity: LayerMask.NameToLayer(LayerName), default 30. Godot has no named render layers for
                    // 2D capture, so the Unity default index is returned without a warning.
                    _layer = 30;
                }

                return _layer;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public static int hiddenLayer
        {
            get
            {
                if (_hiddenLayer == -1)
                {
                    // [Godot] Unity: LayerMask.NameToLayer(HiddenLayerName), default 31. See layer.
                    _hiddenLayer = 31;
                }

                return _hiddenLayer;
            }
        }
    }
}
