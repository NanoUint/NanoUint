using NanoUint.Diagnostics;

namespace NanoUint;

/// <summary>场景：GameObject 的容器，也是保存/加载的序列化单元。</summary>
public sealed class Scene
{
    private readonly List<GameObject> _objects = new();
    private readonly Dictionary<string, GameObject> _objectMap = new();
    private bool _orderDirty;

    /// <summary>场景名称。</summary>
    public string Name { get; }

    /// <summary>场景中的根对象列表（已销毁对象自动过滤）。</summary>
    public IReadOnlyList<GameObject> RootObjects
    {
        get
        {
            _objects.RemoveAll(o => o.IsDestroyed);
            return _objects.AsReadOnly();
        }
    }

    public Scene(string name)
    {
        Name = name;
        Logger.Trace("Scene", $"Created: '{name}'");
    }

    #region 对象管理

    /// <summary>向场景添加一个已构造的 GameObject（如 Instantiate 产出）。</summary>
    public GameObject AddObject(GameObject go)
    {
        // 检测同名冲突并自动重命名
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

    /// <summary>向场景添加一个新的 GameObject。</summary>
    public GameObject AddObject(string name = "GameObject")
    {
        var go = new GameObject(name);
        return AddObject(go);
    }

    /// <summary>按名称查找对象。O(1)。</summary>
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

    /// <summary>从场景中移除一个对象。</summary>
    public void RemoveObject(GameObject obj)
    {
        _objects.Remove(obj);
        if (_objectMap.TryGetValue(obj.Name, out var existing) && existing == obj)
            _objectMap.Remove(obj.Name);
        _orderDirty = true;
        Logger.Trace("Scene", $"'{Name}': -{obj.Name} (total:{_objects.Count})");
    }

    #endregion

    #region 主循环

    /// <summary>更新场景中所有激活的对象。由引擎每帧调用。</summary>
    internal void Update(float deltaTime)
    {
        // 清理已销毁对象
        _objects.RemoveAll(o => o.IsDestroyed);

        // 按 SortingOrder 排序（懒排序）
        if (_orderDirty)
        {
            _objects.Sort((a, b) => a.Transform.SortingOrder.CompareTo(b.Transform.SortingOrder));
            _orderDirty = false;
        }

        // 更新所有对象
        foreach (var go in _objects)
            go.UpdateComponents(deltaTime);

        // 驱动协程
        CoroutineScheduler.Instance.Tick(deltaTime);
    }

    #endregion

    #region 保存/加载

    /// <summary>收集场景中所有对象的可保存状态。</summary>
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

    /// <summary>从保存状态恢复场景。</summary>
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

#region 保存状态 DTO

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