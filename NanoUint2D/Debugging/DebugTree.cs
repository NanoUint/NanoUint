namespace NanoUint.Debugging;

/// <summary>
/// A node in the runtime debug tree. Pure data, no WPF dependency, so game code can build it
/// from any layer without dragging UI types along.
/// </summary>
public sealed class DebugTreeNode
{
    private readonly List<DebugTreeNode> _children = new();

    public DebugTreeNode(string name) => Name = name;

    /// <summary>Display name of the node.</summary>
    public string Name { get; set; }

    /// <summary>Optional value rendered to the right of the name.</summary>
    public string? Value { get; set; }

    /// <summary>Optional longer text shown on hover.</summary>
    public string? Tooltip { get; set; }

    /// <summary>Optional object the inspector should resolve when this node is selected.</summary>
    public object? Payload { get; set; }

    /// <summary>Whether the node is expanded in the panel.</summary>
    public bool IsExpanded { get; set; }

    public DebugTreeNode? Parent { get; private set; }

    public IReadOnlyList<DebugTreeNode> Children => _children;

    public DebugTreeNode AddChild(string name)
    {
        var child = new DebugTreeNode(name) { Parent = this };
        _children.Add(child);
        return child;
    }

    public DebugTreeNode GetOrAddChild(string name)
        => FindChild(name) ?? AddChild(name);

    public DebugTreeNode? FindChild(string name)
    {
        foreach (var c in _children)
        {
            if (string.Equals(c.Name, name, StringComparison.Ordinal)) return c;
        }
        return null;
    }

    /// <summary>Detaches this node from its parent.</summary>
    public void Remove() => Parent?._children.Remove(this);

    internal void ClearChildren() => _children.Clear();
}

/// <summary>
/// Runtime tree that game code registers debug information into.
/// The engine owns the panel that renders it; the game only writes nodes and values.
/// </summary>
public sealed class DebugTree
{
    public DebugTree(string rootName = "Debug") => Root = new DebugTreeNode(rootName);

    public DebugTreeNode Root { get; }

    /// <summary>Raised whenever the tree is mutated. The panel subscribes to refresh lazily.</summary>
    public event Action? Changed;

    /// <summary>Signals that the tree changed. Call this after mutating nodes and values in bulk.</summary>
    public void NotifyChanged() => Changed?.Invoke();

    /// <summary>Resolves a slash-separated path such as "Phone/Messages/0", creating missing nodes.</summary>
    public DebugTreeNode GetOrAdd(string path)
    {
        var node = Root;
        foreach (var segment in SplitPath(path))
        {
            node = node.GetOrAddChild(segment);
        }
        return node;
    }

    /// <summary>Resolves a path without creating anything. Returns null when absent.</summary>
    public DebugTreeNode? Find(string path)
    {
        var node = Root;
        foreach (var segment in SplitPath(path))
        {
            node = node.FindChild(segment);
            if (node == null) return null;
        }
        return node;
    }

    /// <summary>Creates the node if needed and assigns its displayed value.</summary>
    public DebugTreeNode SetValue(string path, object? value, string? tooltip = null)
    {
        var node = GetOrAdd(path);
        node.Value = value?.ToString();
        node.Tooltip = tooltip;
        return node;
    }

    /// <summary>Removes a subtree by path. Removing the root is ignored.</summary>
    public bool Remove(string path)
    {
        var node = Find(path);
        if (node == null || ReferenceEquals(node, Root)) return false;
        node.Remove();
        return true;
    }

    /// <summary>Drops every child of the root.</summary>
    public void Clear()
    {
        Root.ClearChildren();
        NotifyChanged();
    }

    private static IEnumerable<string> SplitPath(string path)
    {
        foreach (var s in path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return s;
        }
    }
}

/// <summary>
/// Optional contract for game-side systems that want to contribute nodes on the fly
/// instead of pushing values manually. Registered providers are polled when the panel refreshes.
/// </summary>
public interface IDebugTreeProvider
{
    /// <summary>Display name of the contributed subtree, e.g. "Phone".</summary>
    string RootName { get; }

    /// <summary>Rebuilds this provider's subtree under the given node.</summary>
    void Build(DebugTreeNode root);
}

/// <summary>Entry point for the runtime debug tree and the optional providers feeding it.</summary>
public static class DebugRegistry
{
    private static readonly List<IDebugTreeProvider> Providers = new();

    /// <summary>The tree game code registers debug information into.</summary>
    public static DebugTree Tree { get; } = new("Debug");

    /// <summary>When false, the panel skips rendering the custom tree tab.</summary>
    public static bool IsEnabled { get; set; } = true;

    /// <summary>Registers a provider that contributes its own subtree.</summary>
    public static void RegisterProvider(IDebugTreeProvider provider)
    {
        if (!Providers.Contains(provider)) Providers.Add(provider);
    }

    public static void UnregisterProvider(IDebugTreeProvider provider) => Providers.Remove(provider);

    public static IReadOnlyList<IDebugTreeProvider> GetProviders() => Providers;

    /// <summary>Re-runs every provider, rebuilding their subtrees in place.</summary>
    public static void RefreshProviders()
    {
        foreach (var provider in Providers)
        {
            var node = Tree.Root.GetOrAddChild(provider.RootName);
            node.ClearChildren();
            provider.Build(node);
        }
        Tree.NotifyChanged();
    }
}
