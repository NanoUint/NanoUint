namespace NanoUint.Rendering;

/// <summary>Creates and manages WPF visuals for one component type.</summary>
public interface IRenderer2D
{
    /// <summary>The component type this renderer handles.</summary>
    Type ComponentType { get; }

    /// <summary>Higher values draw later (on top).</summary>
    int Order { get; }

    /// <summary>Attempts to create a visual for the given component.</summary>
    bool TryCreate(Component c, out IRenderNode? node);
}

/// <summary>A single visual node managed by an <see cref="IRenderer2D"/>.</summary>
public interface IRenderNode : IDisposable
{
    /// <summary>Updates the visual from the component's current state.</summary>
    void Sync(Component c);

    /// <summary>Applies world-space transform (position, scale, opacity, flip).</summary>
    void SetTransform(in RenderTransform2D t);

    /// <summary>Sets the sorting key (layer → order → z).</summary>
    void SetSorting(SortingKey key);

    /// <summary>The underlying WPF element for embedding in the canvas.</summary>
    System.Windows.UIElement Element { get; }
}
