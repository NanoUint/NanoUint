namespace NanoUint.Drawing;

/// <summary>Pure C# rectangle struct with no WPF dependency.</summary>
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
