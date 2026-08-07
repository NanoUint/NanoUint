using System;
using System.Runtime.CompilerServices;

namespace NanoUint;

/// <summary>缓动函数库。所有函数接收归一化时间 t ∈ [0,1]，返回缓动后的值 ∈ [0,1]。</summary>
public static class Ease
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Clamp01(float t) => t <= 0f ? 0f : t >= 1f ? 1f : t;

    /// <summary>线性（无缓动）。</summary>
    public static float Linear(float t) => Clamp01(t);

    #region Quad

    public static float InQuad(float t) { t = Clamp01(t); return t * t; }
    public static float OutQuad(float t) { t = Clamp01(t); return 1f - (1f - t) * (1f - t); }
    public static float InOutQuad(float t)
    {
        t = Clamp01(t);
        return t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f;
    }

    #endregion

    #region Cubic

    public static float InCubic(float t) { t = Clamp01(t); return t * t * t; }
    public static float OutCubic(float t) { t = Clamp01(t); return 1f - MathF.Pow(1f - t, 3f); }
    public static float InOutCubic(float t)
    {
        t = Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
    }

    #endregion

    #region Expo

    public static float InExpo(float t) { t = Clamp01(t); return t <= 0f ? 0f : MathF.Pow(2f, 10f * t - 10f); }
    public static float OutExpo(float t) { t = Clamp01(t); return t >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * t); }
    public static float InOutExpo(float t)
    {
        t = Clamp01(t);
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return t < 0.5f ? MathF.Pow(2f, 20f * t - 10f) / 2f
                         : (2f - MathF.Pow(2f, -20f * t + 10f)) / 2f;
    }

    #endregion

    #region Sine

    public static float InSine(float t) { t = Clamp01(t); return 1f - MathF.Cos(t * MathF.PI / 2f); }
    public static float OutSine(float t) { t = Clamp01(t); return MathF.Sin(t * MathF.PI / 2f); }
    public static float InOutSine(float t) { t = Clamp01(t); return -(MathF.Cos(MathF.PI * t) - 1f) / 2f; }

    #endregion

    #region Back (overshoot)

    public static float InBack(float t) { t = Clamp01(t); const float c1 = 1.70158f; const float c3 = c1 + 1f; return c3 * t * t * t - c1 * t * t; }
    public static float OutBack(float t)
    {
        t = Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
    }
    public static float InOutBack(float t)
    {
        t = Clamp01(t);
        const float c1 = 1.70158f;
        const float c2 = c1 * 1.525f;
        return t < 0.5f
            ? MathF.Pow(2f * t, 2f) * ((c2 + 1f) * 2f * t - c2) / 2f
            : (MathF.Pow(2f * t - 2f, 2f) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) / 2f;
    }

    #endregion

    #region 便捷方法：区间映射

    /// <summary>从 from 缓动到 to，返回缓动后的中间值。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float from, float to, float t, Func<float, float> easing)
    {
        float eased = easing(t);
        return from + (to - from) * eased;
    }
    #endregion
}
