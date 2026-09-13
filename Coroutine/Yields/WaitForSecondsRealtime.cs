namespace NanoUint;

/// <summary>Waits the given seconds, unaffected by time scaling.</summary>
public sealed class WaitForSecondsRealtime
{
    public float Duration { get; }
    public WaitForSecondsRealtime(float seconds) => Duration = seconds;
}
