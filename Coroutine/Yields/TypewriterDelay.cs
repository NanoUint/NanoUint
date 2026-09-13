namespace NanoUint;

/// <summary>Typewriter delay. Waits the given seconds, but returns immediately when Enter is pressed.</summary>
public sealed class TypewriterDelay
{
    public float Duration { get; }
    public TypewriterDelay(float seconds) => Duration = seconds;
}
