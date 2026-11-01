// Ported from FairyGUI-unity: Assets/Scripts/Core/HitTest/HitTestContext.cs
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    public class HitTestContext
    {
        //set before hit test
        // [Godot] screenPoint is in viewport (stage) coordinates, y down, z = 0. There is no y flip and no
        // display index in z. worldPoint is in canvas global coordinates; for objects under the Stage it
        // equals screenPoint. Local point = node.GetGlobalTransformWithCanvas().AffineInverse() * worldPoint.
        public static Vector3 screenPoint;
        public static Vector3 worldPoint;
        public static Vector3 direction;
        public static bool forTouch;

        // [Godot] Kept for API compatibility. Godot 2D UI does not raycast physics, so they are not used.
        public static int layerMask = -1;
        public static float maxDistance = UMath.Infinity;

        // [Godot] Unity cached one RaycastHit per Camera. There is no Camera/Ray here, so the cache stores
        // one hit result per (hit test key, screen point) pair. It is cleared once per frame by the stage
        // (ClearRaycastHitCache), exactly like the Unity version.
        static Dictionary<(object, Vector3), bool?> raycastHits = new Dictionary<(object, Vector3), bool?>();

        /// <summary>
        ///
        /// </summary>
        /// <param name="key">The object whose hit result is cached (usually a ColliderHitTest or its collider).</param>
        /// <param name="hit">The cached result. Only valid if the method returns true.</param>
        /// <returns>True if a result for the current screenPoint is in the cache.</returns>
        public static bool GetRaycastHitFromCache(object key, out bool hit)
        {
            bool? hitRef;
            if (!raycastHits.TryGetValue((key, screenPoint), out hitRef) || hitRef == null)
            {
                hit = false;
                return false;
            }
            else
            {
                hit = (bool)hitRef;
                return true;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hit"></param>
        public static void CacheRaycastHit(object key, bool hit)
        {
            raycastHits[(key, screenPoint)] = hit;
        }

        /// <summary>
        ///
        /// </summary>
        public static void ClearRaycastHitCache()
        {
            raycastHits.Clear();
        }
    }

}
