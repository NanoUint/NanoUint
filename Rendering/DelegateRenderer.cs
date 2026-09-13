using System.Windows;
using System.Windows.Controls;

namespace NanoUint.Rendering;

/// <summary>Adapts an existing Create+Update method pair into an IRenderer2D.</summary>
internal sealed class DelegateRenderer<T> : IRenderer2D where T : Component
{
    private readonly Func<UIElement> _create;
    private readonly Func<T, UIElement, bool> _update;
    private readonly Type _componentType = typeof(T);
    private readonly int _order;

    public Type ComponentType => _componentType;
    public int Order => _order;

    public DelegateRenderer(int order, Func<UIElement> create, Func<T, UIElement, bool> update)
    {
        _order = order;
        _create = create;
        _update = update;
    }

    public bool TryCreate(Component c, out IRenderNode? node)
    {
        if (c is not T typed) { node = null; return false; }
        node = new DelegateRenderNode<T>(_create(), typed, _update);
        return true;
    }
}

/// <summary>RenderNode that delegates Sync to a function.</summary>
internal sealed class DelegateRenderNode<T> : WpfRenderNode where T : Component
{
    private readonly Func<T, UIElement, bool> _update;
    private readonly T _component;

    public DelegateRenderNode(UIElement element, T component, Func<T, UIElement, bool> update)
        : base(element)
    {
        _component = component;
        _update = update;
    }

    public override void Sync(Component c)
    {
        if (c is T typed)
            _update(typed, Element);
    }
}
