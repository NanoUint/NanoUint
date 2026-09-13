using NanoUint.Diagnostics;

namespace NanoUint;

/// <summary>Engine entry point that starts and shuts down the game.</summary>
public static class Application
{
    private static Rendering.WpfEngineHost? _host;

    /// <summary>Default engine context. Set during Run(); null before startup.</summary>
    public static EngineContext? Default { get; private set; }

    /// <summary>Starts the engine and runs the game.</summary>
    public static void Run(IGameBootstrapper bootstrapper)
    {
        Default = new EngineContext();
        _host = new Rendering.WpfEngineHost(bootstrapper);
        _host.Run();
    }

    /// <summary>Shuts down the engine.</summary>
    public static void Quit()
    {
        _host?.Shutdown();
    }

    /// <summary>Runs action on the next frame.</summary>
    /// <remarks>Exceptions are caught and logged so a throwing callback cannot crash the whole app via the Dispatcher.</remarks>
    public static void Defer(Action action)
    {
        if (action == null) return;
        try
        {
            var win = _host?.GetWindow();
            if (win != null)
                win.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.ContextIdle,
                    () => SafeInvoke(action));
            else
                SafeInvoke(action);
        }
        catch (Exception ex)
        {
            Logger.Error("Application", $"Defer failed: {ex.Message}", ex);
        }
    }

    private static void SafeInvoke(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logger.Error("Application", $"Deferred action failed: {ex.Message}", ex);
        }
    }

    internal static Rendering.WpfEngineHost? Host => _host;

    /// <summary>Captures a 320x180 JPEG Base64 thumbnail of the current frame.</summary>
    public static string? CaptureThumbnail() => _host?.CaptureThumbnail();

    #region Screen shake

    /// <summary>Screen shake X offset in pixels.</summary>
    public static float ShakeOffsetX { get; set; }

    /// <summary>Screen shake Y offset in pixels.</summary>
    public static float ShakeOffsetY { get; set; }
    #endregion
}
