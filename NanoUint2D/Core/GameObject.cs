using NanoUint.Diagnostics;

namespace NanoUint;

/// <summary>Scene object that components can be attached to.</summary>
public sealed class GameObject
{
    private readonly List<Component> _components = new();
    private readonly Dictionary<Type, List<Component>> _componentCache = new();
    private bool _started;

    /// <summary>Object name.</summary>
    public string Name { get; set; }

    /// <summary>Whether this object is active.</summary>
    public bool ActiveSelf { get; set; } = true;

    /// <summary>The scene this object belongs to.</summary>
    public Scene? Scene { get; internal set; }

    /// <summary>The Transform component every GameObject is guaranteed to have.
    /// Returns the actual Transform (or RectTransform) currently on this object.</summary>
    public Transform Transform => GetComponent<Transform>()
        ?? throw new InvalidOperationException($"GameObject '{Name}' has no Transform.");

    /// <summary>All components on this object (read-only).</summary>
    public IReadOnlyList<Component> Components => _components;

    internal void MarkComponentsDirty()
    {
        foreach (var comp in _components.ToList())
        {
            if (comp.GetType() != typeof(Transform))
                comp.MarkDirty();
        }
    }

    /// <summary>Whether this object has been destroyed.</summary>
    public bool IsDestroyed { get; private set; }

    public GameObject(string name = "GameObject")
    {
        Name = name;
        AddComponent<Transform>();
    }

    #region Component management

    /// <summary>Adds a component and returns it.</summary>
    public T AddComponent<T>() where T : Component, new()
    {
        if (IsDestroyed)
            throw new InvalidOperationException($"GameObject '{Name}' is destroyed.");

        var comp = new T { GameObject = this, Enabled = true };
        _components.Add(comp);

        var type = typeof(T);
        if (!_componentCache.ContainsKey(type))
            _componentCache[type] = new List<Component>();
        _componentCache[type].Add(comp);

        var baseType = type.BaseType;
        while (baseType != null && typeof(Component).IsAssignableFrom(baseType))
        {
            if (!_componentCache.ContainsKey(baseType))
                _componentCache[baseType] = new List<Component>();
            _componentCache[baseType].Add(comp);
            baseType = baseType.BaseType;
        }

        if (Scene != null)
        {
            comp.Awake();
        }

        Logger.Trace("GO", $"  {Name}.AddComponent<{typeof(T).Name}> (total:{_components.Count})");
        return comp;
    }

    /// <summary>Gets the first component of the given type, or null if none exists.</summary>
    public T? GetComponent<T>() where T : Component
    {
        if (_componentCache.TryGetValue(typeof(T), out var list) && list.Count > 0)
            return (T)list[0];
        return null;
    }

    /// <summary>Tries to get the first component of the given type.</summary>
    public bool TryGetComponent<T>(out T? result) where T : Component
    {
        result = GetComponent<T>();
        return result != null;
    }

    /// <summary>Gets all components of the given type.</summary>
    public IReadOnlyList<T> GetComponents<T>() where T : Component
    {
        if (_componentCache.TryGetValue(typeof(T), out var list))
            return list.Cast<T>().ToList();
        return Array.Empty<T>();
    }

    /// <summary>Removes a component and destroys it.</summary>
    public void RemoveComponent(Component component)
    {
        if (component == Transform)
            throw new InvalidOperationException("Cannot remove the Transform component.");

        _components.Remove(component);
        foreach (var kv in _componentCache.Values)
            kv.Remove(component);
        component.Destroy();
    }

    /// <summary>Destroys this GameObject and all of its components.</summary>
    public void Destroy()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;
        Logger.Trace("GO", $"Destroy: {Name} ({_components.Count} components)");
        Scene?.RemoveObject(this);
        foreach (var comp in _components.ToList())
            comp.Destroy();
    }

    #endregion

    #region Internals

    internal void NotifyAdded(Scene scene)
    {
        Scene = scene;
        foreach (var comp in _components.ToList())
            comp.Awake();
    }

    internal void NotifyStart()
    {
        if (_started) return;
        _started = true;
        foreach (var comp in _components.ToList())
        {
            if (comp.Enabled && !comp.IsDestroyed)
                comp.Start();
        }
    }

    internal void UpdateComponents(float deltaTime)
    {
        if (!ActiveSelf || IsDestroyed) return;
        NotifyStart();
        foreach (var comp in _components.ToList())
        {
            if (comp.Enabled && !comp.IsDestroyed)
                comp.Update(deltaTime);
        }
    }

    public override string ToString() => $"{(IsDestroyed ? "[Destroyed] " : "")}{Name} ({_components.Count} components)";

    #endregion

    #region Instantiate (template cloning)

    /// <summary>Deep-copies a GameObject and all of its components into a target scene.</summary>
    /// <param name="original">Source object (not modified).</param>
    /// <param name="targetScene">Target scene; null uses the origin's scene.</param>
    /// <param name="newName">Clone name; null appends a "(Clone)" suffix automatically.</param>
    public static GameObject Instantiate(GameObject original, Scene? targetScene = null, string? newName = null)
    {
        if (original.IsDestroyed)
            throw new InvalidOperationException($"Cannot instantiate destroyed GameObject '{original.Name}'.");

        var scene = targetScene ?? original.Scene;
        if (scene == null)
            throw new InvalidOperationException("Cannot instantiate: original has no Scene and no targetScene provided.");

        var clone = new GameObject(newName ?? original.Name + "(Clone)");
        clone.ActiveSelf = original.ActiveSelf;

        foreach (var comp in original._components)
        {
            if (comp is Transform) continue;
            var compClone = comp.Clone();
            AddComponentToGO(clone, compClone);
        }

        if (original.Transform is RectTransform originalRT)
        {
            var rtClone = (RectTransform)originalRT.Clone();
            var defaultTransform = clone.Transform;
            clone._components.Remove(defaultTransform);
            clone._componentCache.Remove(typeof(Transform));
            AddComponentToGO(clone, rtClone);

            if (!clone._componentCache.ContainsKey(typeof(Transform)))
                clone._componentCache[typeof(Transform)] = new List<Component>();
            clone._componentCache[typeof(Transform)].Add(rtClone);
        }
        else
        {
            var origT = original.Transform;
            clone.Transform.Position = origT.Position;
            clone.Transform.Opacity = origT.Opacity;
            clone.Transform.FlipX = origT.FlipX;
        }

        scene.AddObject(clone);

        Logger.Trace("GO", $"Instantiate: '{original.Name}' → '{clone.Name}' ({clone._components.Count} components)");
        return clone;
    }

    private static void AddComponentToGO(GameObject go, Component comp)
    {
        comp.GameObject = go;
        comp.Enabled = comp.Enabled;
        go._components.Add(comp);

        var type = comp.GetType();
        if (!go._componentCache.ContainsKey(type))
            go._componentCache[type] = new List<Component>();
        go._componentCache[type].Add(comp);
    }
    #endregion
}
