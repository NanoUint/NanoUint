namespace NanoUintVN.Dialogue;

/// <summary>Stable ID for referencing scene objects (replaces magic strings).</summary>
public readonly record struct ElementId(string Value)
{
    public static readonly ElementId None = new(string.Empty);
    public override string ToString() => Value;
}

/// <summary>Base class for all elements that can live on a page.</summary>
public abstract class VNElement
{
    public ElementId Id { get; init; } = ElementId.None;
}
