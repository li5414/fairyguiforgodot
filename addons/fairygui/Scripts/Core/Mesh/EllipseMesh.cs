// Ported from FairyGUI-unity: Assets/Scripts/Core/Mesh/EllipseMesh.cs
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    public class EllipseMesh : IMeshFactory, IHitTest
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
        public Color? centerColor; // [Godot] Color32? -> Color?

        /// <summary>
        ///
        /// </summary>
        public Color? fillColor; // [Godot] Color32? -> Color?

        /// <summary>
        ///
        /// </summary>
        public float startDegree;

        /// <summary>
        ///
        /// </summary>
        public float endDegreee;

        static int[] SECTOR_CENTER_TRIANGLES = new int[] {
            0, 4, 1,
            0, 3, 4,
            0, 2, 3,
            0, 8, 5,
            0, 7, 8,
            0, 6, 7,
            6, 5, 2,
            2, 1, 6
        };

        public EllipseMesh()
        {
            lineColor = UColor.black;
            startDegree = 0;
            endDegreee = 360;
        }

        public void OnPopulateMesh(VertexBuffer vb)
        {
            Rect rect = drawRect != null ? (Rect)drawRect : vb.contentRect;
            Color color = fillColor != null ? (Color)fillColor : vb.vertexColor;

            float sectionStart = UMath.Clamp(startDegree, 0, 360);
            float sectionEnd = UMath.Clamp(endDegreee, 0, 360);
            bool clipped = sectionStart > 0 || sectionEnd < 360;
            sectionStart = sectionStart * UMath.Deg2Rad;
            sectionEnd = sectionEnd * UMath.Deg2Rad;
            Color centerColor2 = centerColor == null ? color : (Color)centerColor;

            float radiusX = rect.width / 2;
            float radiusY = rect.height / 2;
            int sides = UMath.CeilToInt(UMath.PI * (radiusX + radiusY) / 4);
            sides = UMath.Clamp(sides, 40, 800);
            float angleDelta = 2 * UMath.PI / sides;
            float angle = 0;
            float lineAngle = 0;

            if (lineWidth > 0 && clipped)
            {
                lineAngle = lineWidth / UMath.Max(radiusX, radiusY);
                sectionStart += lineAngle;
                sectionEnd -= lineAngle;
            }

            int vpos = vb.currentVertCount;
            float centerX = rect.x + radiusX;
            float centerY = rect.y + radiusY;
            vb.AddVert(new Vector3(centerX, centerY, 0), centerColor2);
            for (int i = 0; i < sides; i++)
            {
                if (angle < sectionStart)
                    angle = sectionStart;
                else if (angle > sectionEnd)
                    angle = sectionEnd;
                Vector3 vec = new Vector3(UMath.Cos(angle) * (radiusX - lineWidth) + centerX, UMath.Sin(angle) * (radiusY - lineWidth) + centerY, 0);
                vb.AddVert(vec, color);
                if (lineWidth > 0)
                {
                    vb.AddVert(vec, lineColor);
                    vb.AddVert(new Vector3(UMath.Cos(angle) * radiusX + centerX, UMath.Sin(angle) * radiusY + centerY, 0), lineColor);
                }
                angle += angleDelta;
            }

            if (lineWidth > 0)
            {
                int cnt = sides * 3;
                for (int i = 0; i < cnt; i += 3)
                {
                    if (i != cnt - 3)
                    {
                        vb.AddTriangle(0, i + 1, i + 4);
                        vb.AddTriangle(i + 5, i + 2, i + 3);
                        vb.AddTriangle(i + 3, i + 6, i + 5);
                    }
                    else if (!clipped)
                    {
                        vb.AddTriangle(0, i + 1, 1);
                        vb.AddTriangle(2, i + 2, i + 3);
                        vb.AddTriangle(i + 3, 3, 2);
                    }
                    else
                    {
                        vb.AddTriangle(0, i + 1, i + 1);
                        vb.AddTriangle(i + 2, i + 2, i + 3);
                        vb.AddTriangle(i + 3, i + 3, i + 2);
                    }
                }
            }
            else
            {
                for (int i = 0; i < sides; i++)
                {
                    if (i != sides - 1)
                        vb.AddTriangle(0, i + 1, i + 2);
                    else if (!clipped)
                        vb.AddTriangle(0, i + 1, 1);
                    else
                        vb.AddTriangle(0, i + 1, i + 1);
                }
            }

            if (lineWidth > 0 && clipped)
            {
                //扇形内边缘的线条

                vb.AddVert(new Vector3(radiusX, radiusY, 0), lineColor);
                float centerRadius = lineWidth * 0.5f;

                sectionStart -= lineAngle;
                angle = sectionStart + lineAngle * 0.5f + UMath.PI * 0.5f;
                vb.AddVert(new Vector3(UMath.Cos(angle) * centerRadius + radiusX, UMath.Sin(angle) * centerRadius + radiusY, 0), lineColor);
                angle -= UMath.PI;
                vb.AddVert(new Vector3(UMath.Cos(angle) * centerRadius + radiusX, UMath.Sin(angle) * centerRadius + radiusY, 0), lineColor);
                vb.AddVert(new Vector3(UMath.Cos(sectionStart) * radiusX + radiusX, UMath.Sin(sectionStart) * radiusY + radiusY, 0), lineColor);
                vb.AddVert(vb.GetPosition(vpos + 3), lineColor);

                sectionEnd += lineAngle;
                angle = sectionEnd - lineAngle * 0.5f + UMath.PI * 0.5f;
                vb.AddVert(new Vector3(UMath.Cos(angle) * centerRadius + radiusX, UMath.Sin(angle) * centerRadius + radiusY, 0), lineColor);
                angle -= UMath.PI;
                vb.AddVert(new Vector3(UMath.Cos(angle) * centerRadius + radiusX, UMath.Sin(angle) * centerRadius + radiusY, 0), lineColor);
                vb.AddVert(vb.GetPosition(vpos + sides * 3), lineColor);
                vb.AddVert(new Vector3(UMath.Cos(sectionEnd) * radiusX + radiusX, UMath.Sin(sectionEnd) * radiusY + radiusY, 0), lineColor);

                vb.AddTriangles(SECTOR_CENTER_TRIANGLES, sides * 3 + 1);
            }
        }

        public bool HitTest(Rect contentRect, Vector2 point)
        {
            if (!contentRect.Contains(point))
                return false;

            float radiusX = contentRect.width * 0.5f;
            float raduisY = contentRect.height * 0.5f;
            float xx = point.X - radiusX - contentRect.x;
            float yy = point.Y - raduisY - contentRect.y;
            if (UMath.Pow(xx / radiusX, 2) + UMath.Pow(yy / raduisY, 2) < 1)
            {
                if (startDegree != 0 || endDegreee != 360)
                {
                    float deg = UMath.Atan2(yy, xx) * UMath.Rad2Deg;
                    if (deg < 0)
                        deg += 360;
                    return deg >= startDegree && deg <= endDegreee;
                }
                else
                    return true;
            }

            return false;
        }
    }
}
