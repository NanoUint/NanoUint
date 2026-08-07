using NanoUint.Diagnostics;

namespace NanoUint;

/// <summary>所有组件的抽象基类。挂载到 GameObject 上，由引擎主循环驱动生命周期。</summary>
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

    #region 生命周期（由引擎主循环调用）

    /// <summary>当组件被添加到 GameObject 后立即调用。</summary>
    protected internal virtual void Awake() { }

    /// <summary>在第一次 Update 之前调用（仅当 Enabled=true 时）。</summary>
    protected internal virtual void Start() { }

    /// <summary>每帧调用。deltaTime 单位为秒。</summary>
    protected internal virtual void Update(float deltaTime) { }

    /// <summary>组件被销毁时调用。</summary>
    protected internal virtual void OnDestroy() { }

    /// <summary>鼠标悬停时在光标旁显示的提示文本（null = 不显示）。供 HintRenderer 使用。</summary>
    public virtual string? HintText { get; set; }

    /// <summary>深拷贝此组件的状态到新实例。子类应调用 base.Clone() 并覆写特定字段。</summary>
    internal virtual Component Clone()
    {
        var type = GetType();
        var clone = (Component)Activator.CreateInstance(type)!;

        // 反射拷贝所有实例字段（跳过事件委托和不可写字段）
        foreach (var field in type.GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance))
        {
            // 跳过事件委托（C# 编译器生成的多播委托字段）
            if (field.FieldType.BaseType == typeof(MulticastDelegate))
                continue;

            try
            {
                var value = field.GetValue(this);

                if (value is System.Collections.IList list && !field.FieldType.IsArray)
                {
                    var clonedList = (System.Collections.IList)Activator.CreateInstance(field.FieldType)!;
                    foreach (var item in list) clonedList.Add(item);
                    field.SetValue(clone, clonedList);
                }
                else if (value is System.Collections.IDictionary dict)
                {
                    var clonedDict = (System.Collections.IDictionary)Activator.CreateInstance(field.FieldType)!;
                    foreach (var key in dict.Keys) clonedDict[key] = dict[key];
                    field.SetValue(clone, clonedDict);
                }
                else if (value is Array arr)
                {
                    var clonedArray = (Array)Activator.CreateInstance(field.FieldType, arr.Length)!;
                    Array.Copy(arr, clonedArray, arr.Length);
                    field.SetValue(clone, clonedArray);
                }
                else
                {
                    field.SetValue(clone, value);
                }
            }
            catch (Exception ex) { Logger.Trace("Component", $"Clone skipped field '{field.Name}' on {GetType().Name}: {ex.Message}"); }
        }

        // 重置运行时状态
        clone.GameObject = null;
        clone.Enabled = Enabled;
        return clone;
    }

    internal void Destroy()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;
        OnDestroy();
    }

    public override string ToString() =>
        $"{GetType().Name} (GO={(GameObject != null ? GameObject.Name : "null")})";
    #endregion
}
