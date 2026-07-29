using System.Collections;

namespace NanoUint;

/// <summary>等待嵌套协程完成。用于 yield return 一个子协程。</summary>
public sealed class WaitForCoroutine
{
    public IEnumerator Routine { get; }
    public WaitForCoroutine(IEnumerator routine) => Routine = routine;
}
