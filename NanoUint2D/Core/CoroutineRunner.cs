using System.Collections;

namespace NanoUint;

/// <summary>Coroutine host that runs any IEnumerator.</summary>
public sealed class CoroutineRunner : Behaviour
{
    /// <summary>Starts a coroutine and destroys the host GameObject when it completes.</summary>
    public void Run(IEnumerator routine)
    {
        StartCoroutine(WrapAndDestroy(routine));
    }

    private System.Collections.IEnumerator WrapAndDestroy(IEnumerator routine)
    {
        yield return routine;
        GameObject?.Destroy();
    }
}
