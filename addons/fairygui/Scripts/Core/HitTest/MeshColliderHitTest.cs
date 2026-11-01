// Ported from FairyGUI-unity: Assets/Scripts/Core/HitTest/MeshColliderHitTest.cs
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    public class MeshColliderHitTest : ColliderHitTest
    {
        public Vector2 lastHit;

        /// <summary>
        ///
        /// </summary>
        /// <param name="collider">[Godot] The 3D node that shows the UI (e.g. a MeshInstance3D using the UIPainter texture).</param>
        public MeshColliderHitTest(Node collider)
        {
            this.collider = collider;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="contentRect"></param>
        /// <param name="localPoint"></param>
        /// <returns></returns>
        override public bool HitTest(Rect contentRect, Vector2 localPoint)
        {
            // [Godot] Input on 3D surfaces (UIPainter) is not supported: there is no ray/texture-coordinate
            // hit in this port, so the surface is never hit. Unity computed lastHit from hit.textureCoord and
            // moved HitTestContext.worldPoint onto the stage plane here.
            lastHit = new Vector2(float.NaN, float.NaN);
            return false;
        }
    }
}
