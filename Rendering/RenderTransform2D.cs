namespace NanoUint.Rendering;

/// <summary>World-space transform data passed to render nodes.</summary>
public readonly struct RenderTransform2D
{
    public float WorldX { get; init; }
    public float WorldY { get; init; }
    public float Opacity { get; init; }
    public bool FlipX { get; init; }
}

/// <summary>Sorting key for render order.</summary>
public readonly struct SortingKey : IComparable<SortingKey>
{
    public int SortingOrder { get; init; }

    public int CompareTo(SortingKey other) => SortingOrder.CompareTo(other.SortingOrder);
}
