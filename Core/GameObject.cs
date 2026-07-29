using NanoUint.Diagnostics;

namespace NanoUint;

/// <summary>
/// 场景中所有对象的容器。类似 UnityEngine.GameObject。
/// 本身不做任何事——行为通过挂载 Component 来组合。
/// </summary>
public sealed class GameObject
{
    private readonly List<Component> _components = new();
    private readonly Dictionary<Type, List<Component>> _componentCache = new();
    private bool _started;

    /// <summary>对象名称（用于调试和查找）。</summary>
    public string Name { get; set; }

    /// <summary>是否处于激活状态。非激活的对象及其组件不会收到 Update。</summary>
    public bool ActiveSelf { get; set; } = true;

    /// <summary>此对象所属的场景。</summary>
    public Scene? Scene { get; internal set; }

    /// <summary>每个 GameObject 必定拥有的 Transform 组件。</summary>
    public Transform Transform { get; }

    /// <summary>此对象上的所有组件（只读）。</summary>
    public IReadOnlyList<Component> Components => _components;

    /// <summary>对象是否已被销毁。</summary>
    public bool IsDestroyed { get; private set; }

    public GameObject(string name = "GameObject")
    {
        Name = name;
        Transform = AddComponent<Transform>();
    }

    // ── 组件管理 ──

    /// <summary>添加一个组件并返回它。</summary>
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

        // 如果已加入场景，立即触发 Awake
        if (Scene != null)
        {
            comp.Awake();
        }

        Logger.Trace("GO", $"  {Name}.AddComponent<{typeof(T).Name}> (total:{_components.Count})");
        return comp;
    }

    /// <summary>获取第一个指定类型的组件。若不存在则返回 null。</summary>
    public T? GetComponent<T>() where T : Component
    {
        if (_componentCache.TryGetValue(typeof(T), out var list) && list.Count > 0)
            return (T)list[0];
        return null;
    }

    /// <summary>尝试获取第一个指定类型的组件。</summary>
    public bool TryGetComponent<T>(out T? result) where T : Component
    {
        result = GetComponent<T>();
        return result != null;
    }

    /// <summary>获取所有指定类型的组件。</summary>
    public IReadOnlyList<T> GetComponents<T>() where T : Component
    {
        if (_componentCache.TryGetValue(typeof(T), out var list))
            return list.Cast<T>().ToList();
        return Array.Empty<T>();
    }

    /// <summary>移除一个组件（标记销毁，下一帧清理）。</summary>
    public void RemoveComponent(Component component)
    {
        if (component == Transform)
            throw new InvalidOperationException("Cannot remove the Transform component.");

        _components.Remove(component);
        foreach (var kv in _componentCache.Values)
            kv.Remove(component);
        component.Destroy();
    }

    /// <summary>销毁此 GameObject 及其所有组件。</summary>
    public void Destroy()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;
        Logger.Trace("GO", $"Destroy: {Name} ({_components.Count} components)");
        Scene?.RemoveObject(this);
        foreach (var comp in _components.ToList())
            comp.Destroy();
    }

    // ── 内部 ──

    internal void NotifyAdded(Scene scene)
    {
        Scene = scene;
        foreach (var comp in _components)
            comp.Awake();
    }

    internal void NotifyStart()
    {
        if (_started) return;
        _started = true;
        foreach (var comp in _components)
        {
            if (comp.Enabled && !comp.IsDestroyed)
                comp.Start();
        }
    }

    internal void UpdateComponents(float deltaTime)
    {
        if (!ActiveSelf || IsDestroyed) return;
        NotifyStart();
        foreach (var comp in _components)
        {
            if (comp.Enabled && !comp.IsDestroyed)
                comp.Update(deltaTime);
        }
    }

    public override string ToString() => $"{(IsDestroyed ? "[Destroyed] " : "")}{Name} ({_components.Count} components)";
}
