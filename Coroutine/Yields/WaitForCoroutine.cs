using System.Collections;

namespace NanoUint;

/// <summary>Waits for a nested coroutine to finish.</summary>
public sealed class WaitForCoroutine
{
    public IEnumerator Routine { get; }
    public WaitForCoroutine(IEnumerator routine) => Routine = routine;
}
