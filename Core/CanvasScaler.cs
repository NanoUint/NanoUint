using System;
using System.Runtime.CompilerServices;

namespace NanoUint;

/// <summary>Canvas 缩放器。自动适应不同分辨率/窗口大小。</summary>
public sealed class CanvasScaler : Component
{
    /// <summary>参考宽度（设计分辨率）。</summary>
    public float ReferenceWidth { get; set; } = 1280f;

    /// <summary>参考高度（设计分辨率）。</summary>
    public float ReferenceHeight { get; set; } = 720f;

    /// <summary>缩放模式。当前仅支持 ScaleWithScreenSize。</summary>
    public CanvasScaleMode ScaleMode { get; set; } = CanvasScaleMode.ScaleWithScreenSize;

    /// <summary>匹配系数：0 = 匹配宽度，1 = 匹配高度，0.5 = 均衡（默认）。</summary>
    public float MatchWidthOrHeight { get; set; } = 0.5f;

    /// <summary>计算当前缩放因子（由引擎内部调用）。</summary>
    /// <param name="actualWidth">当前窗口/WPF Canvas 实际宽度。</param>
    /// <param name="actualHeight">当前窗口/WPF Canvas 实际高度。</param>
    /// <returns>统一的缩放因子（同时应用于 X 和 Y，保持宽高比）。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ComputeScaleFactor(double actualWidth, double actualHeight)
    {
        if (ScaleMode == CanvasScaleMode.ConstantPixelSize)
            return 1f;

        if (ReferenceWidth <= 0 || ReferenceHeight <= 0)
            return 1f;

        float scaleX = (float)actualWidth / ReferenceWidth;
        float scaleY = (float)actualHeight / ReferenceHeight;

        // 在 scaleX 和 scaleY 之间按 MatchWidthOrHeight 插值，得到统一缩放因子
        float t = Math.Clamp(MatchWidthOrHeight, 0f, 1f);
        float scale = scaleX + (scaleY - scaleX) * t;

        return Math.Max(scale, 0.01f);
    }

    /// <summary>获取参考分辨率下的虚拟 Canvas 尺寸（用于布局计算）。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal (double refW, double refH) GetReferenceSize(double actualW, double actualH)
    {
        if (ScaleMode == CanvasScaleMode.ConstantPixelSize)
            return (actualW, actualH);

        float scale = ComputeScaleFactor(actualW, actualH);
        if (scale <= 0.01f) return (ReferenceWidth, ReferenceHeight);

        // 逆推参考尺寸：实际 / 缩放 = 参考
        return (actualW / scale, actualH / scale);
    }
}

/// <summary>Canvas 缩放模式。</summary>
public enum CanvasScaleMode
{
    /// <summary>固定像素：UI 元素按绝对像素定位，不随窗口大小缩放。</summary>
    ConstantPixelSize,

    /// <summary>随屏幕缩放：按参考分辨率布局，运行时等比缩放。</summary>
    ScaleWithScreenSize,
}
