namespace NanoUint;

/// <summary>Waits one frame (until the end of the next frame).</summary>
public sealed class WaitForEndOfFrame
{
    public static readonly WaitForEndOfFrame Instance = new();
    private WaitForEndOfFrame() { }
}
