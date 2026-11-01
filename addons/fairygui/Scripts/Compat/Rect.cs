// FairyGUI for Godot - compatibility layer (not ported; written for the Godot port).
// A rectangle with UnityEngine.Rect semantics, so ported FairyGUI code that relies on
// x/y/width/height/xMin/xMax/yMin/yMax keeps its exact behaviour.
// Implicitly converts to and from Godot.Rect2 for public-API interop.
using System;
using Godot;

namespace FairyGUI
{
    public struct Rect : IEquatable<Rect>
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public Rect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        public Rect(Vector2 position, Vector2 size)
        {
            x = position.X;
            y = position.Y;
            width = size.X;
            height = size.Y;
        }

        public Rect(Rect source)
        {
            x = source.x;
            y = source.y;
            width = source.width;
            height = source.height;
        }

        public static Rect zero => new Rect(0, 0, 0, 0);

        public static Rect MinMaxRect(float xmin, float ymin, float xmax, float ymax)
        {
            return new Rect(xmin, ymin, xmax - xmin, ymax - ymin);
        }

        public void Set(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        public Vector2 position
        {
            get => new Vector2(x, y);
            set { x = value.X; y = value.Y; }
        }

        public Vector2 size
        {
            get => new Vector2(width, height);
            set { width = value.X; height = value.Y; }
        }

        public Vector2 center
        {
            get => new Vector2(x + width / 2f, y + height / 2f);
            set { x = value.X - width / 2f; y = value.Y - height / 2f; }
        }

        public Vector2 min
        {
            get => new Vector2(xMin, yMin);
            set { xMin = value.X; yMin = value.Y; }
        }

        public Vector2 max
        {
            get => new Vector2(xMax, yMax);
            set { xMax = value.X; yMax = value.Y; }
        }

        /// <summary>Setting xMin keeps xMax fixed (Unity semantics).</summary>
        public float xMin
        {
            get => Math.Min(x, x + width);
            set { float oldxmax = xMax; x = value; width = oldxmax - x; }
        }

        /// <summary>Setting yMin keeps yMax fixed (Unity semantics).</summary>
        public float yMin
        {
            get => Math.Min(y, y + height);
            set { float oldymax = yMax; y = value; height = oldymax - y; }
        }

        public float xMax
        {
            get => Math.Max(x, x + width);
            set { width = value - x; }
        }

        public float yMax
        {
            get => Math.Max(y, y + height);
            set { height = value - y; }
        }

        /// <summary>Inclusive min, exclusive max (Unity semantics).</summary>
        public bool Contains(Vector2 point)
        {
            return point.X >= xMin && point.X < xMax && point.Y >= yMin && point.Y < yMax;
        }

        public bool Contains(Vector3 point)
        {
            return point.X >= xMin && point.X < xMax && point.Y >= yMin && point.Y < yMax;
        }

        public bool Overlaps(Rect other)
        {
            return other.xMax > xMin && other.xMin < xMax && other.yMax > yMin && other.yMin < yMax;
        }

        public static implicit operator Rect2(Rect r) => new Rect2(r.x, r.y, r.width, r.height);
        public static implicit operator Rect(Rect2 r) => new Rect(r.Position.X, r.Position.Y, r.Size.X, r.Size.Y);

        public static bool operator ==(Rect a, Rect b) => a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height;
        public static bool operator !=(Rect a, Rect b) => !(a == b);

        public bool Equals(Rect other) => this == other;
        public override bool Equals(object obj) => obj is Rect r && this == r;
        public override int GetHashCode() => HashCode.Combine(x, y, width, height);
        public override string ToString() => $"(x:{x:F2}, y:{y:F2}, width:{width:F2}, height:{height:F2})";
    }
}
