using System;
using System.Runtime.CompilerServices;

namespace NanoUint;

/// <summary>Canvas scaler that adapts to different resolutions and window sizes.</summary>
public sealed class CanvasScaler : Component
{
    /// <summary>Reference width (design resolution).</summary>
    public float ReferenceWidth { get; set; } = 1280f;

    /// <summary>Reference height (design resolution).</summary>
    public float ReferenceHeight { get; set; } = 720f;

    /// <summary>Scale mode; only ScaleWithScreenSize is supported.</summary>
    public CanvasScaleMode ScaleMode { get; set; } = CanvasScaleMode.ScaleWithScreenSize;

    /// <summary>Match factor: 0 = match width, 1 = match height, 0.5 = balanced (default).</summary>
    public float MatchWidthOrHeight { get; set; } = 0.5f;

    /// <summary>Computes the current scale factor for the given size.</summary>
    /// <param name="actualWidth">Current window/WPF Canvas width.</param>
    /// <param name="actualHeight">Current window/WPF Canvas height.</param>
    /// <returns>Unified scale factor applied to both X and Y to preserve aspect ratio.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ComputeScaleFactor(double actualWidth, double actualHeight)
    {
        if (ScaleMode == CanvasScaleMode.ConstantPixelSize)
            return 1f;

        if (ReferenceWidth <= 0 || ReferenceHeight <= 0)
            return 1f;

        float scaleX = (float)actualWidth / ReferenceWidth;
        float scaleY = (float)actualHeight / ReferenceHeight;

        // Interpolate between scaleX and scaleY by MatchWidthOrHeight for one uniform factor
        float t = Math.Clamp(MatchWidthOrHeight, 0f, 1f);
        float scale = scaleX + (scaleY - scaleX) * t;

        return Math.Max(scale, 0.01f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal (double refW, double refH) GetReferenceSize(double actualW, double actualH)
    {
        if (ScaleMode == CanvasScaleMode.ConstantPixelSize)
            return (actualW, actualH);

        float scale = ComputeScaleFactor(actualW, actualH);
        if (scale <= 0.01f) return (ReferenceWidth, ReferenceHeight);

        // Inverse-derive the reference size: actual / scale = reference
        return (actualW / scale, actualH / scale);
    }
}

/// <summary>Canvas scale modes.</summary>
public enum CanvasScaleMode
{
    /// <summary>Constant pixel size; UI does not scale with the window.</summary>
    ConstantPixelSize,

    /// <summary>Scale with screen size; lays out at the reference resolution.</summary>
    ScaleWithScreenSize,
}
