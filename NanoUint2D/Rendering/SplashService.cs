using System.Collections;
using NanoUint.Drawing;

namespace NanoUint;

/// <summary>Splash screen provider. Implement this to customize the game's startup animation sequence.</summary>
public interface ISplashProvider
{
    /// <summary>Plays the splash. Returns a coroutine the engine schedules; calls onComplete when done.</summary>
    IEnumerator PlaySplash(Scene scene, Action onComplete);
}

/// <summary>Splash service. Invoked automatically at engine startup; games inject a custom splash via Provider.</summary>
public static class SplashService
{
    /// <summary>Custom splash provider. When null, the engine's default animation is used.</summary>
    public static ISplashProvider? Provider { get; set; }

    /// <summary>Runs the splash sequence. Called by the engine after the window is ready and before OnStart.</summary>
    public static IEnumerator RunSplash(Scene scene, Action onComplete)
    {
        if (Provider != null)
        {
            yield return new WaitForCoroutine(Provider.PlaySplash(scene, onComplete));
            yield break;
        }

        #region Engine Default Splash: single logo fade in → hold → fade out
        yield return new WaitForCoroutine(DefaultSplash(scene, onComplete));
        #endregion
    }

    private static IEnumerator DefaultSplash(Scene scene, Action onComplete)
    {
        var go = scene.AddObject("__EngineSplash");
        var sr = go.AddComponent<SpriteRenderer>();
        var logoSprite = AssetDatabase.Load<Sprite>("Logo.jpg");
        sr.Sprite = logoSprite;
        go.Transform.X = 0.5f;
        go.Transform.Y = 0.5f;
        go.Transform.SortingOrder = 999;
        go.Transform.Opacity = 0f;

        if (sr.Sprite == null)
        {
            go.Destroy();
            onComplete();
            yield break;
        }

        yield return new WaitForCoroutine(FadeTo(go, 1f, 1.5f));
        yield return new WaitForSeconds(2f);
        yield return new WaitForCoroutine(FadeTo(go, 0f, 1f));

        go.Destroy();
        onComplete();
    }

    /// <summary>Per-frame opacity fade; decorative only and does not respond to Enter to skip.</summary>
    public static IEnumerator FadeTo(GameObject go, float target, float duration)
    {
        var t = go.Transform;
        float start = t.Opacity;
        int totalSteps = (int)(duration * 60f);
        for (int step = 1; step <= totalSteps; step++)
        {
            yield return WaitForEndOfFrame.Instance;
            float progress = (float)step / totalSteps;
            t.Opacity = Ease.Lerp(start, target, progress, Ease.OutExpo);
        }
        t.Opacity = target;
    }
}
