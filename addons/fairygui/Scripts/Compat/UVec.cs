// FairyGUI for Godot - compatibility layer (not ported; written for the Godot port).
// Unity-semantics vector/color statics and helpers. Godot's equivalents differ in
// several traps that silently change behaviour:
//   Godot Vector2.Up = (0,-1)       Unity Vector2.up = (0,1)
//   Godot Vector3.Back = (0,0,1)    Unity Vector3.back = (0,0,-1)  (forward likewise flipped)
//   Godot Vector2/3.Lerp unclamped  Unity Vector2/3.Lerp clamps t
//   Godot Colors.Transparent = (1,1,1,0)   Unity Color.clear = (0,0,0,0)
//   Godot Colors.Gray = (0.75,..)   Unity Color.gray = (0.5,0.5,0.5,1)
// Unity has implicit Vector2<->Vector3 conversion; Godot does not: use ToV2()/ToV3().
using Godot;

namespace FairyGUI
{
    public static class UVec
    {
        public static readonly Vector3 forward = new Vector3(0, 0, 1);
        public static readonly Vector3 back = new Vector3(0, 0, -1);
        public static readonly Vector2 up2 = new Vector2(0, 1);
        public static readonly Vector2 down2 = new Vector2(0, -1);
        public static readonly Vector3 up3 = new Vector3(0, 1, 0);
        public static readonly Vector3 down3 = new Vector3(0, -1, 0);

        /// <summary>Unity Vector2.Lerp: t clamped to [0,1].</summary>
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
        {
            t = UMath.Clamp01(t);
            return new Vector2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        /// <summary>Unity Vector3.Lerp: t clamped to [0,1].</summary>
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            t = UMath.Clamp01(t);
            return new Vector3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        }

        public static float Distance(Vector2 a, Vector2 b) => a.DistanceTo(b);
        public static float Distance(Vector3 a, Vector3 b) => a.DistanceTo(b);
        public static float Dot(Vector3 a, Vector3 b) => a.Dot(b);
        public static Vector3 Cross(Vector3 a, Vector3 b) => a.Cross(b);

        public static Vector2 ToV2(this Vector3 v) => new Vector2(v.X, v.Y);
        public static Vector3 ToV3(this Vector2 v) => new Vector3(v.X, v.Y, 0);
        public static Vector3 ToV3(this Vector2 v, float z) => new Vector3(v.X, v.Y, z);
    }

    public static class UColor
    {
        public static readonly Color white = new Color(1, 1, 1, 1);
        public static readonly Color black = new Color(0, 0, 0, 1);
        public static readonly Color clear = new Color(0, 0, 0, 0);
        public static readonly Color red = new Color(1, 0, 0, 1);
        public static readonly Color green = new Color(0, 1, 0, 1);
        public static readonly Color blue = new Color(0, 0, 1, 1);
        public static readonly Color yellow = new Color(1, 0.92156863f, 0.015686275f, 1);
        public static readonly Color gray = new Color(0.5f, 0.5f, 0.5f, 1);

        /// <summary>Replacement for `new Color32(r,g,b,a)`.</summary>
        public static Color FromBytes(byte r, byte g, byte b, byte a) => Color.Color8(r, g, b, a);

        /// <summary>Unity Color.Lerp: t clamped to [0,1].</summary>
        public static Color Lerp(Color a, Color b, float t)
        {
            t = UMath.Clamp01(t);
            return new Color(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }
    }
}
