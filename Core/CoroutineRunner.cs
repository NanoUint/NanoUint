using System.Collections;

namespace NanoUint;

/// <summary>极简协程宿主。挂载到 GameObject 上即可运行任意 IEnumerator。</summary>
public sealed class CoroutineRunner : Behaviour
{
    /// <summary>启动协程并在完成后自动销毁宿主 GameObject。</summary>
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
