using System.Collections;

namespace NanoUint;

/// <summary>
/// 带协程支持的组件基类。类似 UnityEngine.MonoBehaviour。
/// 游戏逻辑脚本应继承此类。
/// </summary>
public abstract class Behaviour : Component
{
    private readonly List<Coroutine> _coroutines = new();

    /// <summary>启动一个协程。</summary>
    protected Coroutine StartCoroutine(IEnumerator routine)
    {
        var coroutine = CoroutineScheduler.Instance.Start(routine, this);
        _coroutines.Add(coroutine);
        return coroutine;
    }

    /// <summary>停止一个协程。</summary>
    protected void StopCoroutine(Coroutine coroutine)
    {
        CoroutineScheduler.Instance.Stop(coroutine);
        _coroutines.Remove(coroutine);
    }

    /// <summary>停止此 Behaviour 上的所有协程。</summary>
    protected void StopAllCoroutines()
    {
        foreach (var c in _coroutines.ToList())
            CoroutineScheduler.Instance.Stop(c);
        _coroutines.Clear();
    }

    protected internal override void OnDestroy()
    {
        StopAllCoroutines();
        base.OnDestroy();
    }
}
