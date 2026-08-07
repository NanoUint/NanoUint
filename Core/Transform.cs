using NanoUint.Drawing;

namespace NanoUint;

/// <summary>每个 GameObject 必定拥有的定位组件。控制位置、透明度、翻转和渲染排序，支持父子层级。</summary>
public class Transform : Component
{
    private Vector3 _position;
    private float _opacity = 1f;
    private bool _flipX;
    private Transform? _parent;
    private readonly List<Transform> _children = new();

    /// <summary>父 Transform。null 表示根对象。</summary>
    public Transform? Parent => _parent;

    /// <summary>所有直接子对象的 Transform 列表。</summary>
    public IReadOnlyList<Transform> Children => _children;

    /// <summary>设置父对象。传 null 表示脱离父对象成为根对象。</summary>
    public void SetParent(Transform? parent)
    {
        if (_parent == parent) return;
        // Remove from old parent
        if (_parent != null)
            _parent._children.Remove(this);
        // Add to new parent
        if (parent != null)
            parent._children.Add(this);
        _parent = parent;
        MarkDirty();
        GameObject?.MarkComponentsDirty();
        // Mark all descendants dirty too (their world position changed)
        MarkDescendantsDirty();
    }

    /// <summary>世界坐标位置（递归计算父链条）。</summary>
    public Vector3 WorldPosition =>
        _parent != null ? _parent.WorldPosition + _position : _position;

    /// <summary>世界坐标 X（递归累加父对象）。</summary>
    public float WorldX => WorldPosition.X;

    /// <summary>世界坐标 Y（递归累加父对象）。</summary>
    public float WorldY => WorldPosition.Y;

    /// <summary>世界坐标 Z / 深度（递归累加父对象）。</summary>
    public float WorldZ => WorldPosition.Z;

    /// <summary>位置（相对父对象）：X=水平(0左→1右)，Y=垂直(0顶→1底)，Z=深度(越大越靠前)。</summary>
    public Vector3 Position
    {
        get => _position;
        set
        {
            if (!_position.Equals(value))
            {
                _position = value;
                MarkDirty();
                GameObject?.MarkComponentsDirty();
                MarkDescendantsDirty();
            }
        }
    }

    /// <summary>水平位置（相对于父对象）：0=左侧，0.5=居中，1=右侧。</summary>
    public float X
    {
        get => _position.X;
        set => Position = new Vector3(value, _position.Y, _position.Z);
    }

    /// <summary>垂直位置（相对于父对象）：0=顶部，1=底部。</summary>
    public float Y
    {
        get => _position.Y;
        set => Position = new Vector3(_position.X, value, _position.Z);
    }

    /// <summary>不透明度：0=完全透明，1=完全不透明。</summary>
    public float Opacity
    {
        get => _opacity;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (!_opacity.Equals(clamped)) { _opacity = clamped; MarkDirty(); GameObject?.MarkComponentsDirty(); MarkDescendantsDirty(); }
        }
    }

    /// <summary>水平翻转。</summary>
    public bool FlipX
    {
        get => _flipX;
        set { if (_flipX != value) { _flipX = value; MarkDirty(); GameObject?.MarkComponentsDirty(); MarkDescendantsDirty(); } }
    }

    /// <summary>渲染排序。值越大越靠前显示。读写 Position.Z。</summary>
    public int SortingOrder
    {
        get => (int)_position.Z;
        set
        {
            if ((int)_position.Z != value)
                Position = new Vector3(_position.X, _position.Y, value);
        }
    }

    /// <summary>递归标记所有子孙为脏。</summary>
    private void MarkDescendantsDirty()
    {
        foreach (var child in _children)
        {
            child.MarkDirty();
            child.GameObject?.MarkComponentsDirty();
            child.MarkDescendantsDirty();
        }
    }
}
