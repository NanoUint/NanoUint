using System.Collections;
using NanoUint.Drawing;

namespace NanoUint;

/// <summary>声明式 Tween 动画组件。驱动 Transform 属性的平滑过渡。</summary>
public sealed class Tweener : Behaviour
{
    private readonly List<TweenStep> _steps = new();

    /// <summary>动画完成回调。</summary>
    public Action? OnComplete { get; set; }

    /// <summary>是否正在播放。</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>缓动函数（默认 OutExpo）。</summary>
    public Func<float, float> Easing { get; set; } = Ease.OutExpo;

    #region 链式构建 API

    /// <summary>X 坐标过渡（归一化 0~1）。</summary>
    public Tweener ToX(float target, float duration)
    {
        _steps.Add(new TweenStep(TweenProp.X, target, duration));
        return this;
    }

    /// <summary>Y 坐标过渡（归一化 0~1）。</summary>
    public Tweener ToY(float target, float duration)
    {
        _steps.Add(new TweenStep(TweenProp.Y, target, duration));
        return this;
    }

    /// <summary>Opacity 透明度过渡。</summary>
    public Tweener ToOpacity(float target, float duration)
    {
        _steps.Add(new TweenStep(TweenProp.Opacity, Math.Clamp(target, 0f, 1f), duration));
        return this;
    }

    /// <summary>SortingOrder 过渡（取整数）。</summary>
    public Tweener ToSortingOrder(int target, float duration)
    {
        _steps.Add(new TweenStep(TweenProp.SortingOrder, target, duration));
        return this;
    }

    #endregion

    #region 控制

    /// <summary>启动动画。每步并行播放（全部同时开始）。</summary>
    public void Play()
    {
        if (_steps.Count == 0) return;
        IsPlaying = true;
        StartCoroutine(RunTweens());
    }

    /// <summary>停止动画（保留当前位置）。</summary>
    public void Stop()
    {
        IsPlaying = false;
        StopAllCoroutines();
    }

    #endregion

    #region 批量快捷方式

    /// <summary>淡入（Opacity 0→1）。</summary>
    public static Tweener FadeIn(GameObject go, float duration = 0.5f, Action? onComplete = null)
    {
        go.Transform.Opacity = 0f;
        var tw = go.AddComponent<Tweener>();
        tw.ToOpacity(1f, duration);
        tw.OnComplete = onComplete;
        tw.Play();
        return tw;
    }

    /// <summary>淡出（Opacity 1→0）。</summary>
    public static Tweener FadeOut(GameObject go, float duration = 0.5f, Action? onComplete = null)
    {
        go.Transform.Opacity = 1f;
        var tw = go.AddComponent<Tweener>();
        tw.ToOpacity(0f, duration);
        tw.OnComplete = onComplete;
        tw.Play();
        return tw;
    }

    /// <summary>移动到指定位置。</summary>
    public static Tweener MoveTo(GameObject go, float x, float y, float duration = 0.5f, Action? onComplete = null)
    {
        var tw = go.AddComponent<Tweener>();
        tw.ToX(x, duration).ToY(y, duration);
        tw.OnComplete = onComplete;
        tw.Play();
        return tw;
    }

    #endregion

    #region 内部

    private IEnumerator RunTweens()
    {
        var transform = GameObject?.Transform;
        if (transform == null || _steps.Count == 0) { IsPlaying = false; yield break; }

        // 记录起始值
        var starts = new float[_steps.Count];
        for (int i = 0; i < _steps.Count; i++)
            starts[i] = _steps[i].GetValue(transform);

        float maxDuration = 0f;
        for (int i = 0; i < _steps.Count; i++)
            if (_steps[i].Duration > maxDuration) maxDuration = _steps[i].Duration;

        int totalSteps = Math.Max(1, (int)(maxDuration * 60f));
        for (int step = 1; step <= totalSteps; step++)
        {
            if (IsDestroyed || transform.IsDestroyed) { IsPlaying = false; yield break; }

            // Enter 跳过
            if (InputManager.IsAdvancePressedThisFrame())
            {
                InputManager.ConsumeAdvancePress();
                break;
            }

            yield return WaitForEndOfFrame.Instance;

            float globalT = (float)step / totalSteps;
            for (int i = 0; i < _steps.Count; i++)
            {
                float localT = Math.Clamp(globalT / (_steps[i].Duration / maxDuration), 0f, 1f);
                float easedT = Easing(localT);
                float current = Ease.Lerp(starts[i], _steps[i].Target, easedT, Ease.Linear);
                _steps[i].SetValue(transform, current);
            }
        }

        // 确保最终值精确为目标值
        for (int i = 0; i < _steps.Count; i++)
            _steps[i].SetValue(transform, _steps[i].Target);

        IsPlaying = false;
        OnComplete?.Invoke();
    }

    #endregion

    #region 数据结构

    private enum TweenProp { X, Y, Opacity, SortingOrder }

    private sealed class TweenStep
    {
        public TweenProp Prop { get; }
        public float Target { get; }
        public float Duration { get; }

        public TweenStep(TweenProp prop, float target, float duration)
        {
            Prop = prop;
            Target = target;
            Duration = Math.Max(0.01f, duration);
        }

        public float GetValue(Transform t) => Prop switch
        {
            TweenProp.X => t.X,
            TweenProp.Y => t.Y,
            TweenProp.Opacity => t.Opacity,
            TweenProp.SortingOrder => t.SortingOrder,
            _ => 0f,
        };

        public void SetValue(Transform t, float value)
        {
            switch (Prop)
            {
                case TweenProp.X: t.X = value; break;
                case TweenProp.Y: t.Y = value; break;
                case TweenProp.Opacity: t.Opacity = value; break;
                case TweenProp.SortingOrder: t.SortingOrder = (int)Math.Round(value); break;
            }
        }
    }
    #endregion
}
