// Ported from FairyGUI-unity: Assets/Scripts/Core/StageCamera.cs
using System;
using Godot;

namespace FairyGUI
{
    /// <summary>
    /// Stage Camera is an orthographic camera for UI rendering.
    /// </summary>
    // [Godot] Godot 2D needs no stage camera: the Stage is drawn by a CanvasLayer owned by StageEngine
    // (layer = Stage.canvasLayer) in viewport pixels, unaffected by Camera2D. This was a MonoBehaviour
    // attached to a Camera; only the static members other code references are kept, so it is now a
    // static class. All per-camera members (constantSize, unitsPerPixel instance field, cachedTransform,
    // cachedCamera, OnEnable/Update/OnScreenSizeChanged/OnRenderObject, static main, CreateCamera) are removed.
    public static class StageCamera
    {
        /// <summary>
        /// World units per stage pixel.
        /// </summary>
        // [Godot] Replaces the per-camera `unitsPerPixel` field. The Godot stage is never scaled, so this is always 1.
        public static float UnitsPerPixel = 1;

        /// <summary>
        ///
        /// </summary>
        // [Godot] Incremented by Stage whenever the viewport size changes (and by UIContentScaler.ApplyChange),
        // exactly where Unity's StageCamera.OnScreenSizeChanged incremented it.
        public static int screenSizeVer = 1;

        public const string Name = "Stage Camera";
        public const string LayerName = "UI";

        // [Godot] Kept for API compatibility only; nothing in the Godot port reads them.
        public static float DefaultCameraSize = 5;
        public static float DefaultUnitsPerPixel = 0.02f;

        // [Godot] OnRenderObject drove EMRenderSupport.Update() in edit mode. Godot has no edit-mode
        // rendering for FairyGUI (scripts are not [Tool]), so nothing calls EMRenderSupport.Update().

        /// <summary>
        /// Force the stage size to be re-evaluated (and onStageResized dispatched) on the next frame.
        /// </summary>
        // [Godot] Was an instance method that reset the camera's cached screen size.
        public static void ApplyModifiedProperties()
        {
            Stage.ForceScreenSizeCheck();
        }

        /// <summary>
        /// Check if there is a stage camera in the scene. If none, create one.
        /// </summary>
        // [Godot] Intentionally a no-op: the Godot stage renders through a CanvasLayer and needs no camera.
        // Unity also cached Camera.main into HitTestContext.cachedMainCamera here; Godot hit testing uses
        // viewport coordinates and needs no camera either.
        public static void CheckMainCamera()
        {
        }

        /// <summary>
        ///
        /// </summary>
        // [Godot] Intentionally a no-op: painting mode / cacheAsBitmap use CanvasGroup, not a capture camera.
        public static void CheckCaptureCamera()
        {
        }
    }
}
