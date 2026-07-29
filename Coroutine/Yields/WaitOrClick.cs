namespace NanoUint;

/// <summary>等待指定秒数，或按 Enter 跳过。时间到或按 Enter 都会继续（消费 Enter 事件）。</summary>
public sealed class WaitOrClick
{
    public float Duration { get; }
    public WaitOrClick(float seconds) => Duration = seconds;
}
