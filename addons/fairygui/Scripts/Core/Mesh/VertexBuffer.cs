// Ported from FairyGUI-unity: Assets/Scripts/Core/Mesh/VertexBuffer.cs
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    // [Godot] Internal conventions are kept exactly as in the Unity version: vertices are stored with y negated,
    // uv origin is bottom-left, and the uvRect lerp formulas are unchanged. NGraphics is the only place that
    // converts (x, y) -> (x, -y) and (u, v) -> (u, 1 - v) when submitting to the CanvasItem.
    // [Godot] Color32 -> Color everywhere. Unity quantized vertex colors to bytes when they entered the buffer;
    // Godot keeps float precision. Byte-alpha tests (Color32.a != 255) use Color.A8, which rounds the same way.
    /// <summary>
    ///
    /// </summary>
    public sealed class VertexBuffer
    {
        /// <summary>
        ///
        /// </summary>
        public Rect contentRect;

        /// <summary>
        ///
        /// </summary>
        public Rect uvRect;

        /// <summary>
        ///
        /// </summary>
        public Color vertexColor; // [Godot] Color32 -> Color

        /// <summary>
        ///
        /// </summary>
        public Vector2 textureSize;

        /// <summary>
        ///
        /// </summary>
        public readonly List<Vector3> vertices;

        /// <summary>
        ///
        /// </summary>
        public readonly List<Color> colors; // [Godot] List<Color32> -> List<Color>

        /// <summary>
        ///
        /// </summary>
        public readonly List<Vector2> uvs;

        /// <summary>
        ///
        /// </summary>
        public readonly List<Vector2> uvs2;

        /// <summary>
        ///
        /// </summary>
        public readonly List<int> triangles;

        static public Vector2[] NormalizedUV = new Vector2[] {
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };

        static public Vector2[] NormalizedPosition = new Vector2[] {
            new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1) };

        internal bool _alphaInVertexColor;
        internal bool _isArbitraryQuad;

        static Stack<VertexBuffer> _pool = new Stack<VertexBuffer>();

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public static VertexBuffer Begin()
        {
            if (_pool.Count > 0)
            {
                VertexBuffer inst = _pool.Pop();
                inst.Clear();
                return inst;
            }
            else
                return new VertexBuffer();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        public static VertexBuffer Begin(VertexBuffer source)
        {
            VertexBuffer vb = Begin();
            vb.contentRect = source.contentRect;
            vb.uvRect = source.uvRect;
            vb.vertexColor = source.vertexColor;
            vb.textureSize = source.textureSize;

            return vb;
        }

        private VertexBuffer()
        {
            vertices = new List<Vector3>();
            colors = new List<Color>();
            uvs = new List<Vector2>();
            uvs2 = new List<Vector2>();
            triangles = new List<int>();
        }

        /// <summary>
        ///
        /// </summary>
        public void End()
        {
            _pool.Push(this);
        }

        /// <summary>
        ///
        /// </summary>
        public void Clear()
        {
            vertices.Clear();
            colors.Clear();
            uvs.Clear();
            uvs2.Clear();
            triangles.Clear();

            _isArbitraryQuad = false;
            _alphaInVertexColor = false;
        }

        /// <summary>
        ///
        /// </summary>
        public int currentVertCount
        {
            get
            {
                return vertices.Count;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="position"></param>
        public void AddVert(Vector3 position)
        {
            position.Y = -position.Y;
            vertices.Add(position);
            colors.Add(vertexColor);
            if (vertexColor.A8 != 255)
                _alphaInVertexColor = true;
            uvs.Add(new Vector2(
                    UMath.Lerp(uvRect.xMin, uvRect.xMax, (position.X - contentRect.xMin) / contentRect.width),
                    UMath.Lerp(uvRect.yMax, uvRect.yMin, (-position.Y - contentRect.yMin) / contentRect.height)));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="position"></param>
        /// <param name="color"></param>
        public void AddVert(Vector3 position, Color color)
        {
            position.Y = -position.Y;
            vertices.Add(position);
            colors.Add(color);
            if (color.A8 != 255)
                _alphaInVertexColor = true;
            uvs.Add(new Vector2(
                    UMath.Lerp(uvRect.xMin, uvRect.xMax, (position.X - contentRect.xMin) / contentRect.width),
                    UMath.Lerp(uvRect.yMax, uvRect.yMin, (-position.Y - contentRect.yMin) / contentRect.height)));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="position"></param>
        /// <param name="color"></param>
        /// <param name="uv"></param>
        public void AddVert(Vector3 position, Color color, Vector2 uv)
        {
            position.Y = -position.Y;
            vertices.Add(position);
            uvs.Add(new Vector2(uv.X, uv.Y));
            colors.Add(color);
            if (color.A8 != 255)
                _alphaInVertexColor = true;
        }

        /// <summary>
        ///
        /// 1---2
        /// | / |
        /// 0---3
        /// </summary>
        /// <param name="vertRect"></param>
        public void AddQuad(Rect vertRect)
        {
            AddVert(new Vector3(vertRect.xMin, vertRect.yMax, 0f));
            AddVert(new Vector3(vertRect.xMin, vertRect.yMin, 0f));
            AddVert(new Vector3(vertRect.xMax, vertRect.yMin, 0f));
            AddVert(new Vector3(vertRect.xMax, vertRect.yMax, 0f));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="vertRect"></param>
        /// <param name="color"></param>
        public void AddQuad(Rect vertRect, Color color)
        {
            AddVert(new Vector3(vertRect.xMin, vertRect.yMax, 0f), color);
            AddVert(new Vector3(vertRect.xMin, vertRect.yMin, 0f), color);
            AddVert(new Vector3(vertRect.xMax, vertRect.yMin, 0f), color);
            AddVert(new Vector3(vertRect.xMax, vertRect.yMax, 0f), color);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="vertRect"></param>
        /// <param name="color"></param>
        /// <param name="uvRect"></param>
        public void AddQuad(Rect vertRect, Color color, Rect uvRect)
        {
            vertices.Add(new Vector3(vertRect.xMin, -vertRect.yMax, 0f));
            vertices.Add(new Vector3(vertRect.xMin, -vertRect.yMin, 0f));
            vertices.Add(new Vector3(vertRect.xMax, -vertRect.yMin, 0f));
            vertices.Add(new Vector3(vertRect.xMax, -vertRect.yMax, 0f));

            uvs.Add(new Vector2(uvRect.xMin, uvRect.yMin));
            uvs.Add(new Vector2(uvRect.xMin, uvRect.yMax));
            uvs.Add(new Vector2(uvRect.xMax, uvRect.yMax));
            uvs.Add(new Vector2(uvRect.xMax, uvRect.yMin));

            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);

            if (color.A8 != 255)
                _alphaInVertexColor = true;
        }

        static List<Vector4> helperV4List = new List<Vector4>(4) { Vector4.Zero, Vector4.Zero, Vector4.Zero, Vector4.Zero };
        // [Godot] Godot's CanvasItemAddTriangleArray only accepts Vector2 uvs, so the projective (u*q, v*q, 0, q)
        // coordinates returned here can only be used through a custom shader. The math is unchanged.
        internal List<Vector4> FixUVForArbitraryQuad()
        {
            //ref1 http://www.reedbeta.com/blog/quadrilateral-interpolation-part-1/
            //ref2 https://bitlush.com/blog/arbitrary-quadrilaterals-in-opengl-es-2-0

            Vector4 qq = Vector4.One;
            Vector2 a = (vertices[2] - vertices[0]).ToV2();
            Vector2 b = (vertices[1] - vertices[3]).ToV2();
            Vector2 c = (vertices[0] - vertices[3]).ToV2();

            float cross = a.X * b.Y - a.Y * b.X;
            if (cross != 0)
            {
                float s = (a.X * c.Y - a.Y * c.X) / cross;
                if (s > 0 && s < 1)
                {
                    float t = (b.X * c.Y - b.Y * c.X) / cross;
                    if (t > 0 && t < 1)
                    {
                        qq.X = 1 / (1 - t);
                        qq.Y = 1 / s;
                        qq.Z = 1 / t;
                        qq.W = 1 / (1 - s);
                    }
                }
            }

            for (int i = 0; i < 4; i++)
            {
                Vector4 v = new Vector4(uvs[i].X, uvs[i].Y, 0, 0);
                float q = qq[i];
                v.X *= q;
                v.Y *= q;
                v.W = q;
                helperV4List[i] = v;
            }

            return helperV4List;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <param name="count"></param>
        public void RepeatColors(Color[] value, int startIndex, int count)
        {
            int len = UMath.Min(startIndex + count, vertices.Count);
            int colorCount = value.Length;
            int k = 0;
            for (int i = startIndex; i < len; i++)
            {
                Color c = value[(k++) % colorCount];
                colors[i] = c;
                if (c.A8 != 255)
                    _alphaInVertexColor = true;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="idx0"></param>
        /// <param name="idx1"></param>
        /// <param name="idx2"></param>
        public void AddTriangle(int idx0, int idx1, int idx2)
        {
            triangles.Add(idx0);
            triangles.Add(idx1);
            triangles.Add(idx2);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="idxList"></param>
        /// <param name="startVertexIndex"></param>
        public void AddTriangles(int[] idxList, int startVertexIndex = 0)
        {
            if (startVertexIndex != 0)
            {
                if (startVertexIndex < 0)
                    startVertexIndex = vertices.Count + startVertexIndex;

                int cnt = idxList.Length;
                for (int i = 0; i < cnt; i++)
                    triangles.Add(idxList[i] + startVertexIndex);
            }
            else
                triangles.AddRange(idxList);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="startVertexIndex"></param>
        public void AddTriangles(int startVertexIndex = 0)
        {
            int cnt = vertices.Count;
            if (startVertexIndex < 0)
                startVertexIndex = cnt + startVertexIndex;

            for (int i = startVertexIndex; i < cnt; i += 4)
            {
                triangles.Add(i);
                triangles.Add(i + 1);
                triangles.Add(i + 2);

                triangles.Add(i + 2);
                triangles.Add(i + 3);
                triangles.Add(i);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Vector3 GetPosition(int index)
        {
            if (index < 0)
                index = vertices.Count + index;

            Vector3 vec = vertices[index];
            vec.Y = -vec.Y;
            return vec;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="position"></param>
        /// <param name="usePercent"></param>
        /// <returns></returns>
        public Vector2 GetUVAtPosition(Vector2 position, bool usePercent)
        {
            if (usePercent)
            {
                return new Vector2(UMath.Lerp(uvRect.xMin, uvRect.xMax, position.X),
                    UMath.Lerp(uvRect.yMax, uvRect.yMin, position.Y));
            }
            else
                return new Vector2(UMath.Lerp(uvRect.xMin, uvRect.xMax, (position.X - contentRect.xMin) / contentRect.width),
                    UMath.Lerp(uvRect.yMax, uvRect.yMin, (position.Y - contentRect.yMin) / contentRect.height));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="vb"></param>
        public void Append(VertexBuffer vb)
        {
            int len = vertices.Count;
            vertices.AddRange(vb.vertices);
            uvs.AddRange(vb.uvs);
            uvs2.AddRange(vb.uvs2);
            colors.AddRange(vb.colors);
            if (len != 0)
            {
                int len1 = vb.triangles.Count;
                for (int i = 0; i < len1; i++)
                    triangles.Add(vb.triangles[i] + len);
            }
            else
                triangles.AddRange(vb.triangles);

            if (vb._alphaInVertexColor)
                _alphaInVertexColor = true;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="vb"></param>
        public void Insert(VertexBuffer vb)
        {
            vertices.InsertRange(0, vb.vertices);
            uvs.InsertRange(0, vb.uvs);
            uvs2.InsertRange(0, vb.uvs2);
            colors.InsertRange(0, vb.colors);
            int len = triangles.Count;
            if (len != 0)
            {
                int len1 = vb.vertices.Count;
                for (int i = 0; i < len; i++)
                    triangles[i] += len1;
            }
            triangles.InsertRange(0, vb.triangles);

            if (vb._alphaInVertexColor)
                _alphaInVertexColor = true;
        }

        static float[] STROKE_OFFSET = new float[]
        {
             -1, 0, 1, 0,
            0, -1, 0, 1,
            -1, -1, 1, -1,
            -1, 1, 1, 1
        };
        public void GenerateOutline(int dirs, float width, Color color)
        {
            int count = vertices.Count;
            if (count + dirs * count > VerticesLimit)
            {
                GD.PushWarning("Outline effect cannot be completed as mesh is too large.");
                return;
            }

            VertexBuffer vb2 = VertexBuffer.Begin();
            // Same test as Unity: color is a float Color here, so comparing A with 255 is effectively always true.
            if (color.A != 255)
                vb2._alphaInVertexColor = true;
            for (int j = 0; j < dirs; j++)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3 vert = vertices[i];
                    vb2.vertices.Add(new Vector3(vert.X + STROKE_OFFSET[j * 2] * width, vert.Y + STROKE_OFFSET[j * 2 + 1] * width, 0));
                    vb2.colors.Add(color);
                }

                vb2.uvs.AddRange(uvs);
                if (uvs2.Count > 0)
                    vb2.uvs2.AddRange(uvs2);
            }
            Insert(vb2);
            vb2.End();
        }

        public void GenerateShadow(Vector2 offset, Color color)
        {
            int count = vertices.Count;
            if (count + count > VerticesLimit)
            {
                GD.PushWarning("Shadow effect cannot be completed as mesh is too large.");
                return;
            }

            VertexBuffer vb2 = VertexBuffer.Begin();
            // Same test as Unity: color is a float Color here, so comparing A with 255 is effectively always true.
            if (color.A != 255)
                vb2._alphaInVertexColor = true;
            for (int i = 0; i < count; i++)
            {
                Vector3 vert = vertices[i];
                vb2.vertices.Add(new Vector3(vert.X + offset.X, vert.Y - offset.Y, 0));
                vb2.colors.Add(color);
            }

            vb2.uvs.AddRange(uvs);
            if (uvs2.Count > 0)
                vb2.uvs2.AddRange(uvs2);
            Insert(vb2);
            vb2.End();
        }

        // [Godot] Unity's limit comes from 16-bit mesh indices. Godot canvas polygons use 32-bit indices, but the
        // limit is kept so text meshes behave the same as in the Unity version.
        const int VerticesLimit = 65000;
        public void CheckMeshLimit()
        {
            int count = vertices.Count;
            if (count > VerticesLimit)
            {
                GD.PushWarning("A mesh may not have more than " + VerticesLimit + " vertices.");
                vertices.RemoveRange(VerticesLimit, count - VerticesLimit);
                colors.RemoveRange(VerticesLimit, count - VerticesLimit);
                uvs.RemoveRange(VerticesLimit, count - VerticesLimit);
                if (uvs2.Count > 0)
                    uvs2.RemoveRange(VerticesLimit, count - VerticesLimit);
                count = VerticesLimit;

                // [Godot] Unity left triangles that reference removed vertices, and Mesh.SetTriangles then rejected
                // the whole index list. Godot does not validate indices on CanvasItemAddTriangleArray, so drop
                // those triangles here to avoid out-of-range reads.
                int triCount = triangles.Count - triangles.Count % 3;
                int w = 0;
                for (int i = 0; i < triCount; i += 3)
                {
                    int i0 = triangles[i], i1 = triangles[i + 1], i2 = triangles[i + 2];
                    if (i0 < count && i1 < count && i2 < count)
                    {
                        triangles[w++] = i0;
                        triangles[w++] = i1;
                        triangles[w++] = i2;
                    }
                }
                triangles.RemoveRange(w, triangles.Count - w);
            }
        }
    }
}
