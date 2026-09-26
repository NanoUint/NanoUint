namespace NanoUint;

public sealed class WaitForSeconds
{
    public float Duration { get; }
    public WaitForSeconds(float seconds) => Duration = seconds;
}
