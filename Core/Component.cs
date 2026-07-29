namespace NanoUint;

/// <summary>
/// 所有组件的抽象基类。类似 UnityEngine.Component。
/// 挂载到 GameObject 上，由引擎主循环驱动生命周期。
/// </summary>
public abstract class Component
{
    /// <summary>此组件所属的 GameObject。</summary>
    public GameObject? GameObject { get; internal set; }

    /// <summary>是否启用。禁用的组件不会收到 Update 回调。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>组件是否已被销毁。</summary>
    public bool IsDestroyed { get; internal set; }

    /// <summary>渲染版本号。当视觉属性变更时递增。WpfRenderer 用于增量同步。</summary>
    internal int RenderVersion { get; private set; }

    /// <summary>标记组件需要重新同步到 WPF 控件树。</summary>
    internal void MarkDirty()
    {
        unchecked { RenderVersion++; }
    }

    // ── 生命周期（由引擎主循环调用） ──

    /// <summary>当组件被添加到 GameObject 后立即调用。</summary>
    protected internal virtual void Awake() { }

    /// <summary>在第一次 Update 之前调用（仅当 Enabled=true 时）。</summary>
    protected internal virtual void Start() { }

    /// <summary>每帧调用。deltaTime 单位为秒。</summary>
    protected internal virtual void Update(float deltaTime) { }

    /// <summary>组件被销毁时调用。</summary>
    protected internal virtual void OnDestroy() { }

    internal void Destroy()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;
        OnDestroy();
    }

    public override string ToString() =>
        $"{GetType().Name} (GO={(GameObject != null ? GameObject.Name : "null")})";
}
