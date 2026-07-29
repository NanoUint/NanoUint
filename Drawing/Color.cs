namespace NanoUint.Drawing;

/// <summary>
/// 纯 C# 颜色结构，无 WPF 依赖。
/// </summary>
public struct Color : IEquatable<Color>
{
    public byte R, G, B, A;

    public Color(byte r, byte g, byte b, byte a = 255)
    {
        R = r; G = g; B = b; A = a;
    }

    public static Color White => new(255, 255, 255);
    public static Color Black => new(0, 0, 0);
    public static Color Transparent => new(0, 0, 0, 0);
    public static Color Red => new(255, 0, 0);
    public static Color Green => new(0, 255, 0);
    public static Color Blue => new(0, 0, 255);
    public static Color Yellow => new(255, 255, 0);
    public static Color Cyan => new(0, 255, 255);
    public static Color Magenta => new(255, 0, 255);
    public static Color Gray => new(128, 128, 128);

    public static Color FromRgb(byte r, byte g, byte b) => new(r, g, b);
    public static Color FromRgba(byte r, byte g, byte b, byte a) => new(r, g, b, a);

    public bool Equals(Color other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object? obj) => obj is Color other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    public static bool operator ==(Color left, Color right) => left.Equals(right);
    public static bool operator !=(Color left, Color right) => !left.Equals(right);

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}{(A != 255 ? A.ToString("X2") : "")}";
}
