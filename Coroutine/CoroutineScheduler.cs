using System.Collections;

namespace NanoUint;

/// <summary>
/// 协程调度器。类似 Unity 的协程系统，在引擎主循环中驱动 IEnumerator。
/// 单例模式，由 Scene.Update 每帧调用 Tick()。
/// 支持嵌套协程（WaitForCoroutine）。
/// </summary>
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

    /// <summary>每帧由 Scene 调用，推进所有活跃协程。</summary>
    internal void Tick(float deltaTime)
    {
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

        // 嵌套协程支持
        private IEnumerator? _nestedRoutine;
        private object? _nestedYield;
        private float _nestedTimer;

        public CoroutineState(Coroutine coroutine, IEnumerator routine, Component owner)
        {
            Coroutine = coroutine;
            _routine = routine;
            Owner = owner;
        }

        /// <summary>推进协程一步。返回 false 表示协程结束。</summary>
        public bool MoveNext(float deltaTime)
        {
            if (Coroutine.IsStopped) return false;

            // 如果有活跃的嵌套协程，先推进它
            if (_nestedRoutine != null)
            {
                if (!AdvanceNested(deltaTime))
                    return true; // 嵌套协程还在等
                // 嵌套协程完成，清理
                _nestedRoutine = null;
                _nestedYield = null;
                _nestedTimer = 0f;
                _currentYield = null; // WaitForCoroutine 完成
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

            // 嵌套协程
            if (yieldValue is WaitForCoroutine wfc)
            {
                _nestedRoutine = wfc.Routine;
                _nestedYield = null;
                _nestedTimer = 0f;
                // 立即推进嵌套协程第一步
                if (!AdvanceNested(deltaTime))
                    return true; // 嵌套协程在等待
                _nestedRoutine = null;
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

        /// <summary>推进嵌套协程一步。返回 false 表示还在等待。</summary>
        private bool AdvanceNested(float deltaTime)
        {
            if (_nestedRoutine == null) return true;

            // 处理嵌套协程的当前 yield
            if (_nestedYield != null)
            {
                if (!TrySatisfyYield(_nestedYield, ref _nestedTimer, deltaTime))
                    return false; // 还在等

                _nestedYield = null;
                _nestedTimer = 0f;
            }

            // 推进嵌套 IEnumerator
            bool hasNext;
            try
            {
                hasNext = _nestedRoutine.MoveNext();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Coroutine] Exception in nested coroutine on {Owner}: {ex}");
                return true; // 异常 → 结束嵌套协程
            }

            if (!hasNext) return true; // 嵌套协程完成

            _nestedYield = _nestedRoutine.Current;
            _nestedTimer = 0f;

            // 检查是否立即可满足
            if (TrySatisfyYield(_nestedYield, ref _nestedTimer, deltaTime))
            {
                _nestedYield = null;
                _nestedTimer = 0f;
                return AdvanceNested(deltaTime); // 继续推进
            }

            return false; // 嵌套协程在等待
        }

        private static bool TrySatisfyYield(object? yieldValue, ref float timer, float deltaTime)
        {
            switch (yieldValue)
            {
                case TypewriterDelay td:
                    timer += deltaTime;
                    if (timer >= td.Duration) return true;
                    // Enter 按下时立即返回，但不消费 —— 让 TypewriterShow 循环头检查并消费
                    if (InputManager.IsAdvancePressedThisFrame())
                        return true;
                    return false;
                case WaitForSeconds wfs:
                    return SatisfyWaitForSeconds(wfs, ref timer, deltaTime);
                case WaitOrClick woc:
                    timer += deltaTime;
                    if (timer >= woc.Duration) return true;
                    if (InputManager.IsAdvancePressedThisFrame())
                        return true; // 不消费 — 同 Tick 内后续 yield 也能看到此帧输入
                    return false;
                case WaitForClick:
                    if (InputManager.IsAdvancePressedThisFrame())
                        return true; // 不消费 — 同 Tick 内后续 yield 也能看到此帧输入
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
