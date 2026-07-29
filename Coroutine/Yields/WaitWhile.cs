namespace NanoUint;

public sealed class WaitWhile
{
    public Func<bool> Predicate { get; }
    public WaitWhile(Func<bool> predicate) => Predicate = predicate;
}
