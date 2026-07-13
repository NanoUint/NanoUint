using System.Windows;
using System.Windows.Media.Animation;

namespace NanoUint.Services;

/// <summary>
/// 管理 FallenAltair 游戏的多窗口协调。
/// 处理窗口定位、动画以及主窗口-设置窗口的分屏。
/// </summary>
public class WindowManager
{
    private readonly Window _mainWindow;
    private Window? _settingsWindow;
    private Window? _saveLoadWindow;

    // 存储用于从设置模式恢复的状态
    private double _mainOriginalLeft;
    private double _mainOriginalTop;
    private double _mainOriginalWidth;
    private double _mainOriginalHeight;

    private bool _isSettingsOpen;
    private bool _isSaveLoadOpen;

    public WindowManager(Window mainWindow)
    {
        _mainWindow = mainWindow;
        _mainWindow.LocationChanged += OnMainWindowMoved;
    }

    /// <summary>打开设置窗口 —— 主窗口向左滑动，设置窗口出现在右侧</summary>
    public void OpenSettings(Window settingsWindow)
    {
        if (_isSettingsOpen) return;

        // 设置窗口和手机窗口互斥
        if (_isPhoneOpen)
            ClosePhone();

        // 保存主窗口的位置和大小
        _mainOriginalLeft = _mainWindow.Left;
        _mainOriginalTop = _mainWindow.Top;
        _mainOriginalWidth = _mainWindow.Width;
        _mainOriginalHeight = _mainWindow.Height;

        _settingsWindow = settingsWindow;
        _settingsWindow.Closed += (_, _) => CloseSettings();

        // 计算屏幕布局
        var (screenLeft, screenTop, screenWidth, screenHeight) = GetCurrentScreen();
        double halfWidth = screenWidth / 2;

        // 将主窗口动画移动到左半部分
        AnimateWindow(_mainWindow,
            screenLeft, screenTop,
            halfWidth, screenHeight);

        // 将设置窗口定位并显示在右半部分
        _settingsWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        _settingsWindow.Left = screenLeft + halfWidth;
        _settingsWindow.Top = screenTop;
        _settingsWindow.Width = halfWidth;
        _settingsWindow.Height = screenHeight;
        _settingsWindow.Show();

        _isSettingsOpen = true;
    }

    /// <summary>关闭设置窗口并将主窗口恢复到原始位置</summary>
    public void CloseSettings()
    {
        if (!_isSettingsOpen) return;

        _isSettingsOpen = false;

        // Animate main window back to original position
        AnimateWindow(_mainWindow,
            _mainOriginalLeft, _mainOriginalTop,
            _mainOriginalWidth, _mainOriginalHeight);

        _settingsWindow?.Close();
        _settingsWindow = null;
    }

    /// <summary>切换设置窗口</summary>
    public void ToggleSettings(Window settingsWindow)
    {
        if (_isSettingsOpen)
            CloseSettings();
        else
            OpenSettings(settingsWindow);
    }

    /// <summary>以模态叠加层形式打开存档/读档窗口</summary>
    public void OpenSaveLoad(Window saveLoadWindow, bool isSaveMode)
    {
        if (_isSaveLoadOpen) return;

        _saveLoadWindow = saveLoadWindow;
        _saveLoadWindow.Owner = _mainWindow;
        _saveLoadWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // 通过 Tag 传递存档模式信息
        _saveLoadWindow.Tag = isSaveMode;

        _saveLoadWindow.Closed += (_, _) =>
        {
            _isSaveLoadOpen = false;
            _saveLoadWindow = null;
        };

        _saveLoadWindow.ShowDialog();
        _isSaveLoadOpen = true;
    }

    /// <summary>切换主窗口的全屏模式</summary>
    public void SetFullscreen(bool fullscreen)
    {
        if (fullscreen)
        {
            _mainOriginalLeft = _mainWindow.Left;
            _mainOriginalTop = _mainWindow.Top;
            _mainOriginalWidth = _mainWindow.Width;
            _mainOriginalHeight = _mainWindow.Height;

            _mainWindow.WindowStyle = WindowStyle.None;
            _mainWindow.WindowState = WindowState.Maximized;
        }
        else
        {
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.WindowStyle = WindowStyle.SingleBorderWindow;
            _mainWindow.Left = _mainOriginalLeft;
            _mainWindow.Top = _mainOriginalTop;
            _mainWindow.Width = _mainOriginalWidth;
            _mainWindow.Height = _mainOriginalHeight;
        }
    }

    /// <summary>设置主窗口分辨率</summary>
    public void SetResolution(int width, int height)
    {
        if (_mainWindow.WindowState == WindowState.Maximized)
            return;

        _mainWindow.Width = width;
        _mainWindow.Height = height;

        // 屏幕居中
        var (sLeft, sTop, sWidth, sHeight) = GetCurrentScreen();
        _mainWindow.Left = sLeft + (sWidth - width) / 2;
        _mainWindow.Top = sTop + (sHeight - height) / 2;
    }

    /// <summary>获取主窗口当前所在屏幕的工作区域</summary>
    private (double Left, double Top, double Width, double Height) GetCurrentScreen()
    {
        // 使用 SystemParameters 获取主屏幕工作区域
        // 生产环境中可使用 P/Invoke 支持多显示器
        var wa = SystemParameters.WorkArea;
        return (wa.Left, wa.Top, wa.Width, wa.Height);
    }

    /// <summary>将窗口动画移动到新位置并调整大小</summary>
    private static void AnimateWindow(Window window, double left, double top, double width, double height)
    {
        var duration = TimeSpan.FromMilliseconds(300);
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseInOut };

        // 动画左侧位置
        var leftAnim = new DoubleAnimation(window.Left, left, duration) { EasingFunction = easing };
        // 动画顶部位置
        var topAnim = new DoubleAnimation(window.Top, top, duration) { EasingFunction = easing };
        // 动画宽度
        var widthAnim = new DoubleAnimation(window.Width, width, duration) { EasingFunction = easing };
        // 动画高度
        var heightAnim = new DoubleAnimation(window.Height, height, duration) { EasingFunction = easing };

        // 应用动画
        window.BeginAnimation(Window.LeftProperty, leftAnim);
        window.BeginAnimation(Window.TopProperty, topAnim);
        window.BeginAnimation(Window.WidthProperty, widthAnim);
        window.BeginAnimation(Window.HeightProperty, heightAnim);
    }

    /// <summary>处理主窗口移动以保持设置窗口对齐</summary>
    private void OnMainWindowMoved(object? sender, EventArgs e)
    {
        if (!_isSettingsOpen || _settingsWindow == null) return;

        // 保持设置窗口与主窗口右侧对齐
        _settingsWindow.Left = _mainWindow.Left + _mainWindow.Width;
        _settingsWindow.Top = _mainWindow.Top;
        _settingsWindow.Height = _mainWindow.Height;
    }

    public bool IsSettingsOpen => _isSettingsOpen;

    #region 手机窗口

    private Window? _phoneWindow;
    private bool _isPhoneOpen;
    private double _phoneMainOriginalLeft;
    private double _phoneMainOriginalTop;
    private double _phoneMainOriginalWidth;
    private double _phoneMainOriginalHeight;

    /// <summary>打开手机窗口 —— 主窗口向左滑动，手机窗口出现在右侧</summary>
    public void OpenPhone(Window phoneWindow)
    {
        if (_isPhoneOpen) return;

        // 手机窗口和设置窗口互斥
        if (_isSettingsOpen)
            CloseSettings();

        // 保存主窗口位置
        _phoneMainOriginalLeft = _mainWindow.Left;
        _phoneMainOriginalTop = _mainWindow.Top;
        _phoneMainOriginalWidth = _mainWindow.Width;
        _phoneMainOriginalHeight = _mainWindow.Height;

        _phoneWindow = phoneWindow;
        _phoneWindow.Closed += (_, _) => ClosePhone();

        var (screenLeft, screenTop, screenWidth, screenHeight) = GetCurrentScreen();
        double phoneWidth = 310;
        double mainWidth = screenWidth - phoneWidth;

        // 将主窗口动画移动到左侧区域
        AnimateWindow(_mainWindow, screenLeft, screenTop, mainWidth, screenHeight);

        // 将手机窗口定位在右侧
        _phoneWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        _phoneWindow.Left = screenLeft + mainWidth;
        _phoneWindow.Top = screenTop;
        _phoneWindow.Width = phoneWidth;
        _phoneWindow.Height = screenHeight;
        _phoneWindow.Show();

        _isPhoneOpen = true;
    }

    /// <summary>关闭手机窗口并将主窗口恢复到原始位置</summary>
    public void ClosePhone()
    {
        if (!_isPhoneOpen) return;
        _isPhoneOpen = false;

        // 将主窗口动画恢复到原始位置
        AnimateWindow(_mainWindow,
            _phoneMainOriginalLeft, _phoneMainOriginalTop,
            _phoneMainOriginalWidth, _phoneMainOriginalHeight);

        _phoneWindow?.Close();
        _phoneWindow = null;
    }

    /// <summary>切换手机窗口</summary>
    public void TogglePhone(Window phoneWindow)
    {
        if (_isPhoneOpen)
            ClosePhone();
        else
            OpenPhone(phoneWindow);
    }

    public bool IsPhoneOpen => _isPhoneOpen;
    public bool IsSaveLoadOpen => _isSaveLoadOpen;

    #endregion
}
