using System.Reflection;

namespace NanoUint;

/// <summary>预制体：GameObject 的模板化创建。通过 Builder 模式预设组件和 Transform 属性。</summary>
public sealed class Prefab
{
    /// <summary>预制体名称。</summary>
    public string Name { get; }

    /// <summary>Transform 预设值。</summary>
    public float DefaultX { get; private set; } = 0.5f;
    public float DefaultY { get; private set; } = 0.5f;
    public int DefaultSortingOrder { get; private set; }
    public float DefaultOpacity { get; private set; } = 1f;

    /// <summary>组件类型列表（按添加顺序）。</summary>
    private readonly List<Type> _componentTypes = new();

    /// <summary>实例创建后的配置回调。</summary>
    private readonly List<Action<GameObject>> _configurators = new();

    public Prefab(string name)
    {
        Name = name;
    }

    #region 链式配置

    /// <summary>添加一个组件类型。</summary>
    public Prefab WithComponent<T>() where T : Component, new()
    {
        _componentTypes.Add(typeof(T));
        return this;
    }

    /// <summary>添加组件并在实例化后配置。</summary>
    public Prefab WithComponent<T>(Action<T> configure) where T : Component, new()
    {
        _componentTypes.Add(typeof(T));
        _configurators.Add(go =>
        {
            var comp = go.GetComponent<T>();
            if (comp != null) configure(comp);
        });
        return this;
    }

    /// <summary>设置 Transform 默认值。</summary>
    public Prefab WithTransform(float x = 0.5f, float y = 0.5f, int order = 0, float opacity = 1f)
    {
        DefaultX = x;
        DefaultY = y;
        DefaultSortingOrder = order;
        DefaultOpacity = Math.Clamp(opacity, 0f, 1f);
        return this;
    }

    /// <summary>注册一个实例创建后的回调。</summary>
    public Prefab OnInstantiated(Action<GameObject> callback)
    {
        _configurators.Add(callback);
        return this;
    }

    #endregion

    #region 实例化

    private static readonly MethodInfo? s_addComponentMethod = typeof(GameObject)
        .GetMethods()
        .FirstOrDefault(m => m.Name == nameof(GameObject.AddComponent) && m.IsGenericMethod);

    /// <summary>在指定场景中创建预制体实例。</summary>
    public GameObject Instantiate(Scene scene, string? instanceName = null)
    {
        var go = scene.AddObject(instanceName ?? Name);

        // 通过反射调用 AddComponent<T>()
        foreach (var type in _componentTypes)
        {
            try
            {
                s_addComponentMethod?.MakeGenericMethod(type).Invoke(go, null);
            }
            catch (TargetInvocationException ex)
            {
                Debug.LogError($"Prefab: Failed to add component {type.Name}: {ex.InnerException?.Message}");
            }
        }

        go.Transform.X = DefaultX;
        go.Transform.Y = DefaultY;
        go.Transform.SortingOrder = DefaultSortingOrder;
        go.Transform.Opacity = DefaultOpacity;

        foreach (var config in _configurators)
            config(go);

        return go;
    }
    #endregion
}
