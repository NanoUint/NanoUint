using System.Collections;

namespace NanoUint;

/// <summary>Marks a type that can be converted into a nested coroutine.</summary>
public interface ICoroutineNestable
{
    IEnumerator ToNestedRoutine();
}
