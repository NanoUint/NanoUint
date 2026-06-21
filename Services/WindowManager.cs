using System.Windows;
using System.Windows.Media.Animation;

namespace NanoUint.Services;

/// <summary>
/// Manages multi-window orchestration for the Liminal game.
/// Handles window positioning, animations, and the main-settings window split.
/// </summary>
public class WindowManager
{
    private readonly Window _mainWindow;
    private Window? _settingsWindow;
    private Window? _saveLoadWindow;

    // Stored state for restoring from settings mode
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

    /// <summary>Open the settings window - main window slides left, settings appears on the right</summary>
    public void OpenSettings(Window settingsWindow)
    {
        if (_isSettingsOpen) return;

        // Save main window position and size
        _mainOriginalLeft = _mainWindow.Left;
        _mainOriginalTop = _mainWindow.Top;
        _mainOriginalWidth = _mainWindow.Width;
        _mainOriginalHeight = _mainWindow.Height;

        _settingsWindow = settingsWindow;
        _settingsWindow.Closed += (_, _) => CloseSettings();

        // Calculate screen layout
        var (screenLeft, screenTop, screenWidth, screenHeight) = GetCurrentScreen();
        double halfWidth = screenWidth / 2;

        // Animate main window to left half
        AnimateWindow(_mainWindow,
            screenLeft, screenTop,
            halfWidth, screenHeight);

        // Position and show settings window on right half
        _settingsWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        _settingsWindow.Left = screenLeft + halfWidth;
        _settingsWindow.Top = screenTop;
        _settingsWindow.Width = halfWidth;
        _settingsWindow.Height = screenHeight;
        _settingsWindow.Show();

        _isSettingsOpen = true;
    }

    /// <summary>Close the settings window and restore main window</summary>
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

    /// <summary>Toggle settings window</summary>
    public void ToggleSettings(Window settingsWindow)
    {
        if (_isSettingsOpen)
            CloseSettings();
        else
            OpenSettings(settingsWindow);
    }

    /// <summary>Open the save/load window as a modal overlay</summary>
    public void OpenSaveLoad(Window saveLoadWindow, bool isSaveMode)
    {
        if (_isSaveLoadOpen) return;

        _saveLoadWindow = saveLoadWindow;
        _saveLoadWindow.Owner = _mainWindow;
        _saveLoadWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // Pass save mode info via Tag
        _saveLoadWindow.Tag = isSaveMode;

        _saveLoadWindow.Closed += (_, _) =>
        {
            _isSaveLoadOpen = false;
            _saveLoadWindow = null;
        };

        _saveLoadWindow.ShowDialog();
        _isSaveLoadOpen = true;
    }

    /// <summary>Toggle fullscreen mode for the main window</summary>
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

    /// <summary>Set the main window resolution</summary>
    public void SetResolution(int width, int height)
    {
        if (_mainWindow.WindowState == WindowState.Maximized)
            return;

        _mainWindow.Width = width;
        _mainWindow.Height = height;

        // Center on screen
        var (sLeft, sTop, sWidth, sHeight) = GetCurrentScreen();
        _mainWindow.Left = sLeft + (sWidth - width) / 2;
        _mainWindow.Top = sTop + (sHeight - height) / 2;
    }

    /// <summary>Get the work area of the screen the main window is currently on</summary>
    private (double Left, double Top, double Width, double Height) GetCurrentScreen()
    {
        // Use SystemParameters for the primary screen work area
        // In production, use P/Invoke for multi-monitor support
        var wa = SystemParameters.WorkArea;
        return (wa.Left, wa.Top, wa.Width, wa.Height);
    }

    /// <summary>Animate a window to a new position and size</summary>
    private static void AnimateWindow(Window window, double left, double top, double width, double height)
    {
        var duration = TimeSpan.FromMilliseconds(300);
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseInOut };

        // Animate Left
        var leftAnim = new DoubleAnimation(window.Left, left, duration) { EasingFunction = easing };
        // Animate Top
        var topAnim = new DoubleAnimation(window.Top, top, duration) { EasingFunction = easing };
        // Animate Width
        var widthAnim = new DoubleAnimation(window.Width, width, duration) { EasingFunction = easing };
        // Animate Height
        var heightAnim = new DoubleAnimation(window.Height, height, duration) { EasingFunction = easing };

        // Apply animations
        window.BeginAnimation(Window.LeftProperty, leftAnim);
        window.BeginAnimation(Window.TopProperty, topAnim);
        window.BeginAnimation(Window.WidthProperty, widthAnim);
        window.BeginAnimation(Window.HeightProperty, heightAnim);
    }

    /// <summary>Handle main window movement to keep settings window aligned</summary>
    private void OnMainWindowMoved(object? sender, EventArgs e)
    {
        if (!_isSettingsOpen || _settingsWindow == null) return;

        // Keep settings window aligned to the right of main window
        _settingsWindow.Left = _mainWindow.Left + _mainWindow.Width;
        _settingsWindow.Top = _mainWindow.Top;
        _settingsWindow.Height = _mainWindow.Height;
    }

    public bool IsSettingsOpen => _isSettingsOpen;
    public bool IsSaveLoadOpen => _isSaveLoadOpen;
}
