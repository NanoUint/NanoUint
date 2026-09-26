using NanoUint.Diagnostics;

namespace NanoUint;

/// <summary>Abstract base class for all components.</summary>
public abstract class Component
{
    /// <summary>The GameObject this component belongs to.</summary>
    public GameObject? GameObject { get; internal set; }

    /// <summary>Whether the component is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether this component has been destroyed.</summary>
    public bool IsDestroyed { get; internal set; }

    internal int RenderVersion { get; private set; }

    internal void MarkDirty()
    {
        unchecked { RenderVersion++; }
    }

    #region Lifecycle (called by the engine main loop)

    protected internal virtual void Awake() { }

    protected internal virtual void Start() { }

    protected internal virtual void Update(float deltaTime) { }

    protected internal virtual void OnDestroy() { }

    /// <summary>Tooltip shown next to the cursor on hover; null means hidden.</summary>
    public virtual string? HintText { get; set; }

    internal virtual Component Clone()
    {
        var type = GetType();
        var clone = (Component)Activator.CreateInstance(type)!;

        // Reflectively copy all instance fields (skipping event delegates and non-writable fields)
        foreach (var field in type.GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance))
        {
            // Skip event delegates (compiler-generated multicast delegate fields)
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
