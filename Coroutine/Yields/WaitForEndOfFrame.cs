namespace NanoUint;

/// <summary>等待一帧（到下一帧结束）。用于逐帧动画循环。</summary>
public sealed class WaitForEndOfFrame
{
    public static readonly WaitForEndOfFrame Instance = new();
    private WaitForEndOfFrame() { }
}
