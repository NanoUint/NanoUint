namespace NanoUint;

/// <summary>Waits the given seconds or until Enter is pressed.</summary>
public sealed class WaitOrClick
{
    public float Duration { get; }
    public WaitOrClick(float seconds) => Duration = seconds;
}
