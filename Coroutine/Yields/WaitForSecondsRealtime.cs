namespace NanoUint;

/// <summary>等待指定秒数（不受时间缩放影响）。</summary>
public sealed class WaitForSecondsRealtime
{
    public float Duration { get; }
    public WaitForSecondsRealtime(float seconds) => Duration = seconds;
}
