using NanoUint.Drawing;

namespace NanoUint;

/// <summary>鼠标悬停提示渲染器。在光标旁显示浮动提示文本。</summary>
public sealed class HintRenderer : Component
{
    /// <summary>是否启用提示显示。</summary>
    public bool EnableHints { get; set; } = true;

    /// <summary>提示文字颜色（默认翡翠青）。</summary>
    public Color TextColor { get; set; } = new(0x3E, 0xBF, 0xBF);

    /// <summary>提示背景颜色（默认半透明黑）。</summary>
    public Color BackgroundColor { get; set; } = new(0x0A, 0x0A, 0x0A, 0xE0);

    public override string ToString() =>
        $"HintRenderer (enabled={EnableHints})";
}
