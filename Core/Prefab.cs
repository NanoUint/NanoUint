using System.Reflection;

namespace NanoUint;

/// <summary>Template for creating GameObjects with preset components and Transform values.</summary>
public sealed class Prefab
{
    /// <summary>Prefab name.</summary>
    public string Name { get; }

    /// <summary>Default Transform values.</summary>
    public float DefaultX { get; private set; } = 0.5f;
    public float DefaultY { get; private set; } = 0.5f;
    public int DefaultSortingOrder { get; private set; }
    public float DefaultOpacity { get; private set; } = 1f;

    private readonly List<Type> _componentTypes = new();

    private readonly List<Action<GameObject>> _configurators = new();

    public Prefab(string name)
    {
        Name = name;
    }

    #region Fluent configuration

    /// <summary>Adds a component type.</summary>
    public Prefab WithComponent<T>() where T : Component, new()
    {
        _componentTypes.Add(typeof(T));
        return this;
    }

    /// <summary>Adds a component and configures it after instantiation.</summary>
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

    /// <summary>Sets the default Transform values.</summary>
    public Prefab WithTransform(float x = 0.5f, float y = 0.5f, int order = 0, float opacity = 1f)
    {
        DefaultX = x;
        DefaultY = y;
        DefaultSortingOrder = order;
        DefaultOpacity = Math.Clamp(opacity, 0f, 1f);
        return this;
    }

    /// <summary>Registers a callback to run after an instance is created.</summary>
    public Prefab OnInstantiated(Action<GameObject> callback)
    {
        _configurators.Add(callback);
        return this;
    }

    #endregion

    #region Instantiation

    private static readonly MethodInfo? s_addComponentMethod = typeof(GameObject)
        .GetMethods()
        .FirstOrDefault(m => m.Name == nameof(GameObject.AddComponent) && m.IsGenericMethod);

    /// <summary>Creates an instance of the prefab in the given scene.</summary>
    public GameObject Instantiate(Scene scene, string? instanceName = null)
    {
        var go = scene.AddObject(instanceName ?? Name);

        // Invoke AddComponent<T>() via reflection
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
