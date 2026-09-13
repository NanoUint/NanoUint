using System.Collections;
using NanoUint.Drawing;

namespace NanoUint;

/// <summary>Declarative tween component that drives smooth transitions of Transform properties.</summary>
public sealed class Tweener : Behaviour
{
    private readonly List<TweenStep> _steps = new();

    /// <summary>Callback invoked when the animation completes.</summary>
    public Action? OnComplete { get; set; }

    /// <summary>Whether the tween is currently playing.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Easing function (default OutExpo).</summary>
    public Func<float, float> Easing { get; set; } = Ease.OutExpo;

    #region Fluent builder API

    /// <summary>Transitions the X coordinate (normalized 0-1).</summary>
    public Tweener ToX(float target, float duration)
    {
        _steps.Add(new TweenStep(TweenProp.X, target, duration));
        return this;
    }

    /// <summary>Transitions the Y coordinate (normalized 0-1).</summary>
    public Tweener ToY(float target, float duration)
    {
        _steps.Add(new TweenStep(TweenProp.Y, target, duration));
        return this;
    }

    /// <summary>Transitions the opacity.</summary>
    public Tweener ToOpacity(float target, float duration)
    {
        _steps.Add(new TweenStep(TweenProp.Opacity, Math.Clamp(target, 0f, 1f), duration));
        return this;
    }

    /// <summary>Transitions the sorting order (rounded to an integer).</summary>
    public Tweener ToSortingOrder(int target, float duration)
    {
        _steps.Add(new TweenStep(TweenProp.SortingOrder, target, duration));
        return this;
    }

    #endregion

    #region Control

    /// <summary>Starts the animation.</summary>
    public void Play()
    {
        if (_steps.Count == 0) return;
        IsPlaying = true;
        StartCoroutine(RunTweens());
    }

    /// <summary>Stops the animation, keeping the current position.</summary>
    public void Stop()
    {
        IsPlaying = false;
        StopAllCoroutines();
    }

    #endregion

    #region Static shortcuts

    /// <summary>Fades in (opacity 0 -> 1).</summary>
    public static Tweener FadeIn(GameObject go, float duration = 0.5f, Action? onComplete = null)
    {
        go.Transform.Opacity = 0f;
        var tw = go.AddComponent<Tweener>();
        tw.ToOpacity(1f, duration);
        tw.OnComplete = onComplete;
        tw.Play();
        return tw;
    }

    /// <summary>Fades out (opacity 1 -> 0).</summary>
    public static Tweener FadeOut(GameObject go, float duration = 0.5f, Action? onComplete = null)
    {
        go.Transform.Opacity = 1f;
        var tw = go.AddComponent<Tweener>();
        tw.ToOpacity(0f, duration);
        tw.OnComplete = onComplete;
        tw.Play();
        return tw;
    }

    /// <summary>Moves to the given position.</summary>
    public static Tweener MoveTo(GameObject go, float x, float y, float duration = 0.5f, Action? onComplete = null)
    {
        var tw = go.AddComponent<Tweener>();
        tw.ToX(x, duration).ToY(y, duration);
        tw.OnComplete = onComplete;
        tw.Play();
        return tw;
    }

    #endregion

    #region Internal

    private IEnumerator RunTweens()
    {
        var transform = GameObject?.Transform;
        if (transform == null || _steps.Count == 0) { IsPlaying = false; yield break; }

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

            // Enter skips the animation
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

        // Snap the final values exactly to the targets
        for (int i = 0; i < _steps.Count; i++)
            _steps[i].SetValue(transform, _steps[i].Target);

        IsPlaying = false;
        OnComplete?.Invoke();
    }

    #endregion

    #region Data structures

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
