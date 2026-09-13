using NanoUint.Drawing;

namespace NanoUint;

/// <summary>Positioning component every GameObject is guaranteed to have.</summary>
public class Transform : Component
{
    private Vector3 _position;
    private float _opacity = 1f;
    private bool _flipX;
    private int _sortingLayer;
    private int _orderInLayer;
    private Transform? _parent;
    private readonly List<Transform> _children = new();

    /// <summary>Parent Transform; null means a root object.</summary>
    public Transform? Parent => _parent;

    /// <summary>All direct children Transforms.</summary>
    public IReadOnlyList<Transform> Children => _children;

    /// <summary>Sets the parent; passing null detaches the object into a root.</summary>
    public void SetParent(Transform? parent)
    {
        if (_parent == parent) return;
        if (_parent != null)
            _parent._children.Remove(this);
        if (parent != null)
            parent._children.Add(this);
        _parent = parent;
        MarkDirty();
        GameObject?.MarkComponentsDirty();
        // Mark all descendants dirty too (their world position changed)
        MarkDescendantsDirty();
    }

    /// <summary>World position.</summary>
    public Vector3 WorldPosition =>
        _parent != null ? _parent.WorldPosition + _position : _position;

    /// <summary>World X.</summary>
    public float WorldX => WorldPosition.X;

    /// <summary>World Y.</summary>
    public float WorldY => WorldPosition.Y;

    /// <summary>World Z / depth.</summary>
    public float WorldZ => WorldPosition.Z;

    /// <summary>Position relative to the parent (0~1 normalized; Z is depth).</summary>
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

    /// <summary>Horizontal position relative to the parent.</summary>
    public float X
    {
        get => _position.X;
        set => Position = new Vector3(value, _position.Y, _position.Z);
    }

    /// <summary>Vertical position relative to the parent.</summary>
    public float Y
    {
        get => _position.Y;
        set => Position = new Vector3(_position.X, value, _position.Z);
    }

    /// <summary>Opacity: 0=fully transparent, 1=fully opaque.</summary>
    public float Opacity
    {
        get => _opacity;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (!_opacity.Equals(clamped)) { _opacity = clamped; MarkDirty(); GameObject?.MarkComponentsDirty(); MarkDescendantsDirty(); }
        }
    }

    /// <summary>Horizontal flip.</summary>
    public bool FlipX
    {
        get => _flipX;
        set { if (_flipX != value) { _flipX = value; MarkDirty(); GameObject?.MarkComponentsDirty(); MarkDescendantsDirty(); } }
    }

    /// <summary>Render sort order; larger values draw in front.</summary>
    public int SortingOrder
    {
        get => (int)_position.Z;
        set
        {
            if ((int)_position.Z != value)
                Position = new Vector3(_position.X, _position.Y, value);
        }
    }

    /// <summary>Sorting layer (lower renders first). Default 0.</summary>
    public int SortingLayer
    {
        get => _sortingLayer;
        set { if (_sortingLayer != value) { _sortingLayer = value; MarkDirty(); GameObject?.MarkComponentsDirty(); } }
    }

    /// <summary>Order within the sorting layer (higher renders in front). Default 0.</summary>
    public int OrderInLayer
    {
        get => _orderInLayer;
        set { if (_orderInLayer != value) { _orderInLayer = value; MarkDirty(); GameObject?.MarkComponentsDirty(); } }
    }

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
