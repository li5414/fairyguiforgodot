// Ported from FairyGUI-unity: Assets/Scripts/Core/Mesh/RectMesh.cs
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    public class RectMesh : IMeshFactory, IHitTest
    {
        /// <summary>
        ///
        /// </summary>
        public Rect? drawRect;

        /// <summary>
        ///
        /// </summary>
        public float lineWidth;

        /// <summary>
        ///
        /// </summary>
        public Color lineColor; // [Godot] Color32 -> Color

        /// <summary>
        ///
        /// </summary>
        public Color? fillColor; // [Godot] Color32? -> Color?

        /// <summary>
        ///
        /// </summary>
        public Color[] colors; // [Godot] Color32[] -> Color[]

        public RectMesh()
        {
            lineColor = UColor.black;
        }

        public void OnPopulateMesh(VertexBuffer vb)
        {
            Rect rect = drawRect != null ? (Rect)drawRect : vb.contentRect;
            Color color = fillColor != null ? (Color)fillColor : vb.vertexColor;
            if (lineWidth == 0)
            {
                if (color.A8 != 0)//optimized // [Godot] Color32.a (byte) -> Color.A8
                    vb.AddQuad(rect, color);
            }
            else
            {
                Rect part;

                //left,right
                part = new Rect(rect.x, rect.y, lineWidth, rect.height);
                vb.AddQuad(part, lineColor);
                part = new Rect(rect.xMax - lineWidth, rect.y, lineWidth, rect.height);
                vb.AddQuad(part, lineColor);

                //top, bottom
                part = new Rect(rect.x + lineWidth, rect.y, rect.width - lineWidth * 2, lineWidth);
                vb.AddQuad(part, lineColor);
                part = new Rect(rect.x + lineWidth, rect.yMax - lineWidth, rect.width - lineWidth * 2, lineWidth);
                vb.AddQuad(part, lineColor);

                //middle
                if (color.A8 != 0)//optimized // [Godot] Color32.a (byte) -> Color.A8
                {
                    part = Rect.MinMaxRect(rect.x + lineWidth, rect.y + lineWidth, rect.xMax - lineWidth, rect.yMax - lineWidth);
                    if (part.width > 0 && part.height > 0)
                        vb.AddQuad(part, color);
                }
            }

            if (colors != null)
                vb.RepeatColors(colors, 0, vb.currentVertCount);

            vb.AddTriangles();
        }

        public bool HitTest(Rect contentRect, Vector2 point)
        {
            return contentRect.Contains(point);
        }
    }
}
