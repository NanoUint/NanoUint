using System.Collections;

namespace NanoUint;

/// <summary>标记类型可通过 ToNestedRoutine() 转换为嵌套协程。</summary>
public interface ICoroutineNestable
{
    IEnumerator ToNestedRoutine();
}
