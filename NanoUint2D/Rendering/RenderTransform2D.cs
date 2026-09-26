namespace NanoUint.Rendering;

/// <summary>World-space transform data passed to render nodes.</summary>
public readonly struct RenderTransform2D
{
    public float WorldX { get; init; }
    public float WorldY { get; init; }
    public float Opacity { get; init; }
    public bool FlipX { get; init; }
}

/// <summary>Sorting key for render order: SortingLayer → OrderInLayer → Z.</summary>
public readonly struct SortingKey : IComparable<SortingKey>
{
    public int SortingLayer { get; init; }
    public int OrderInLayer { get; init; }
    public int Z { get; init; }

    public int CompareTo(SortingKey other)
    {
        int cmp = SortingLayer.CompareTo(other.SortingLayer);
        if (cmp != 0) return cmp;
        cmp = OrderInLayer.CompareTo(other.OrderInLayer);
        if (cmp != 0) return cmp;
        return Z.CompareTo(other.Z);
    }
}
