using NanoUint.Drawing;

namespace NanoUint;

/// <summary>全屏纯色遮罩。供过渡动画（闪黑/闪白）使用。</summary>
public sealed class FlashOverlay : Component
{
    private Color _color = Color.Black;

    /// <summary>遮罩颜色。Alpha 通道由 Transform.Opacity 控制。</summary>
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
