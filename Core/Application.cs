namespace NanoUint;

/// <summary>
/// 引擎入口。类似 Unity 的 Application 类。
/// 封装所有 WPF 启动逻辑，对游戏端仅暴露 Run(IGameBootstrapper)。
/// </summary>
public static class Application
{
    private static Rendering.WpfEngineHost? _host;

    /// <summary>启动引擎并运行游戏。</summary>
    public static void Run(IGameBootstrapper bootstrapper)
    {
        _host = new Rendering.WpfEngineHost(bootstrapper);
        _host.Run();
    }

    /// <summary>退出引擎。</summary>
    public static void Quit()
    {
        _host?.Shutdown();
    }

    /// <summary>
    /// 延迟到下一帧执行 action。用于在 UI 事件回调中安全地切换场景。
    /// </summary>
    public static void Defer(Action action)
    {
        var win = _host?.GetWindow();
        if (win != null)
            win.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ContextIdle, action);
        else
            action();
    }

    /// <summary>获取引擎内部的 WPF 宿主（仅引擎内部使用）。</summary>
    internal static Rendering.WpfEngineHost? Host => _host;

    /// <summary>捕获当前画面缩略图（320x180 JPEG Base64）。存档时使用。</summary>
    public static string? CaptureThumbnail() => _host?.CaptureThumbnail();
}
