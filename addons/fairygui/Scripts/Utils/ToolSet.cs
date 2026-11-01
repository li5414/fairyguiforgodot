// Ported from FairyGUI-unity: Assets/Scripts/Utils/ToolSet.cs
using Godot;

namespace FairyGUI.Utils
{
    /// <summary>
    ///
    /// </summary>
    public static class ToolSet
    {
        public static Color ConvertFromHtmlColor(string str)
        {
            if (str.Length < 7 || str[0] != '#')
                return UColor.black;

            if (str.Length == 9)
            {
                //optimize:avoid using Convert.ToByte and Substring
                //return new Color32(Convert.ToByte(str.Substring(3, 2), 16), Convert.ToByte(str.Substring(5, 2), 16),
                //  Convert.ToByte(str.Substring(7, 2), 16), Convert.ToByte(str.Substring(1, 2), 16));

                return UColor.FromBytes((byte)(CharToHex(str[3]) * 16 + CharToHex(str[4])),
                    (byte)(CharToHex(str[5]) * 16 + CharToHex(str[6])),
                    (byte)(CharToHex(str[7]) * 16 + CharToHex(str[8])),
                    (byte)(CharToHex(str[1]) * 16 + CharToHex(str[2])));
            }
            else
            {
                //return new Color32(Convert.ToByte(str.Substring(1, 2), 16), Convert.ToByte(str.Substring(3, 2), 16),
                //Convert.ToByte(str.Substring(5, 2), 16), 255);

                return UColor.FromBytes((byte)(CharToHex(str[1]) * 16 + CharToHex(str[2])),
                    (byte)(CharToHex(str[3]) * 16 + CharToHex(str[4])),
                    (byte)(CharToHex(str[5]) * 16 + CharToHex(str[6])),
                    255);
            }
        }

        public static Color ColorFromRGB(int value)
        {
            return new Color(((value >> 16) & 0xFF) / 255f, ((value >> 8) & 0xFF) / 255f, (value & 0xFF) / 255f, 1);
        }

        public static Color ColorFromRGBA(uint value)
        {
            return new Color(((value >> 16) & 0xFF) / 255f, ((value >> 8) & 0xFF) / 255f, (value & 0xFF) / 255f, ((value >> 24) & 0xFF) / 255f);
        }

        public static int CharToHex(char c)
        {
            if (c >= '0' && c <= '9')
                return (int)c - 48;
            if (c >= 'A' && c <= 'F')
                return 10 + (int)c - 65;
            else if (c >= 'a' && c <= 'f')
                return 10 + (int)c - 97;
            else
                return 0;
        }

        public static Rect Intersection(ref Rect rect1, ref Rect rect2)
        {
            if (rect1.width == 0 || rect1.height == 0 || rect2.width == 0 || rect2.height == 0)
                return new Rect(0, 0, 0, 0);

            float left = rect1.xMin > rect2.xMin ? rect1.xMin : rect2.xMin;
            float right = rect1.xMax < rect2.xMax ? rect1.xMax : rect2.xMax;
            float top = rect1.yMin > rect2.yMin ? rect1.yMin : rect2.yMin;
            float bottom = rect1.yMax < rect2.yMax ? rect1.yMax : rect2.yMax;

            if (left > right || top > bottom)
                return new Rect(0, 0, 0, 0);
            else
                return Rect.MinMaxRect(left, top, right, bottom);
        }

        public static Rect Union(ref Rect rect1, ref Rect rect2)
        {
            if (rect2.width == 0 || rect2.height == 0)
                return rect1;

            if (rect1.width == 0 || rect1.height == 0)
                return rect2;

            float x = UMath.Min(rect1.x, rect2.x);
            float y = UMath.Min(rect1.y, rect2.y);
            return new Rect(x, y, UMath.Max(rect1.xMax, rect2.xMax) - x, UMath.Max(rect1.yMax, rect2.yMax) - y);
        }

        // [Godot] Unity: SkewMatrix(ref Matrix4x4 matrix, ...). Godot C# has no Matrix4x4, and the 2D display
        // transform is a Transform2D, so the same left-multiplication is applied to its basis:
        // m00/m10 -> X.X/X.Y (column 0), m01/m11 -> Y.X/Y.Y (column 1). Unity's m02/m12 are the z-axis column,
        // which Transform2D does not have; Origin (Unity m03/m13, translation) is left untouched, as in Unity.
        // [Godot] Unity negates both angles because its vertices are y-up (y flipped). Godot 2D is y-down like
        // FairyGUI, so the angles are used as-is; this gives the same on-screen skew as the Unity version.
        public static void SkewMatrix(ref Transform2D matrix, float skewX, float skewY)
        {
            skewX = skewX * UMath.Deg2Rad;
            skewY = skewY * UMath.Deg2Rad;
            float sinX = UMath.Sin(skewX);
            float cosX = UMath.Cos(skewX);
            float sinY = UMath.Sin(skewY);
            float cosY = UMath.Cos(skewY);

            float m00 = matrix.X.X * cosY - matrix.X.Y * sinX;
            float m10 = matrix.X.X * sinY + matrix.X.Y * cosX;
            float m01 = matrix.Y.X * cosY - matrix.Y.Y * sinX;
            float m11 = matrix.Y.X * sinY + matrix.Y.Y * cosX;

            matrix.X.X = m00;
            matrix.X.Y = m10;
            matrix.Y.X = m01;
            matrix.Y.Y = m11;
        }

        public static void RotateUV(Vector2[] uv, ref Rect baseUVRect)
        {
            int vertCount = uv.Length;
            float xMin = UMath.Min(baseUVRect.xMin, baseUVRect.xMax);
            float yMin = baseUVRect.yMin;
            float yMax = baseUVRect.yMax;
            if (yMin > yMax)
            {
                yMin = yMax;
                yMax = baseUVRect.yMin;
            }

            float tmp;
            for (int i = 0; i < vertCount; i++)
            {
                Vector2 m = uv[i];
                tmp = m.Y;
                m.Y = yMin + m.X - xMin;
                m.X = xMin + yMax - tmp;
                uv[i] = m;
            }
        }
    }
}
