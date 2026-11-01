// Ported from FairyGUI-unity: Assets/Scripts/Core/Mesh/LineMesh.cs
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// Inspired by kim ki won (http://mypi.ruliweb.daum.net/mypi.htm?id=newtypekorea)
    /// </summary>
    public class LineMesh : IMeshFactory
    {
        /// <summary>
        ///
        /// </summary>
        public GPath path;

        /// <summary>
        ///
        /// </summary>
        public float lineWidth;

        // [Godot] UnityEngine.AnimationCurve -> Godot.Curve, evaluated with Curve.Sample(t), t in [0,1].
        // Godot clamps point values to [MinValue, MaxValue] (default 0..1), so set MaxValue to at least
        // the largest line width before adding points.
        /// <summary>
        ///
        /// </summary>
        public Curve lineWidthCurve;

        // [Godot] UnityEngine.Gradient -> Godot.Gradient, evaluated with Gradient.Sample(t).
        /// <summary>
        ///
        /// </summary>
        public Gradient gradient;

        /// <summary>
        ///
        /// </summary>
        public bool roundEdge;

        /// <summary>
        ///
        /// </summary>
        public float fillStart;

        /// <summary>
        ///
        /// </summary>
        public float fillEnd;

        /// <summary>
        ///
        /// </summary>
        public float pointDensity;

        /// <summary>
        ///
        /// </summary>
        public bool repeatFill;

        static List<Vector3> points = new List<Vector3>();
        static List<float> ts = new List<float>();

        public LineMesh()
        {
            path = new GPath();
            lineWidth = 2;
            fillStart = 0;
            fillEnd = 1;
            pointDensity = 0.1f;
        }

        public void OnPopulateMesh(VertexBuffer vb)
        {
            Vector2 uvMin = vb.uvRect.position;
            Vector2 uvMax = new Vector2(vb.uvRect.xMax, vb.uvRect.yMax);
            float uvRatio = path.length / vb.textureSize.X;

            int segCount = path.segmentCount;
            float t = 0;
            float lw = lineWidth;
            float u;
            for (int si = 0; si < segCount; si++)
            {
                float ratio = path.GetSegmentLength(si) / path.length;
                float t0 = UMath.Clamp(fillStart - t, 0, ratio) / ratio;
                float t1 = UMath.Clamp(fillEnd - t, 0, ratio) / ratio;
                if (t0 >= t1)
                {
                    t += ratio;
                    continue;
                }

                points.Clear();
                ts.Clear();
                path.GetPointsInSegment(si, t0, t1, points, ts, pointDensity);
                int cnt = points.Count;

                Color c0 = vb.vertexColor;
                Color c1 = vb.vertexColor;
                if (gradient != null)
                    c0 = gradient.Sample(t); // [Godot] Unity: gradient.Evaluate(t)
                if (lineWidthCurve != null)
                    lw = lineWidthCurve.Sample(t); // [Godot] Unity: lineWidthCurve.Evaluate(t)

                if (roundEdge && si == 0 && t0 == 0)
                    DrawRoundEdge(vb, points[0].ToV2(), points[1].ToV2(), lw, c0, uvMin);

                int vertCount = vb.currentVertCount;
                for (int i = 1; i < cnt; i++)
                {
                    Vector3 p0 = points[i - 1];
                    Vector3 p1 = points[i];
                    int k = vertCount + (i - 1) * 2;
                    float tc = t + ratio * ts[i];

                    Vector3 lineVector = p1 - p0;
                    Vector3 widthVector = lineVector.Cross(new Vector3(0, 0, 1));
                    widthVector = widthVector.Normalized();

                    if (i == 1)
                    {
                        if (repeatFill)
                            u = tc * uvRatio * uvMax.X;
                        else
                            u = UMath.Lerp(uvMin.X, uvMax.X, t + ratio * ts[i - 1]);
                        vb.AddVert(p0 - widthVector * lw * 0.5f, c0, new Vector2(u, uvMax.Y));
                        vb.AddVert(p0 + widthVector * lw * 0.5f, c0, new Vector2(u, uvMin.Y));

                        if (si != 0) //joint
                        {
                            vb.AddTriangle(k - 2, k - 1, k + 1);
                            vb.AddTriangle(k - 2, k + 1, k);
                        }
                    }
                    if (gradient != null)
                        c1 = gradient.Sample(tc); // [Godot] Unity: gradient.Evaluate(tc)

                    if (lineWidthCurve != null)
                        lw = lineWidthCurve.Sample(tc); // [Godot] Unity: lineWidthCurve.Evaluate(tc)

                    if (repeatFill)
                        u = tc * uvRatio * uvMax.X;
                    else
                        u = UMath.Lerp(uvMin.X, uvMax.X, tc);
                    vb.AddVert(p1 - widthVector * lw * 0.5f, c1, new Vector2(u, uvMax.Y));
                    vb.AddVert(p1 + widthVector * lw * 0.5f, c1, new Vector2(u, uvMin.Y));

                    vb.AddTriangle(k, k + 1, k + 3);
                    vb.AddTriangle(k, k + 3, k + 2);
                }

                if (roundEdge && si == segCount - 1 && t1 == 1)
                    DrawRoundEdge(vb, points[cnt - 1].ToV2(), points[cnt - 2].ToV2(), lw, c1, uvMax);

                t += ratio;
            }
        }

        void DrawRoundEdge(VertexBuffer vb, Vector2 p0, Vector2 p1, float lw, Color color, Vector2 uv)
        {
            Vector2 widthVector = (p0 - p1).ToV3().Cross(new Vector3(0, 0, 1)).ToV2();
            widthVector = widthVector.Normalized();
            widthVector = widthVector * lw / 2f;
            Vector2 lineVector = (p0 - p1).Normalized() * lw / 2f;

            int sides = UMath.CeilToInt(UMath.PI * lw / 2);
            if (sides < 6)
                sides = 6;
            int current = vb.currentVertCount;
            float angleUnit = UMath.PI / (sides - 1);

            vb.AddVert(p0.ToV3(), color, uv);
            vb.AddVert((p0 + widthVector).ToV3(), color, uv);

            for (int n = 0; n < sides; n++)
            {
                vb.AddVert((p0 + UMath.Cos(angleUnit * n) * widthVector + UMath.Sin(angleUnit * n) * lineVector).ToV3(), color, uv);
                vb.AddTriangle(current, current + 1 + n, current + 2 + n);
            }
        }
    }
}
