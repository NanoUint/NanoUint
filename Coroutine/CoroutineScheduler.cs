using System.Collections;
using System.Collections.Generic;

namespace NanoUint;

/// <summary>Runs IEnumerator coroutines and supports nesting.</summary>
public sealed class CoroutineScheduler
{
    public static CoroutineScheduler Instance { get; } = new();

    private readonly List<CoroutineState> _active = new();

    private CoroutineScheduler() { }

    /// <summary>Starts a coroutine.</summary>
    public Coroutine Start(IEnumerator routine, Component owner)
    {
        var coroutine = new Coroutine();
        var state = new CoroutineState(coroutine, routine, owner);
        _active.Add(state);

        // Run the first step immediately so coroutines started in Awake/Start do not stall a frame.
        if (!state.MoveNext(0f))
            _active.Remove(state);

        return coroutine;
    }

    /// <summary>Stops a coroutine.</summary>
    public void Stop(Coroutine coroutine)
    {
        _active.RemoveAll(s => s.Coroutine == coroutine);
        coroutine.MarkStopped();
    }

    internal int FrameNumber { get; private set; }

    private readonly List<CoroutineState> _tickBuffer = new();

    private bool _ticking;

    internal void Tick(float deltaTime)
    {
        FrameNumber++;

        // Tick iterates a snapshot, not _active: a coroutine may Start/Stop others inside MoveNext,
        // which would mutate the collection mid-iteration. The buffer is reused to avoid per-frame allocation.
        if (_ticking)
        {
            TickCore(new List<CoroutineState>(_active), deltaTime);
            return;
        }

        _ticking = true;
        try
        {
            _tickBuffer.Clear();
            _tickBuffer.AddRange(_active);
            TickCore(_tickBuffer, deltaTime);
        }
        finally
        {
            _ticking = false;
        }
    }

    private void TickCore(List<CoroutineState> snapshot, float deltaTime)
    {
        for (int i = snapshot.Count - 1; i >= 0; i--)
        {
            var state = snapshot[i];
            if (state.Coroutine.IsStopped) continue;
            if (!state.MoveNext(deltaTime))
                _active.Remove(state);
        }
    }

    private sealed class CoroutineState
    {
        public Coroutine Coroutine { get; }
        public Component Owner { get; }
        private readonly IEnumerator _routine;
        private object? _currentYield;
        private float _timer;

        // Nested coroutines are tracked on a stack so nesting depth is unbounded.
        private Stack<IEnumerator>? _nestedStack;
        private object? _nestedYield;
        private float _nestedTimer;

        private int _lastEndOfFrameTick;

        public CoroutineState(Coroutine coroutine, IEnumerator routine, Component owner)
        {
            Coroutine = coroutine;
            _routine = routine;
            Owner = owner;
        }

        [ThreadStatic]
        private static int _moveNextDepth;

        public bool MoveNext(float deltaTime)
        {
            if (_moveNextDepth > 50)
            {
                _moveNextDepth--;
                return true;
            }
            _moveNextDepth++;

            try
            {
                if (Coroutine.IsStopped) return false;

                if (_nestedStack != null && _nestedStack.Count > 0)
                {
                    if (!AdvanceNested(deltaTime))
                        return true;

                    _nestedStack.Pop();
                    _nestedYield = null;
                    _nestedTimer = 0f;

                    if (_nestedStack.Count == 0)
                    {
                        _nestedStack = null;
                        _currentYield = null;
                    }
                    return true;
                }

                if (_currentYield != null)
                {
                    if (!TrySatisfyYield(_currentYield, ref _timer, deltaTime))
                        return true;

                    _currentYield = null;
                    _timer = 0f;
                }

                bool hasNext;
                try
                {
                    hasNext = _routine.MoveNext();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Coroutine] Exception in coroutine on {Owner}: {ex}");
                    return false;
                }

                if (!hasNext) return false;

                var yieldValue = _routine.Current;

                IEnumerator? nestedRoutine = null;
                if (yieldValue is WaitForCoroutine wfc)
                    nestedRoutine = wfc.Routine;
                else if (yieldValue is ICoroutineNestable nestable)
                    nestedRoutine = nestable.ToNestedRoutine();
                else if (yieldValue is IEnumerator directEnum)
                    nestedRoutine = directEnum;

                if (nestedRoutine != null)
                {
                    _nestedStack = new Stack<IEnumerator>();
                    _nestedStack.Push(nestedRoutine);
                    _nestedYield = null;
                    _nestedTimer = 0f;
                    if (!AdvanceNested(deltaTime))
                        return true;
                    _nestedStack.Pop();
                    _nestedStack = null;
                    _nestedYield = null;
                    _nestedTimer = 0f;
                    return true;
                }

                _currentYield = yieldValue;
                _timer = 0f;

                if (TrySatisfyYield(_currentYield, ref _timer, deltaTime))
                {
                    _currentYield = null;
                    _timer = 0f;
                    return MoveNext(deltaTime);
                }

                return true;
            }
            finally
            {
                _moveNextDepth--;
            }
        }

        private bool AdvanceNested(float deltaTime)
        {
            if (_nestedStack == null || _nestedStack.Count == 0) return true;

            var current = _nestedStack.Peek();

            if (_nestedYield != null)
            {
                if (!TrySatisfyYield(_nestedYield, ref _nestedTimer, deltaTime))
                    return false;

                _nestedYield = null;
                _nestedTimer = 0f;
            }

            bool hasNext;
            try
            {
                hasNext = current.MoveNext();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Coroutine] Exception in nested coroutine on {Owner}: {ex}");
                return true;
            }

            if (!hasNext) return true;

            _nestedYield = current.Current;
            _nestedTimer = 0f;

            IEnumerator? deeperRoutine = null;
            if (_nestedYield is WaitForCoroutine deeperWfc)
                deeperRoutine = deeperWfc.Routine;
            else if (_nestedYield is ICoroutineNestable deeperNestable)
                deeperRoutine = deeperNestable.ToNestedRoutine();
            else if (_nestedYield is IEnumerator deeperEnum)
                deeperRoutine = deeperEnum;

            if (deeperRoutine != null)
            {
                _nestedStack.Push(deeperRoutine);
                _nestedYield = null;
                _nestedTimer = 0f;
                return AdvanceNested(deltaTime);
            }

            if (TrySatisfyYield(_nestedYield, ref _nestedTimer, deltaTime))
            {
                _nestedYield = null;
                _nestedTimer = 0f;
                return AdvanceNested(deltaTime);
            }

            return false;
        }

        private bool TrySatisfyYield(object? yieldValue, ref float timer, float deltaTime)
        {
            switch (yieldValue)
            {
                case WaitForEndOfFrame:
                    if (_lastEndOfFrameTick == CoroutineScheduler.Instance.FrameNumber)
                        return false;
                    _lastEndOfFrameTick = CoroutineScheduler.Instance.FrameNumber;
                    return true;
                case TypewriterDelay td:
                    timer += deltaTime;
                    if (timer >= td.Duration) return true;
                    // Enter returns immediately without consuming the press (unlike WaitOrClick).
                    if (InputManager.IsAdvancePressedThisFrame())
                        return true;
                    return false;
                case WaitForSeconds wfs:
                    return SatisfyWaitForSeconds(wfs, ref timer, deltaTime);
                case WaitForSecondsRealtime wfsr:
                    timer += deltaTime;
                    return timer >= wfsr.Duration;
                case WaitOrClick woc:
                    timer += deltaTime;
                    if (timer >= woc.Duration) return true;
                    if (InputManager.IsAdvancePressedThisFrame())
                    {
                        InputManager.ConsumeAdvancePress();
                        return true;
                    }
                    return false;
                case WaitForClick:
                    timer += deltaTime;
                    if (InputManager.IsAdvancePressedThisFrame())
                    {
                        InputManager.ConsumeAdvancePress();
                        return true;
                    }
                    // Auto mode: after the text is fully shown, advance automatically after AutoAdvanceDelay seconds.
                    if (SettingsManager.AutoAdvance && timer >= SettingsManager.AutoAdvanceDelay)
                        return true;
                    return false;
                case WaitForChoice wfc:
                    return wfc.ChoiceGroup.HasResult;
                case WaitWhile ww:
                    return !ww.Predicate();
                case WaitUntil wu:
                    return wu.Predicate();
                default:
                    return true;
            }
        }

        private static bool SatisfyWaitForSeconds(WaitForSeconds wfs, ref float timer, float deltaTime)
        {
            timer += deltaTime;
            return timer >= wfs.Duration;
        }
    }
}

/// <summary>Coroutine handle.</summary>
public sealed class Coroutine
{
    public bool IsStopped { get; private set; }
    internal void MarkStopped() => IsStopped = true;
}
