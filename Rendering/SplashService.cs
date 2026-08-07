using System.Collections;
using NanoUint.Drawing;

namespace NanoUint;

/// <summary>启动画面提供者。实现此接口以自定义游戏启动动画序列。</summary>
public interface ISplashProvider
{
    /// <summary>播放启动画面。返回协程，引擎自动调度。完成后调用 onComplete。</summary>
    IEnumerator PlaySplash(Scene scene, Action onComplete);
}

/// <summary>Splash 服务。引擎启动时自动调用，游戏通过 Provider 注入自定义启动画面。</summary>
public static class SplashService
{
    /// <summary>自定义 Splash 提供者。设为 null 则使用引擎默认动画。</summary>
    public static ISplashProvider? Provider { get; set; }

    /// <summary>运行 Splash 序列。由引擎在窗口就绪后、OnStart 前调用。</summary>
    public static IEnumerator RunSplash(Scene scene, Action onComplete)
    {
        if (Provider != null)
        {
            yield return new WaitForCoroutine(Provider.PlaySplash(scene, onComplete));
            yield break;
        }

        #region 引擎默认 Splash：单张 logo 图渐显→停留→渐隐
        yield return new WaitForCoroutine(DefaultSplash(scene, onComplete));
        #endregion
    }

    private static IEnumerator DefaultSplash(Scene scene, Action onComplete)
    {
        // 引擎默认 Splash：NanoUint Resources/Logo.jpg
        var go = scene.AddObject("__EngineSplash");
        var sr = go.AddComponent<SpriteRenderer>();
        // Sprite.Path 设为 Logo.jpg
        var logoSprite = AssetDatabase.Load<Sprite>("Logo.jpg");
        sr.Sprite = logoSprite;
        go.Transform.X = 0.5f;
        go.Transform.Y = 0.5f;
        go.Transform.SortingOrder = 999;
        go.Transform.Opacity = 0f;

        if (sr.Sprite == null)
        {
            // 无素材 → 跳过 splash
            go.Destroy();
            onComplete();
            yield break;
        }

        // 渐显 1.5s
        yield return new WaitForCoroutine(FadeTo(go, 1f, 1.5f));
        // 停留 2s
        yield return new WaitForSeconds(2f);
        // 渐隐 1s
        yield return new WaitForCoroutine(FadeTo(go, 0f, 1f));

        go.Destroy();
        onComplete();
    }

    /// <summary>逐帧透明度渐变（纯装饰，不响应 Enter 跳过）。</summary>
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
