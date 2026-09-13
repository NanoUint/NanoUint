using NanoUint.Diagnostics;

namespace NanoUint;

/// <summary>Scene: a container of GameObjects and the serialization unit for save/load.</summary>
public sealed class Scene
{
    private readonly List<GameObject> _objects = new();
    private readonly Dictionary<string, GameObject> _objectMap = new();
    private bool _orderDirty;

    /// <summary>Scene name.</summary>
    public string Name { get; }

    /// <summary>All objects in the scene (including destroyed ones until cleanup).</summary>
    public IReadOnlyList<GameObject> AllObjects
    {
        get
        {
            _objects.RemoveAll(o => o.IsDestroyed);
            return _objects.AsReadOnly();
        }
    }

    /// <summary>Objects with no parent (true roots). Alias for backward compatibility.</summary>
    public IReadOnlyList<GameObject> RootObjects => AllObjects;

    /// <summary>Returns only objects whose Transform has no parent.</summary>
    public IReadOnlyList<GameObject> GetRootObjects()
    {
        _objects.RemoveAll(o => o.IsDestroyed);
        return _objects.Where(o => o.Transform.Parent == null).ToList().AsReadOnly();
    }

    public Scene(string name)
    {
        Name = name;
        Logger.Trace("Scene", $"Created: '{name}'");
    }

    #region Object management

    /// <summary>Adds an already-constructed GameObject to the scene.</summary>
    public GameObject AddObject(GameObject go)
    {
        // Detect name collisions and auto-rename
        if (_objectMap.ContainsKey(go.Name))
        {
            int suffix = 1;
            string originalName = go.Name;
            while (_objectMap.ContainsKey($"{originalName}_{suffix}"))
                suffix++;
            Logger.Warning("Scene",
                $"Name collision: '{go.Name}' already exists in scene '{Name}'. Renaming to '{originalName}_{suffix}'.");
            go.Name = $"{originalName}_{suffix}";
        }

        _objects.Add(go);
        _objectMap[go.Name] = go;
        _orderDirty = true;
        go.NotifyAdded(this);
        Logger.Trace("Scene", $"'{Name}': +{go.Name} (total:{_objects.Count})");
        return go;
    }

    /// <summary>Adds a new GameObject to the scene.</summary>
    public GameObject AddObject(string name = "GameObject")
    {
        var go = new GameObject(name);
        return AddObject(go);
    }

    /// <summary>Finds an object by name. O(1).</summary>
    public GameObject? FindObject(string name)
    {
        _objectMap.TryGetValue(name, out var go);
        if (go != null && go.IsDestroyed)
        {
            _objectMap.Remove(name);
            return null;
        }
        return go;
    }

    /// <summary>Removes an object from the scene.</summary>
    public void RemoveObject(GameObject obj)
    {
        _objects.Remove(obj);
        if (_objectMap.TryGetValue(obj.Name, out var existing) && existing == obj)
            _objectMap.Remove(obj.Name);
        _orderDirty = true;
        Logger.Trace("Scene", $"'{Name}': -{obj.Name} (total:{_objects.Count})");
    }

    /// <summary>Removes all destroyed objects from internal lists. Call at frame end.</summary>
    public int DestroyPending()
    {
        int count = _objects.RemoveAll(o => o.IsDestroyed);
        var toRemove = _objectMap.Where(kv => kv.Value.IsDestroyed).Select(kv => kv.Key).ToList();
        foreach (var key in toRemove) _objectMap.Remove(key);
        if (count > 0) _orderDirty = true;
        return count;
    }

    #endregion

    #region Main loop

    internal void Update(float deltaTime)
    {
        _objects.RemoveAll(o => o.IsDestroyed);

        // Sort by SortingOrder (lazy: only when dirty)
        if (_orderDirty)
        {
            _objects.Sort((a, b) => a.Transform.SortingOrder.CompareTo(b.Transform.SortingOrder));
            _orderDirty = false;
        }

        // Snapshot iteration: objects may be destroyed or added during Update
        foreach (var go in _objects.ToList())
            go.UpdateComponents(deltaTime);

        CoroutineScheduler.Instance.Tick(deltaTime);
    }

    #endregion

    #region Save/Load

    /// <summary>Collects the savable state of all objects in the scene.</summary>
    public SceneSaveState CollectSaveState()
    {
        var state = new SceneSaveState
        {
            SceneName = Name,
            Objects = _objects
                .Where(o => !o.IsDestroyed)
                .Select(o => new GameObjectSaveState
                {
                    Name = o.Name,
                    ActiveSelf = o.ActiveSelf,
                    TransformX = o.Transform.X,
                    TransformY = o.Transform.Y,
                    TransformOpacity = o.Transform.Opacity,
                    TransformFlipX = o.Transform.FlipX,
                    SortingOrder = o.Transform.SortingOrder,
                })
                .ToList()
        };
        return state;
    }

    /// <summary>Restores the scene from a saved state.</summary>
    public void ApplySaveState(SceneSaveState state)
    {
        foreach (var objState in state.Objects)
        {
            var go = FindObject(objState.Name);
            if (go != null)
            {
                go.ActiveSelf = objState.ActiveSelf;
                go.Transform.X = objState.TransformX;
                go.Transform.Y = objState.TransformY;
                go.Transform.Opacity = objState.TransformOpacity;
                go.Transform.FlipX = objState.TransformFlipX;
                go.Transform.SortingOrder = objState.SortingOrder;
            }
        }
    }

    public override string ToString() => $"Scene '{Name}' ({_objects.Count(o => !o.IsDestroyed)} objects)";
    #endregion
}

#region Save-state DTOs

public class SceneSaveState
{
    public string SceneName { get; set; } = "";
    public List<GameObjectSaveState> Objects { get; set; } = new();
}

public class GameObjectSaveState
{
    public string Name { get; set; } = "";
    public bool ActiveSelf { get; set; }
    public float TransformX { get; set; }
    public float TransformY { get; set; }
    public float TransformOpacity { get; set; }
    public bool TransformFlipX { get; set; }
    public int SortingOrder { get; set; }
}

#endregion
