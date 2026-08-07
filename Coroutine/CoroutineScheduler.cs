using System.Collections;
using System.Collections.Generic;

namespace NanoUint;

/// <summary>协程调度器。在引擎主循环中驱动 IEnumerator，支持嵌套协程。</summary>
public sealed class CoroutineScheduler
{
    public static CoroutineScheduler Instance { get; } = new();

    private readonly List<CoroutineState> _active = new();

    private CoroutineScheduler() { }

    /// <summary>启动一个协程（公开 API，任意组件可用）。</summary>
    public Coroutine Start(IEnumerator routine, Component owner)
    {
        var coroutine = new Coroutine();
        var state = new CoroutineState(coroutine, routine, owner);
        _active.Add(state);

        // 立即执行第一步（处理 Awake/Start 中的协程）
        if (!state.MoveNext(0f))
            _active.Remove(state);

        return coroutine;
    }

    /// <summary>停止一个协程。</summary>
    public void Stop(Coroutine coroutine)
    {
        _active.RemoveAll(s => s.Coroutine == coroutine);
        coroutine.MarkStopped();
    }

    /// <summary>每帧递增的计数器，用于 WaitForEndOfFrame 的同帧去重。</summary>
    internal int FrameNumber { get; private set; }

    /// <summary>每帧由 Scene 调用，推进所有活跃协程。</summary>
    internal void Tick(float deltaTime)
    {
        FrameNumber++;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var state = _active[i];
            if (!state.MoveNext(deltaTime))
                _active.RemoveAt(i);
        }
    }

    private sealed class CoroutineState
    {
        public Coroutine Coroutine { get; }
        public Component Owner { get; }
        private readonly IEnumerator _routine;
        private object? _currentYield;
        private float _timer;

        // 嵌套协程支持（用栈支持任意深度）
        private Stack<IEnumerator>? _nestedStack;
        private object? _nestedYield;
        private float _nestedTimer;

        /// <summary>上一次满足 WaitForEndOfFrame 的帧号，确保同帧只消费一次。</summary>
        private int _lastEndOfFrameTick;

        public CoroutineState(Coroutine coroutine, IEnumerator routine, Component owner)
        {
            Coroutine = coroutine;
            _routine = routine;
            Owner = owner;
        }

        [ThreadStatic]
        private static int _moveNextDepth;

        /// <summary>推进协程一步。返回 false 表示协程结束。</summary>
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

                // 如果有活跃的嵌套协程栈，先推进最内层
                if (_nestedStack != null && _nestedStack.Count > 0)
                {
                    if (!AdvanceNested(deltaTime))
                        return true; // 内层还在等

                    // 内层完成，弹出
                    _nestedStack.Pop();
                    _nestedYield = null;
                    _nestedTimer = 0f;

                    if (_nestedStack.Count == 0)
                    {
                        _nestedStack = null;
                        _currentYield = null; // 最外层 WaitForCoroutine 完成
                    }
                    return true;
                }

                // 如果当前在等待条件，检查是否满足
                if (_currentYield != null)
                {
                    if (!TrySatisfyYield(_currentYield, ref _timer, deltaTime))
                        return true; // 继续等待

                    _currentYield = null;
                    _timer = 0f;
                }

                // 推进 IEnumerator
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

                // 处理 yield return 值
                var yieldValue = _routine.Current;

                // 嵌套协程：支持 WaitForCoroutine / ICoroutineNestable / 原始 IEnumerator
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
                    // 立即推进嵌套协程第一步
                    if (!AdvanceNested(deltaTime))
                        return true; // 嵌套协程在等待
                    _nestedStack.Pop();
                    _nestedStack = null;
                    _nestedYield = null;
                    _nestedTimer = 0f;
                    return true;
                }

                _currentYield = yieldValue;
                _timer = 0f;

                // 如果新 yield 立即可满足（如 WaitForSeconds(0)），同帧继续推进
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

        /// <summary>推进嵌套协程一步（使用栈顶）。返回 false 表示还在等待。</summary>
        private bool AdvanceNested(float deltaTime)
        {
            if (_nestedStack == null || _nestedStack.Count == 0) return true;

            var current = _nestedStack.Peek();

            // 处理当前嵌套协程的 yield
            if (_nestedYield != null)
            {
                if (!TrySatisfyYield(_nestedYield, ref _nestedTimer, deltaTime))
                    return false; // 还在等

                _nestedYield = null;
                _nestedTimer = 0f;
            }

            // 推进当前嵌套 IEnumerator
            bool hasNext;
            try
            {
                hasNext = current.MoveNext();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Coroutine] Exception in nested coroutine on {Owner}: {ex}");
                return true; // 异常 → 结束当前嵌套协程
            }

            if (!hasNext) return true; // 当前嵌套协程完成

            _nestedYield = current.Current;
            _nestedTimer = 0f;

            // 如果嵌套协程内部又 yield 了子协程，压入更深一层
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

            // 检查是否立即可满足
            if (TrySatisfyYield(_nestedYield, ref _nestedTimer, deltaTime))
            {
                _nestedYield = null;
                _nestedTimer = 0f;
                return AdvanceNested(deltaTime); // 继续推进
            }

            return false; // 嵌套协程在等待
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
                    // Enter 按下时立即返回（不消费）
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
                    if (InputManager.IsAdvancePressedThisFrame())
                    {
                        InputManager.ConsumeAdvancePress();
                        return true;
                    }
                    return false;
                case WaitForChoice wfc:
                    return wfc.ChoiceGroup.HasResult;
                case WaitWhile ww:
                    return !ww.Predicate();
                case WaitUntil wu:
                    return wu.Predicate();
                default:
                    return true; // 未知 yield → 立即通过
            }
        }

        private static bool SatisfyWaitForSeconds(WaitForSeconds wfs, ref float timer, float deltaTime)
        {
            timer += deltaTime;
            return timer >= wfs.Duration;
        }
    }
}

/// <summary>协程句柄。</summary>
public sealed class Coroutine
{
    public bool IsStopped { get; private set; }
    internal void MarkStopped() => IsStopped = true;
}
