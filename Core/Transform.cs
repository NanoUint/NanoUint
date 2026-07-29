namespace NanoUint;

/// <summary>
/// 每个 GameObject 必定拥有的定位组件。类似 UnityEngine.Transform。
/// 控制对象在画面中的位置、透明度、翻转和渲染排序。
/// </summary>
public sealed class Transform : Component
{
    private float _x;
    private float _y;
    private float _opacity = 1f;
    private bool _flipX;
    private int _sortingOrder;

    /// <summary>水平位置：0=左侧，0.5=居中，1=右侧。</summary>
    public float X
    {
        get => _x;
        set { if (!_x.Equals(value)) { _x = value; MarkDirty(); } }
    }

    /// <summary>垂直位置：0=顶部，1=底部。</summary>
    public float Y
    {
        get => _y;
        set { if (!_y.Equals(value)) { _y = value; MarkDirty(); } }
    }

    /// <summary>不透明度：0=完全透明，1=完全不透明。</summary>
    public float Opacity
    {
        get => _opacity;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (!_opacity.Equals(clamped)) { _opacity = clamped; MarkDirty(); }
        }
    }

    /// <summary>水平翻转。</summary>
    public bool FlipX
    {
        get => _flipX;
        set { if (_flipX != value) { _flipX = value; MarkDirty(); } }
    }

    /// <summary>渲染排序。值越大越靠前显示。</summary>
    public int SortingOrder
    {
        get => _sortingOrder;
        set { if (_sortingOrder != value) { _sortingOrder = value; MarkDirty(); } }
    }
}
