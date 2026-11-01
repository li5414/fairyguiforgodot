// Ported from FairyGUI-unity: Assets/Scripts/Core/HitTest/ColliderHitTest.cs
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    public class ColliderHitTest : IHitTest
    {
        /// <summary>
        /// [Godot] Informational only. Godot UI hit testing does not use 2D physics colliders, so this can be
        /// any node (or null). It is also used as the cache key when it is set.
        /// </summary>
        public Node collider;

        /// <summary>
        /// [Godot] Replaces Unity's collider.enabled (DisplayObject.touchable toggles it).
        /// </summary>
        public bool enabled = true;

        /// <summary>
        ///
        /// </summary>
        /// <param name="contentRect"></param>
        /// <param name="localPoint"></param>
        /// <returns></returns>
        virtual public bool HitTest(Rect contentRect, Vector2 localPoint)
        {
            if (!enabled)
                return false;

            // [Godot] Unity raycast the collider through the camera. Without 2D colliders the collider
            // area is the content rect of the owner. The per-frame cache is kept.
            object key = collider != null ? (object)collider : this;
            bool hit;
            if (!HitTestContext.GetRaycastHitFromCache(key, out hit))
            {
                hit = contentRect.Contains(localPoint);
                HitTestContext.CacheRaycastHit(key, hit);
            }

            if (!hit)
                return false;

            return true;
        }
    }
}
