namespace NanoUint;

/// <summary>
/// 打字机延迟：等待指定秒数，但如果 Enter 被按下也立即返回。
/// 与 WaitOrClick 不同，它不消费 Enter —— 留给 TypewriterShow 循环头检查并消费。
/// </summary>
public sealed class TypewriterDelay
{
    public float Duration { get; }
    public TypewriterDelay(float seconds) => Duration = seconds;
}
