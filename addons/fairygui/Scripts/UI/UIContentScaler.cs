// Ported from FairyGUI-unity: Assets/Scripts/UI/UIContentScaler.cs
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    // [Godot] Was a MonoBehaviour ([ExecuteInEditMode], [AddComponentMenu]) attached to the Stage GameObject.
    // Now a plain class owned by the Stage: use Stage.inst.contentScaler (replaces
    // Stage.inst.gameObject.GetComponent<UIContentScaler>()).
    public class UIContentScaler
    {
        /// <summary>
        ///
        /// </summary>
        public enum ScaleMode
        {
            ConstantPixelSize,
            ScaleWithScreenSize,
            ConstantPhysicalSize
        }

        /// <summary>
        ///
        /// </summary>
        public enum ScreenMatchMode
        {
            MatchWidthOrHeight,
            MatchWidth,
            MatchHeight
        }

        /// <summary>
        ///
        /// </summary>
        public ScaleMode scaleMode;

        /// <summary>
        ///
        /// </summary>
        public ScreenMatchMode screenMatchMode;

        /// <summary>
        ///
        /// </summary>
        public int designResolutionX;

        /// <summary>
        ///
        /// </summary>
        public int designResolutionY;

        /// <summary>
        ///
        /// </summary>
        public int fallbackScreenDPI = 96;

        /// <summary>
        ///
        /// </summary>
        public int defaultSpriteDPI = 96;

        /// <summary>
        ///
        /// </summary>
        public float constantScaleFactor = 1;

        /// <summary>
        /// 当false时，计算比例时会考虑designResolutionX/Y的设置是针对横屏还是竖屏。否则不考虑。
        /// </summary>
        public bool ignoreOrientation = false;

        public static float scaleFactor = 1;

        public static int scaleLevel = 0;

        /// <summary>
        /// Copy the settings of this scaler to the Stage's scaler (Stage.inst.contentScaler) and apply them.
        /// </summary>
        // [Godot] Replaces OnEnable (play-mode branch): a scene-placed UIContentScaler component used to forward
        // its settings to the Stage's scaler when enabled. The edit-mode branch (deferred ApplyChange from Update)
        // is removed, there is no edit-mode rendering in Godot.
        public void ApplyToStage()
        {
            //播放模式下都是通过Stage自带的UIContentScaler实现调整的，所以这里只是把参数传过去
            UIContentScaler scaler = Stage.inst.contentScaler;
            if (scaler != this)
            {
                scaler.scaleMode = scaleMode;
                if (scaleMode == ScaleMode.ScaleWithScreenSize)
                {
                    scaler.designResolutionX = designResolutionX;
                    scaler.designResolutionY = designResolutionY;
                    scaler.screenMatchMode = screenMatchMode;
                    scaler.ignoreOrientation = ignoreOrientation;
                }
                else if (scaleMode == ScaleMode.ConstantPhysicalSize)
                {
                    scaler.fallbackScreenDPI = fallbackScreenDPI;
                    scaler.defaultSpriteDPI = defaultSpriteDPI;
                }
                else
                {
                    scaler.constantScaleFactor = constantScaleFactor;
                }
            }
            // [Godot] Unity applied only inside the `scaler != this` branch; applying unconditionally lets
            // Stage.inst.contentScaler.ApplyToStage() be used after editing the stage scaler directly.
            scaler.ApplyChange();
            GRoot.inst.ApplyContentScaleFactor();
        }

        // [Godot] Update() and OnDestroy() removed: they only served edit mode (deferred ApplyChange,
        // resetting scaleFactor/scaleLevel when the component was destroyed outside play mode).

        //For UIContentScalerEditor Only, as the Screen.width/height is not correct in OnInspectorGUI
        /// <summary>
        ///
        /// </summary>
        // [Godot] Unity set a flag consumed by the next Update(). A plain class has no Update, so the change is
        // applied immediately; if this is the Stage's scaler, GRoot is rescaled too.
        public void ApplyModifiedProperties()
        {
            ApplyChange();
            if (Stage.inst.contentScaler == this)
                GRoot.inst.ApplyContentScaleFactor();
        }

        /// <summary>
        ///
        /// </summary>
        public void ApplyChange()
        {
            float screenWidth;
            float screenHeight;

            // [Godot] Always the play-mode branch: the size of the Stage (= viewport visible rect).
            // The edit-mode branch (Screen.width/height) is unreachable in Godot (no [Tool]).
            //In case of multi display， we keep using the display which Stage object resides.
            screenWidth = Stage.inst.width;
            screenHeight = Stage.inst.height;

            if (scaleMode == ScaleMode.ScaleWithScreenSize)
            {
                if (designResolutionX == 0 || designResolutionY == 0)
                    return;

                int dx = designResolutionX;
                int dy = designResolutionY;
                if (!ignoreOrientation && (screenWidth > screenHeight && dx < dy || screenWidth < screenHeight && dx > dy))
                {
                    //scale should not change when orientation change
                    int tmp = dx;
                    dx = dy;
                    dy = tmp;
                }

                if (screenMatchMode == ScreenMatchMode.MatchWidthOrHeight)
                {
                    float s1 = (float)screenWidth / dx;
                    float s2 = (float)screenHeight / dy;
                    scaleFactor = UMath.Min(s1, s2);
                }
                else if (screenMatchMode == ScreenMatchMode.MatchWidth)
                    scaleFactor = (float)screenWidth / dx;
                else
                    scaleFactor = (float)screenHeight / dy;
            }
            else if (scaleMode == ScaleMode.ConstantPhysicalSize)
            {
                // [Godot] Screen.dpi -> DisplayServer.ScreenGetDpi()
                float dpi = DisplayServer.ScreenGetDpi();
                if (dpi == 0)
                    dpi = fallbackScreenDPI;
                if (dpi == 0)
                    dpi = 96;
                scaleFactor = dpi / (defaultSpriteDPI == 0 ? 96 : defaultSpriteDPI);
            }
            else
                scaleFactor = constantScaleFactor;

            if (scaleFactor > 10)
                scaleFactor = 10;

            UpdateScaleLevel();

            StageCamera.screenSizeVer++;
        }

        void UpdateScaleLevel()
        {
            if (scaleFactor > 3)
                scaleLevel = 3; //x4
            else if (scaleFactor > 2)
                scaleLevel = 2; //x3
            else if (scaleFactor > 1)
                scaleLevel = 1; //x2
            else
                scaleLevel = 0;
        }
    }
}
