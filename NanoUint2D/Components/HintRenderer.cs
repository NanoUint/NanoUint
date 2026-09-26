using NanoUint.Drawing;

namespace NanoUint;

/// <summary>Mouse-hover hint renderer. Shows floating hint text next to the cursor.</summary>
public sealed class HintRenderer : Component
{
    /// <summary>Whether hint display is enabled.</summary>
    public bool EnableHints { get; set; } = true;

    /// <summary>Hint text color (default emerald).</summary>
    public Color TextColor { get; set; } = new(0x3E, 0xBF, 0xBF);

    /// <summary>Hint background color (default semi-transparent black).</summary>
    public Color BackgroundColor { get; set; } = new(0x0A, 0x0A, 0x0A, 0xE0);

    public override string ToString() =>
        $"HintRenderer (enabled={EnableHints})";
}
