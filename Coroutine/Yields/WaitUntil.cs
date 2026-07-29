namespace NanoUint;

public sealed class WaitUntil
{
    public Func<bool> Predicate { get; }
    public WaitUntil(Func<bool> predicate) => Predicate = predicate;
}
