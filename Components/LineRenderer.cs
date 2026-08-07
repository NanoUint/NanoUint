using NanoUint.Drawing;

namespace NanoUint;

/// <summary>线条渲染器。在 2D 空间中绘制折线（坐标归一化 0~1）。</summary>
public sealed class LineRenderer : Component
{
    private readonly List<Vector2> _positions = new();
    private Color _color = Color.White;
    private float _width = 2f;
    private bool _loop;

    /// <summary>顶点数量。</summary>
    public int PositionCount => _positions.Count;

    /// <summary>线条颜色。</summary>
    public Color Color
    {
        get => _color;
        set { if (!_color.Equals(value)) { _color = value; MarkDirty(); } }
    }

    /// <summary>线条宽度（WPF 像素）。</summary>
    public float Width
    {
        get => _width;
        set { if (!_width.Equals(value)) { _width = value; MarkDirty(); } }
    }

    /// <summary>是否闭合首尾形成环（true 时渲染为 Polygon）。</summary>
    public bool Loop
    {
        get => _loop;
        set { if (_loop != value) { _loop = value; MarkDirty(); } }
    }

    /// <summary>所有顶点（只读）。</summary>
    public IReadOnlyList<Vector2> Positions => _positions;

    /// <summary>获取指定索引的顶点。</summary>
    public Vector2 GetPosition(int index) => _positions[index];

    /// <summary>设置指定索引的顶点位置。</summary>
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

    /// <summary>设置所有顶点（替换现有）。</summary>
    public void SetPositions(IEnumerable<Vector2> positions)
    {
        _positions.Clear();
        _positions.AddRange(positions);
        MarkDirty();
    }

    /// <summary>追加一个顶点。</summary>
    public void AddPosition(Vector2 position)
    {
        _positions.Add(position);
        MarkDirty();
    }

    /// <summary>清空所有顶点。</summary>
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
