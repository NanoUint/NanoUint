using NanoUint.Drawing;

namespace NanoUint;

/// <summary>Line renderer that draws a polyline in 2D space (coordinates normalized 0-1).</summary>
public sealed class LineRenderer : Component
{
    private readonly List<Vector2> _positions = new();
    private Color _color = Color.White;
    private float _width = 2f;
    private bool _loop;

    /// <summary>Number of vertices.</summary>
    public int PositionCount => _positions.Count;

    /// <summary>Line color.</summary>
    public Color Color
    {
        get => _color;
        set { if (!_color.Equals(value)) { _color = value; MarkDirty(); } }
    }

    /// <summary>Line width (WPF pixels).</summary>
    public float Width
    {
        get => _width;
        set { if (!_width.Equals(value)) { _width = value; MarkDirty(); } }
    }

    /// <summary>Whether the ends are joined into a loop.</summary>
    public bool Loop
    {
        get => _loop;
        set { if (_loop != value) { _loop = value; MarkDirty(); } }
    }

    /// <summary>All vertices (read-only).</summary>
    public IReadOnlyList<Vector2> Positions => _positions;

    /// <summary>Gets the vertex at the given index.</summary>
    public Vector2 GetPosition(int index) => _positions[index];

    /// <summary>Sets the position of the vertex at the given index.</summary>
    public void SetPosition(int index, Vector2 position)
    {
        const int maxPoints = 16384;
        if (index < 0 || index >= maxPoints)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"LineRenderer index must be 0–{maxPoints - 1}, got {index}.");
        while (index >= _positions.Count)
            _positions.Add(Vector2.Zero);
        if (!_positions[index].Equals(position))
        {
            _positions[index] = position;
            MarkDirty();
        }
    }

    /// <summary>Sets all vertices, replacing the existing ones.</summary>
    public void SetPositions(IEnumerable<Vector2> positions)
    {
        _positions.Clear();
        _positions.AddRange(positions);
        MarkDirty();
    }

    /// <summary>Appends a vertex.</summary>
    public void AddPosition(Vector2 position)
    {
        _positions.Add(position);
        MarkDirty();
    }

    /// <summary>Clears all vertices.</summary>
    public void Clear()
    {
        if (_positions.Count > 0)
        {
            _positions.Clear();
            MarkDirty();
        }
    }

    public override string ToString() =>
        $"LineRenderer (points={PositionCount}, width={Width:F1}, color={Color}, loop={Loop})";
}
