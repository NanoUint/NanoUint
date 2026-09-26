namespace NanoUint.Rendering;

/// <summary>Registry of component-type → renderer mappings.</summary>
public sealed class RendererRegistry
{
    private List<IRenderer2D>? _sortedCache;
    private readonly Dictionary<Type, IRenderer2D> _map = new();

    /// <summary>Register a renderer. Later registrations for the same type override earlier ones.</summary>
    public void Register<TRenderer>() where TRenderer : IRenderer2D, new()
    {
        var r = new TRenderer();
        _map[r.ComponentType] = r;
        _sortedCache = null;
    }

    /// <summary>Register an existing renderer instance.</summary>
    public void Register(IRenderer2D renderer)
    {
        _map[renderer.ComponentType] = renderer;
        _sortedCache = null;
    }

    /// <summary>Try to get the renderer for a component type.</summary>
    public bool TryGet(Type componentType, out IRenderer2D renderer)
    {
        return _map.TryGetValue(componentType, out renderer!);
    }

    /// <summary>Try to get the renderer for a component.</summary>
    public bool TryGet(Component c, out IRenderer2D renderer)
    {
        return TryGet(c.GetType(), out renderer);
    }

    /// <summary>All registered renderers, sorted by Order.</summary>
    public IReadOnlyList<IRenderer2D> All => _sortedCache ??= _map.Values.OrderBy(r => r.Order).ToList();
}
