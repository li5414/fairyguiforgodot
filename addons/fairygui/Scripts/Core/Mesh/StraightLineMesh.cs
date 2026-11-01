// Ported from FairyGUI-unity: Assets/Scripts/Core/Mesh/StraightLineMesh.cs
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    public class StraightLineMesh : IMeshFactory
    {
        /// <summary>
        ///
        /// </summary>
        public Color color;

        /// <summary>
        ///
        /// </summary>
        public Vector3 origin;

        /// <summary>
        ///
        /// </summary>
        public Vector3 end;

        /// <summary>
        ///
        /// </summary>
        public float lineWidth;

        /// <summary>
        ///
        /// </summary>
        public bool repeatFill;

        public StraightLineMesh()
        {
            color = UColor.black;
            lineWidth = 1;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="lineWidth"></param>
        /// <param name="color"></param>
        /// <param name="repeatFill"></param>
        public StraightLineMesh(float lineWidth, Color color, bool repeatFill)
        {
            this.lineWidth = lineWidth;
            this.color = color;
            this.repeatFill = repeatFill;
        }

        public void OnPopulateMesh(VertexBuffer vb)
        {
            // [Godot] Unity's Vector3 == is approximate (squared distance < 1e-10) while Godot's is exact,
            // so the Unity test is written out explicitly.
            if ((origin - end).LengthSquared() < 9.99999944E-11f)
                return;

            float length = origin.ToV2().DistanceTo(end.ToV2()); // Unity: Vector2.Distance(origin, end) (z dropped)
            Vector3 lineVector = end - origin;
            Vector3 widthVector = lineVector.Cross(new Vector3(0, 0, 1));
            widthVector = widthVector.Normalized();

            // [Godot] Declared as Vector2 (Unity declared Vector3 and relied on implicit Vector2<->Vector3 conversion; z was always 0).
            Vector2 v0, v1, v2, v3;

            if (repeatFill)
            {
                float ratio = length / vb.textureSize.X;
                v0 = VertexBuffer.NormalizedUV[0];
                v1 = VertexBuffer.NormalizedUV[1];
                v2 = new Vector2(ratio, 1);
                v3 = new Vector2(ratio, 0);
            }
            else
            {
                v0 = new Vector2(vb.uvRect.xMin, vb.uvRect.yMin);
                v1 = new Vector2(vb.uvRect.xMin, vb.uvRect.yMax);
                v2 = new Vector2(vb.uvRect.xMax, vb.uvRect.yMax);
                v3 = new Vector2(vb.uvRect.xMax, vb.uvRect.yMin);
            }

            vb.AddVert(origin - widthVector * lineWidth * 0.5f, color, v0);
            vb.AddVert(origin + widthVector * lineWidth * 0.5f, color, v1);
            vb.AddVert(end + widthVector * lineWidth * 0.5f, color, v2);
            vb.AddVert(end - widthVector * lineWidth * 0.5f, color, v3);

            vb.AddTriangles();
        }
    }
}
