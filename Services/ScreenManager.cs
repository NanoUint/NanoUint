namespace NanoUint;

/// <summary>
/// 画面管理器。控制分辨率、全屏、VSync 等。
/// 内部操作 WPF Window（通过 Application.Host）。
/// </summary>
public static class ScreenManager
{
    private static int _width = 1280;
    private static int _height = 720;
    private static bool _isFullscreen;
    private static bool _vsyncEnabled = true;
    private static bool _showFPS;

    public static int Width
    {
        get => _width;
        set { _width = Math.Max(800, value); ApplyResolution(); }
    }
    public static int Height
    {
        get => _height;
        set { _height = Math.Max(600, value); ApplyResolution(); }
    }
    public static bool IsFullscreen
    {
        get => _isFullscreen;
        set { _isFullscreen = value; ApplyFullscreen(); }
    }
    public static bool VSyncEnabled
    {
        get => _vsyncEnabled;
        set { _vsyncEnabled = value; }
    }
    public static bool ShowFPS
    {
        get => _showFPS;
        set { _showFPS = value; }
    }

    private static void ApplyResolution()
    {
        Application.Host?.SetResolution(_width, _height);
        Debug.Log($"ScreenManager: resolution={_width}x{_height}");
    }

    private static void ApplyFullscreen()
    {
        Application.Host?.SetFullscreen(_isFullscreen);
        Debug.Log($"ScreenManager: fullscreen={_isFullscreen}");
    }
}
