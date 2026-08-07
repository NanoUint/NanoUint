namespace NanoUint;

/// <summary>打字机延迟。等待指定秒数，Enter 按下时立即返回。</summary>
public sealed class TypewriterDelay
{
    public float Duration { get; }
    public TypewriterDelay(float seconds) => Duration = seconds;
}
