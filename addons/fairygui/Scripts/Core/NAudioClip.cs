// Ported from FairyGUI-unity: Assets/Scripts/Core/NAudioClip.cs
using System;
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    public class NAudioClip
    {
        // [Godot] AudioClip -> Godot.AudioStream
        public static Action<AudioStream> CustomDestroyMethod;

        /// <summary>
        ///
        /// </summary>
        public DestroyMethod destroyMethod;

        /// <summary>
        ///
        /// </summary>
        public AudioStream nativeClip;

        /// <summary>
        ///
        /// </summary>
        /// <param name="audioClip"></param>
        public NAudioClip(AudioStream audioClip)
        {
            nativeClip = audioClip;
        }

        /// <summary>
        ///
        /// </summary>
        public void Unload()
        {
            if (nativeClip == null)
                return;

            if (destroyMethod == DestroyMethod.Unload)
            {
                // [Godot] Resources.UnloadAsset has no equivalent: Godot resources are reference counted
                // and are released when the last reference (cleared below) goes away.
            }
            else if (destroyMethod == DestroyMethod.Destroy)
            {
                // [Godot] Object.DestroyImmediate(asset) has no equivalent for a RefCounted resource;
                // dropping our reference (below) frees it once nothing else holds it.
            }
            else if (destroyMethod == DestroyMethod.Custom)
            {
                if (CustomDestroyMethod == null)
                    GD.PushWarning("NAudioClip.CustomDestroyMethod must be set to handle DestroyMethod.Custom");
                else
                    CustomDestroyMethod(nativeClip);
            }

            nativeClip = null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="audioClip"></param>
        public void Reload(AudioStream audioClip)
        {
            if (nativeClip != null && nativeClip != audioClip)
                Unload();

            nativeClip = audioClip;
        }

        // [Godot] RuntimeInitializeOnLoadMethod (domain-reload reset) removed: Godot resets statics on assembly reload.
    }
}
