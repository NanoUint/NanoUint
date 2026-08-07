namespace NanoUint.Drawing;

/// <summary>纯 C# 矩形结构，无 WPF 依赖。</summary>
public struct Rect
{
    public float X, Y, Width, Height;

    public Rect(float x, float y, float w, float h)
    {
        X = x; Y = y; Width = w; Height = h;
    }

    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;

    public static Rect Empty => new(0, 0, 0, 0);
}
