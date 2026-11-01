// FairyGUI for Godot - compatibility layer (not ported; written for the Godot port).
// UMath mirrors UnityEngine.Mathf with Unity's exact semantics.
// Porting rule: every `Mathf.Xxx` in the Unity source becomes `UMath.Xxx`.
// Do NOT use Godot.Mathf in ported code: Lerp (unclamped), Sign(0)=0, Epsilon etc. differ.
using System;

namespace FairyGUI
{
    public static class UMath
    {
        public const float PI = (float)Math.PI;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Deg2Rad = PI * 2f / 360f;
        public const float Rad2Deg = 1f / Deg2Rad;
        public static readonly float Epsilon = float.Epsilon;

        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Tan(float f) => (float)Math.Tan(f);
        public static float Asin(float f) => (float)Math.Asin(f);
        public static float Acos(float f) => (float)Math.Acos(f);
        public static float Atan(float f) => (float)Math.Atan(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int value) => Math.Abs(value);
        public static float Pow(float f, float p) => (float)Math.Pow(f, p);
        public static float Exp(float power) => (float)Math.Exp(power);
        public static float Log(float f) => (float)Math.Log(f);
        public static float Log(float f, float p) => (float)Math.Log(f, p);
        public static float Log10(float f) => (float)Math.Log10(f);

        public static float Ceil(float f) => (float)Math.Ceiling(f);
        public static float Floor(float f) => (float)Math.Floor(f);
        /// <summary>Banker's rounding, same as Unity.</summary>
        public static float Round(float f) => (float)Math.Round(f);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        /// <summary>Banker's rounding, same as Unity.</summary>
        public static int RoundToInt(float f) => (int)Math.Round(f);

        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Min(params float[] values)
        {
            int len = values.Length;
            if (len == 0) return 0;
            float m = values[0];
            for (int i = 1; i < len; i++) if (values[i] < m) m = values[i];
            return m;
        }
        public static int Min(params int[] values)
        {
            int len = values.Length;
            if (len == 0) return 0;
            int m = values[0];
            for (int i = 1; i < len; i++) if (values[i] < m) m = values[i];
            return m;
        }

        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Max(params float[] values)
        {
            int len = values.Length;
            if (len == 0) return 0;
            float m = values[0];
            for (int i = 1; i < len; i++) if (values[i] > m) m = values[i];
            return m;
        }
        public static int Max(params int[] values)
        {
            int len = values.Length;
            if (len == 0) return 0;
            int m = values[0];
            for (int i = 1; i < len; i++) if (values[i] > m) m = values[i];
            return m;
        }

        /// <summary>Unity semantics: returns 1 for 0.</summary>
        public static float Sign(float f) => f >= 0f ? 1f : -1f;

        public static float Clamp(float value, float min, float max)
        {
            if (value < min) value = min;
            else if (value > max) value = max;
            return value;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) value = min;
            else if (value > max) value = max;
            return value;
        }

        public static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        /// <summary>Unity semantics: t is clamped to [0,1].</summary>
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;

        public static float InverseLerp(float a, float b, float value)
        {
            if (a != b) return Clamp01((value - a) / (b - a));
            return 0f;
        }

        public static bool Approximately(float a, float b)
        {
            return Math.Abs(b - a) < Math.Max(1E-06f * Math.Max(Math.Abs(a), Math.Abs(b)), float.Epsilon * 8f);
        }

        public static float Repeat(float t, float length)
        {
            return Clamp(t - Floor(t / length) * length, 0f, length);
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Sign(target - current) * maxDelta;
        }
    }
}
