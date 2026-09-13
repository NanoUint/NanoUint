using NanoUint.Drawing;

namespace NanoUint;

/// <summary>Full-screen solid-color overlay for transition effects.</summary>
public sealed class FlashOverlay : Component
{
    private Color _color = Color.Black;

    /// <summary>Overlay color.</summary>
    public Color Color
    {
        get => _color;
        set { if (!_color.Equals(value)) { _color = value; MarkDirty(); } }
    }

    protected internal override void Awake()
    {
        if (GameObject?.Transform != null)
        {
            GameObject.Transform.SortingOrder = 999;
            GameObject.Transform.X = 0;
            GameObject.Transform.Y = 0;
        }
    }
}
