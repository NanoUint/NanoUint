using System.Globalization;

namespace NanoUint.Drawing;

/// <summary>3D vector. Pure math type independent of WPF.</summary>
public readonly struct Vector3 : IEquatable<Vector3>
{
    public float X { get; }
    public float Y { get; }
    public float Z { get; }

    public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }
    public Vector3(float x, float y) : this(x, y, 0f) { }

    public static readonly Vector3 Zero = new(0f, 0f, 0f);
    public static readonly Vector3 One = new(1f, 1f, 1f);
    public static readonly Vector3 Forward = new(0f, 0f, 1f);
    public static readonly Vector3 Back = new(0f, 0f, -1f);
    public static readonly Vector3 Up = new(0f, -1f, 0f);
    public static readonly Vector3 Down = new(0f, 1f, 0f);
    public static readonly Vector3 Left = new(-1f, 0f, 0f);
    public static readonly Vector3 Right = new(1f, 0f, 0f);

    public float LengthSquared => X * X + Y * Y + Z * Z;
    public float Length => MathF.Sqrt(LengthSquared);

    public Vector3 Normalized
    {
        get
        {
            var len = Length;
            return len > 0.0001f ? new Vector3(X / len, Y / len, Z / len) : Zero;
        }
    }

    public Vector2 XY => new(X, Y);
    public Vector2 XZ => new(X, Z);
    public Vector2 YZ => new(Y, Z);

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator -(Vector3 v) => new(-v.X, -v.Y, -v.Z);
    public static Vector3 operator *(Vector3 v, float s) => new(v.X * s, v.Y * s, v.Z * s);
    public static Vector3 operator *(float s, Vector3 v) => new(v.X * s, v.Y * s, v.Z * s);
    public static Vector3 operator /(Vector3 v, float s) => new(v.X / s, v.Y / s, v.Z / s);

    public static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Vector3 Cross(Vector3 a, Vector3 b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);

    public static float Distance(Vector3 a, Vector3 b) => (a - b).Length;

    public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Vector3(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t);
    }

    public void Deconstruct(out float x, out float y, out float z) { x = X; y = Y; z = Z; }

    public bool Equals(Vector3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
    public override bool Equals(object? obj) => obj is Vector3 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public static bool operator ==(Vector3 a, Vector3 b) => a.Equals(b);
    public static bool operator !=(Vector3 a, Vector3 b) => !a.Equals(b);
    public override string ToString() =>
        $"({X.ToString("F3", CultureInfo.InvariantCulture)}, {Y.ToString("F3", CultureInfo.InvariantCulture)}, {Z.ToString("F3", CultureInfo.InvariantCulture)})";
}
