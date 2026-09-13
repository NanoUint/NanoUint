using System.Collections;

namespace NanoUint;

/// <summary>Component base class with coroutine support.</summary>
public abstract class Behaviour : Component
{
    private readonly List<Coroutine> _coroutines = new();

    /// <summary>Starts a coroutine.</summary>
    protected Coroutine StartCoroutine(IEnumerator routine)
    {
        var coroutine = CoroutineScheduler.Instance.Start(routine, this);
        _coroutines.Add(coroutine);
        return coroutine;
    }

    /// <summary>Stops a coroutine.</summary>
    protected void StopCoroutine(Coroutine coroutine)
    {
        CoroutineScheduler.Instance.Stop(coroutine);
        _coroutines.Remove(coroutine);
    }

    /// <summary>Stops all coroutines running on this Behaviour.</summary>
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
