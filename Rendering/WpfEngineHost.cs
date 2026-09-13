using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NanoUint.Debugging;
using NanoUint.Diagnostics;

namespace NanoUint.Rendering;

internal sealed class WpfEngineHost
{
    private readonly IGameBootstrapper _bootstrapper;
    private Window? _window;
    private Canvas? _rootCanvas;
    private WpfRenderer? _renderer;
    private Scene? _mainScene;
    private DateTime _lastFrame;
    private bool _isRunning;
    private MediaElement? _videoPlayer;
    private Action? _onVideoFinished;
#if DEBUG
    private Debugging.UE.UIManager? _ueUi;
    private Debugging.UE.ObjectExplorerPanel? _objectExplorer;
    private Debugging.UE.InspectorPanel? _inspector;
    private Debugging.UE.LogPanel? _logPanel;
#endif
    private Point _lastMousePosition;

    internal Point LastMousePosition => _lastMousePosition;

    public WpfEngineHost(IGameBootstrapper bootstrapper)
    {
        _bootstrapper = bootstrapper;
    }

    [STAThread]
    public void Run()
    {
        var app = new System.Windows.Application();

        _window = new Window
        {
            Title = "Steins;Gate X",
            Width = ScreenManager.Width,
            Height = ScreenManager.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.SingleBorderWindow,
            AllowsTransparency = false,
            Background = Brushes.Black,
            ResizeMode = ResizeMode.NoResize,
            Focusable = true,
        };

        _rootCanvas = new Canvas
        {
            Width = ScreenManager.Width,
            Height = ScreenManager.Height,
            ClipToBounds = true,
            Background = Brushes.Black,
            Focusable = true,
            IsHitTestVisible = true,
        };
        _window.Content = _rootCanvas;

        if (Application.Default != null)
        {
            Application.Default.Dispatcher = new WpfDispatcher(_window.Dispatcher);
            Application.Default.System = new WpfSystemServices(() => _window);
        }

        _window.KeyDown += OnKeyDown;
        _window.KeyUp += OnKeyUp;
        _rootCanvas.MouseLeftButtonDown += OnCanvasClick;
        _rootCanvas.MouseRightButtonDown += OnCanvasRightClick;
        _rootCanvas.MouseMove += OnCanvasMouseMove;

        #region Main Loop: CompositionTarget.Rendering
        // Fires before WPF render at Render priority; drives both game logic and render sync.
        CompositionTarget.Rendering += OnFrame;

        _renderer = new WpfRenderer(_rootCanvas);
        _mainScene = new Scene("Main");
        _renderer.SetActiveScene(_mainScene);
        SceneManager.LoadScene(_mainScene);
        Application.Default!.Renderers = _renderer.Registry;
#if DEBUG
        _ueUi = new Debugging.UE.UIManager(_rootCanvas);
        _objectExplorer = new Debugging.UE.ObjectExplorerPanel(_rootCanvas);
        _inspector = new Debugging.UE.InspectorPanel(_rootCanvas);
        _logPanel = new Debugging.UE.LogPanel(_rootCanvas);
        _ueUi.AddPanelTab(_objectExplorer, "ObjectExplorer");
        _ueUi.AddPanelTab(_inspector, "Inspector");
        _ueUi.AddPanelTab(_logPanel, "Log");
        _objectExplorer.OnGameObjectSelected += go => Debugging.UE.InspectorManager.Inspect(go);
        _inspector.ShowInExplorerRequested += () => _objectExplorer.Show();
        Debugging.UE.InspectorManager.Panel = _inspector;
        _objectExplorer.Show();
        _inspector.Show();
        _logPanel.Show();
#endif

        _window.Loaded += OnLoaded;
        _window.Closed += OnClosed;
        _window.Activated += (s, e) =>
        {
            _window.Focus();
            Logger.Trace("WPF", "Window activated + focused");
        };

        Logger.Info("WPF", "WpfEngineHost starting WPF Application.Run()");
        app.Run(_window);
        #endregion
    }

    public void Shutdown()
    {
        Logger.Info("WPF", "Shutdown requested");
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            System.Windows.Application.Current.Shutdown();
        });
    }

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _lastFrame = DateTime.UtcNow;
        ApplyClientResolution(ScreenManager.Width, ScreenManager.Height);
        Logger.Info("WPF", $"Window loaded. Client={ScreenManager.Width}x{ScreenManager.Height}, Outer={_window!.ActualWidth:F0}x{_window.ActualHeight:F0}");

        LoadEmbeddedStyles();

        #region Engine Splash: NanoUint Logo fade in → hold → fade out
        ResourceManager.Initialize();
        RunEngineSplash(() =>
        {
            Logger.Info("WPF", "Engine splash complete. Calling IGameBootstrapper.OnStart()");
            _bootstrapper.OnStart(_mainScene!);

            foreach (var go in _mainScene!.RootObjects)
                go.NotifyStart();

            _isRunning = true;
            Logger.Info("WPF", $"Engine started. {_mainScene.RootObjects.Count} root objects. " +
                $"Frame loop via CompositionTarget.Rendering.");
        });
        #endregion
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Logger.Info("WPF", "Window closed — stopping engine");
        _isRunning = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    #endregion

    #region Engine Splash (NanoUint Logo fade in → hold → fade out)

    private void RunEngineSplash(Action onComplete)
    {
        var logoBmp = ResourceManager.GetBitmap("Logo.jpg");
        if (logoBmp == null)
        {
            Logger.Info("WPF", "Engine splash: Logo.jpg not found — skipping");
            onComplete();
            return;
        }

        if (_rootCanvas == null || _window == null) { onComplete(); return; }

        var w = _window.ActualWidth > 0 ? _window.ActualWidth : 1280;
        var h = _window.ActualHeight > 0 ? _window.ActualHeight : 720;

        var overlay = new Grid
        {
            Background = Brushes.Black,
            Width = w,
            Height = h,
        };

        var img = new Image
        {
            Source = logoBmp,
            Stretch = Stretch.Uniform,
            Opacity = 0,
            MaxWidth = w * 0.6,
            MaxHeight = h * 0.4,
        };
        overlay.Children.Add(img);
        _rootCanvas.Children.Add(overlay);
        Panel.SetZIndex(overlay, int.MaxValue);

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5))
        {
            BeginTime = TimeSpan.FromSeconds(2.0),
        };

        var sb = new Storyboard();
        sb.Children.Add(fadeIn);
        sb.Children.Add(fadeOut);
        Storyboard.SetTarget(fadeIn, img);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
        Storyboard.SetTarget(fadeOut, img);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

        sb.Completed += (_, _) =>
        {
            _rootCanvas.Children.Remove(overlay);
            onComplete();
        };

        Logger.Info("WPF", "Engine splash: playing Logo.jpg fade sequence");
        sb.Begin();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        Logger.Trace("Input", $"KeyDown: {e.Key}");

        var engineKey = ConvertKey(e.Key);

        #region Let the Active Screen Handle Keys First
        if (InputManager.KeyDispatch?.Invoke(engineKey) == true)
            return;

        switch (e.Key)
        {
            case Key.Space:
            case Key.Enter:
            case Key.Right:
                // Phone modal interception: while the phone is open, Enter/Space/Right
                // route to the phone (wake black screen / interact) instead of advancing dialogue.
                if (_mainScene!.FindObject("__Phone")?.GetComponent<PhoneScreen>() is { } ps
                    && ps.CurrentState != PhoneScreen.State.Closed
                    && ps.CurrentState != PhoneScreen.State.Opening)
                {
                    if (ps.CurrentState == PhoneScreen.State.BlackScreen)
                    {
                        ps.WakePhone();
                    }
                    CoroutineScheduler.Instance.Tick(0f);
                    _renderer?.UpdateDirtyComponents();
                    break;
                }
                InputManager.FeedAdvancePress();
                // Immediately tick coroutines waiting on input instead of waiting for the next frame.
                CoroutineScheduler.Instance.Tick(0f);
                _renderer?.UpdateDirtyComponents();
                InputManager.ConsumeAdvancePress();
                break;
            case Key.A:
                InputManager.FeedAutoToggle();
                InputManager.FireAutoToggle();
                break;
            case Key.LeftCtrl:
            case Key.RightCtrl:
                InputManager.FeedSkipState(true);
                break;
            case Key.LeftShift:
            case Key.RightShift:
                InputManager.FireHideUIToggle();
                break;
#if DEBUG
            case Key.F3:
                Logger.Info("Input", "F3: Toggle UnityExplorer UI");
                _ueUi?.ToggleAll();
                break;
            case Key.F2:
                Logger.Info("Input", "F2: Toggle Inspector panel");
                _inspector?.Toggle();
                _ueUi?.UpdateTabStates();
                break;
#endif
            case Key.F5:
                Logger.Info("Input", "F5: Quick Save");
                InputManager.FireQuickSave();
                break;
            case Key.F6:
                Logger.Info("Input", "F6: Open Save Screen");
                InputManager.FireSaveScreen();
                break;
            case Key.F7:
                Logger.Info("Input", "F7: Open Load Screen");
                InputManager.FireLoadScreen();
                break;
            case Key.F9:
                Logger.Info("Input", "F9: Quick Load");
                InputManager.FireQuickLoad();
                break;
            case Key.S:
                Logger.Info("Input", "S: Quick Save");
                InputManager.FireQuickSave();
                break;
            case Key.L:
                Logger.Info("Input", "L: Quick Load");
                InputManager.FireQuickLoad();
                break;
            case Key.F:
                Logger.Info("Input", "F: Toggle fullscreen");
                ScreenManager.IsFullscreen = !ScreenManager.IsFullscreen;
                break;
            case Key.P:
                if ((System.Windows.Input.Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    Logger.Info("Input", "Ctrl+P: Open phone");
                    InputManager.FirePhoneToggle();
                }
                break;
            case Key.Escape:
                Logger.Info("Input", "Esc: System menu");
                InputManager.FeedMenuPress();
                break;
            case Key.PageUp:
                Logger.Info("Input", "PageUp: Backlog");
                InputManager.FireBacklog();
                break;
            case Key.Up:
                InputManager.FeedUpPress();
                break;
            case Key.Down:
                InputManager.FeedDownPress();
                break;
            case Key.Left:
                InputManager.FeedLeftPress();
                break;
        }
        #endregion
    }

    private void OnKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            InputManager.FeedSkipState(false);
        }
    }

    private void OnCanvasClick(object sender, MouseButtonEventArgs e)
    {
        Logger.Trace("Input", $"Canvas click: OriginalSource={e.OriginalSource?.GetType().Name}, " +
            $"Source={e.Source?.GetType().Name}, Position={e.GetPosition(_rootCanvas)}");

        if (_mainScene!.FindObject("__Phone")?.GetComponent<PhoneScreen>() is { } ps
            && ps.CurrentState != PhoneScreen.State.Closed
            && ps.CurrentState != PhoneScreen.State.Opening)
        {
            if (ps.CurrentState == PhoneScreen.State.BlackScreen)
            {
                ps.WakePhone();
                CoroutineScheduler.Instance.Tick(0f);
                _renderer?.UpdateDirtyComponents();
            }
            return;
        }

        if (e.OriginalSource == _rootCanvas)
        {
            Logger.Trace("Input", "Canvas background clicked → advance");
            InputManager.FeedAdvancePress();
            CoroutineScheduler.Instance.Tick(0f);
            _renderer?.UpdateDirtyComponents();
            InputManager.ConsumeAdvancePress();
        }
    }

    private void OnCanvasRightClick(object sender, MouseButtonEventArgs e)
    {
        Logger.Trace("Input", "Canvas right-click → system menu");
        InputManager.FeedMenuPress();
        e.Handled = true;
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        _lastMousePosition = e.GetPosition(_rootCanvas);
        WpfRenderer.LastMousePosition = _lastMousePosition;
    }

    internal string? CaptureThumbnail()
    {
        if (_rootCanvas == null) return null;
        try
        {
            _rootCanvas.UpdateLayout();
            _renderer?.UpdateDirtyComponents();

            var actualW = _rootCanvas.ActualWidth > 0 ? (int)_rootCanvas.ActualWidth : 1280;
            var actualH = _rootCanvas.ActualHeight > 0 ? (int)_rootCanvas.ActualHeight : 720;
            var rtb = new RenderTargetBitmap(actualW, actualH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(_rootCanvas);

            var thumb = new TransformedBitmap(rtb, new ScaleTransform(320.0 / actualW, 180.0 / actualH));

            var encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = 60;
            encoder.Frames.Add(BitmapFrame.Create(thumb));

            using var ms = new System.IO.MemoryStream();
            encoder.Save(ms);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch (Exception ex)
        {
            Logger.Warning("Render", $"CaptureThumbnail failed: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Main Loop

    private double _accumulator;
    private const double FixedDt = 1.0 / 60.0;

    private void OnFrame(object? sender, EventArgs e)
    {
        if (!_isRunning || _mainScene == null) return;

        try
        {
            var now = DateTime.UtcNow;
            var frameDt = Math.Min((now - _lastFrame).TotalSeconds, 0.1);
            _lastFrame = now;

            _accumulator += frameDt * Debugging.UE.UIManager.TimeScale;

            while (_accumulator >= FixedDt)
            {
                _mainScene.Update((float)FixedDt);
                var physics = Application.Default?.Physics;
                if (physics != null && physics.Enabled)
                    physics.StepWithInterpolation((float)FixedDt, (float)(_accumulator / FixedDt));
                _accumulator -= FixedDt;
            }

            _renderer!.UpdateDirtyComponents();

            if (_rootCanvas != null && (Application.ShakeOffsetX != 0 || Application.ShakeOffsetY != 0))
            {
                _rootCanvas.RenderTransform = new TranslateTransform(
                    Application.ShakeOffsetX, Application.ShakeOffsetY);
                Application.ShakeOffsetX = 0;
                Application.ShakeOffsetY = 0;
            }
            else if (_rootCanvas != null && _rootCanvas.RenderTransform != null)
            {
                _rootCanvas.RenderTransform = null;
            }

            InputManager.EndFrame();
        }
        catch (Exception ex)
        {
            Logger.Error("Engine", "Frame exception", ex);
        }
    }

    #endregion

    #region Window Control (called by ScreenManager)

    internal void SetFullscreen(bool fullscreen)
    {
        var win = _window;
        if (win == null) return;
        win.Dispatcher.Invoke(() =>
        {
            if (fullscreen)
            {
                win.WindowStyle = WindowStyle.None;
                win.WindowState = WindowState.Maximized;
                win.ResizeMode = ResizeMode.NoResize;
            }
            else
            {
                win.WindowStyle = WindowStyle.SingleBorderWindow;
                win.WindowState = WindowState.Normal;
                    win.ResizeMode = ResizeMode.NoResize;
                    ApplyClientResolution(ScreenManager.Width, ScreenManager.Height);
            }
            Logger.Info("Screen", $"Fullscreen={fullscreen}, State={win.WindowState}, Size={win.Width}x{win.Height}");
        });
    }

    internal void SetResolution(int width, int height)
    {
        _window?.Dispatcher.Invoke(() =>
        {
            ApplyClientResolution(width, height);
            Logger.Info("Screen", $"Resolution set to {width}x{height} (client)");
        });
    }

    private void ApplyClientResolution(int width, int height)
    {
        if (_window == null || _rootCanvas == null) return;

        _rootCanvas.Width = width;
        _rootCanvas.Height = height;
        if (_window.WindowState == WindowState.Normal)
        {
            _window.SizeToContent = SizeToContent.WidthAndHeight;
            _window.UpdateLayout();
            _window.SizeToContent = SizeToContent.Manual;
            _window.Width = _window.ActualWidth;
            _window.Height = _window.ActualHeight;
        }
    }

    internal Window? GetWindow() => _window;

    #endregion

    #region Video Playback

    internal void PlayVideo(string filePath, Action? onFinished = null)
    {
        if (_window == null || _rootCanvas == null) return;
        _window.Dispatcher.Invoke(() =>
        {
            _onVideoFinished = onFinished;
            _videoPlayer = new MediaElement
            {
                Source = new Uri(filePath),
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Stop,
                Stretch = Stretch.Uniform,
                Width = _window.ActualWidth,
                Height = _window.ActualHeight,
            };
            Panel.SetZIndex(_videoPlayer, 10000);
            _rootCanvas.Children.Add(_videoPlayer);
            _videoPlayer.MediaEnded += OnVideoEnded;
            _videoPlayer.MouseLeftButtonDown += OnVideoSkip;
            _videoPlayer.KeyDown += OnVideoSkip;
            _videoPlayer.Focus();
            _videoPlayer.Play();
            Logger.Info("Video", $"Playback started: {filePath}");
        });
    }

    internal void StopVideo()
    {
        _window?.Dispatcher.Invoke(() =>
        {
            if (_videoPlayer != null)
            {
                _videoPlayer.Stop();
                _videoPlayer.MediaEnded -= OnVideoEnded;
                _rootCanvas?.Children.Remove(_videoPlayer);
                _videoPlayer = null;
                Logger.Info("Video", "Playback stopped");
            }
        });
    }

    private void OnVideoEnded(object? sender, EventArgs e) => FinishVideo();
    private void OnVideoSkip(object? sender, EventArgs e) => FinishVideo();

    private void FinishVideo()
    {
        StopVideo();
        _onVideoFinished?.Invoke();
        _onVideoFinished = null;
        Logger.Info("Video", "Playback finished");
    }

    #endregion

    #region Embedded Styles + Fonts

    internal static System.Windows.Media.FontFamily UIFontFamily { get; private set; }
        = new System.Windows.Media.FontFamily("Segoe UI");

    private void LoadEmbeddedStyles()
    {
        try
        {
            LoadCustomFonts();

            // Inherit the font at Window level so control-template internals are not affected.
            _window!.FontFamily = UIFontFamily;
            Logger.Trace("WPF", $"Window font family set: {UIFontFamily.Source}");

            var styleResource = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/NanoUint;component/Resources/Styles/CommonStyles.xaml")
            };
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(styleResource);
            Logger.Trace("WPF", "Embedded styles loaded");
        }
        catch (Exception ex)
        {
            Logger.Warning("WPF", $"Embedded styles not loaded: {ex.Message}");
        }
    }

    private static void LoadCustomFonts()
    {
        try
        {
            var fontPaths = new[]
            {
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Fonts", "HarmonySansBold.ttf"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Fonts", "HarmonySansBold.otf"),
            };

            foreach (var fontPath in fontPaths)
            {
                if (System.IO.File.Exists(fontPath))
                {
                    var baseUri = new Uri(
                        System.IO.Path.GetDirectoryName(fontPath)! + System.IO.Path.DirectorySeparatorChar);
                    var fontFile = System.IO.Path.GetFileName(fontPath);
                    var fontFamily = new System.Windows.Media.FontFamily(baseUri, $"./{fontFile}");
                    UIFontFamily = fontFamily;
                    Logger.Info("WPF", $"Custom font loaded: {System.IO.Path.GetFileName(fontPath)}");
                    return;
                }
            }

            foreach (var systemFont in System.Windows.Media.Fonts.SystemFontFamilies)
            {
                if (systemFont.Source.Contains("Harmony"))
                {
                    UIFontFamily = systemFont;
                    Logger.Info("WPF", $"System font found: {systemFont.Source}");
                    return;
                }
            }

            Logger.Info("WPF", "Harmony Sans Bold not found — using Segoe UI fallback. " +
                "Place HarmonySansBold.ttf in Resources/Fonts/ to apply the design system font.");
        }
        catch (Exception ex)
        {
            Logger.Warning("WPF", $"Font loading failed: {ex.Message}. Using Segoe UI.");
        }
    }
    #endregion

    #region Key conversion (WPF → engine-neutral)

    private static EngineKey ConvertKey(System.Windows.Input.Key key) => key switch
    {
        System.Windows.Input.Key.A => EngineKey.A, System.Windows.Input.Key.B => EngineKey.B,
        System.Windows.Input.Key.C => EngineKey.C, System.Windows.Input.Key.D => EngineKey.D,
        System.Windows.Input.Key.E => EngineKey.E, System.Windows.Input.Key.F => EngineKey.F,
        System.Windows.Input.Key.G => EngineKey.G, System.Windows.Input.Key.H => EngineKey.H,
        System.Windows.Input.Key.I => EngineKey.I, System.Windows.Input.Key.J => EngineKey.J,
        System.Windows.Input.Key.K => EngineKey.K, System.Windows.Input.Key.L => EngineKey.L,
        System.Windows.Input.Key.M => EngineKey.M, System.Windows.Input.Key.N => EngineKey.N,
        System.Windows.Input.Key.O => EngineKey.O, System.Windows.Input.Key.P => EngineKey.P,
        System.Windows.Input.Key.Q => EngineKey.Q, System.Windows.Input.Key.R => EngineKey.R,
        System.Windows.Input.Key.S => EngineKey.S, System.Windows.Input.Key.T => EngineKey.T,
        System.Windows.Input.Key.U => EngineKey.U, System.Windows.Input.Key.V => EngineKey.V,
        System.Windows.Input.Key.W => EngineKey.W, System.Windows.Input.Key.X => EngineKey.X,
        System.Windows.Input.Key.Y => EngineKey.Y, System.Windows.Input.Key.Z => EngineKey.Z,
        System.Windows.Input.Key.D0 => EngineKey.D0, System.Windows.Input.Key.D1 => EngineKey.D1,
        System.Windows.Input.Key.D2 => EngineKey.D2, System.Windows.Input.Key.D3 => EngineKey.D3,
        System.Windows.Input.Key.D4 => EngineKey.D4, System.Windows.Input.Key.D5 => EngineKey.D5,
        System.Windows.Input.Key.D6 => EngineKey.D6, System.Windows.Input.Key.D7 => EngineKey.D7,
        System.Windows.Input.Key.D8 => EngineKey.D8, System.Windows.Input.Key.D9 => EngineKey.D9,
        System.Windows.Input.Key.Left => EngineKey.Left, System.Windows.Input.Key.Right => EngineKey.Right,
        System.Windows.Input.Key.Up => EngineKey.Up, System.Windows.Input.Key.Down => EngineKey.Down,
        System.Windows.Input.Key.PageUp => EngineKey.PageUp, System.Windows.Input.Key.PageDown => EngineKey.PageDown,
        System.Windows.Input.Key.Home => EngineKey.Home, System.Windows.Input.Key.End => EngineKey.End,
        System.Windows.Input.Key.Space => EngineKey.Space, System.Windows.Input.Key.Enter => EngineKey.Enter,
        System.Windows.Input.Key.Escape => EngineKey.Escape, System.Windows.Input.Key.Tab => EngineKey.Tab,
        System.Windows.Input.Key.Back => EngineKey.Backspace, System.Windows.Input.Key.Delete => EngineKey.Delete,
        System.Windows.Input.Key.Insert => EngineKey.Insert,
        System.Windows.Input.Key.LeftShift => EngineKey.LeftShift, System.Windows.Input.Key.RightShift => EngineKey.RightShift,
        System.Windows.Input.Key.LeftCtrl => EngineKey.LeftCtrl, System.Windows.Input.Key.RightCtrl => EngineKey.RightCtrl,
        System.Windows.Input.Key.LeftAlt => EngineKey.LeftAlt, System.Windows.Input.Key.RightAlt => EngineKey.RightAlt,
        System.Windows.Input.Key.F1 => EngineKey.F1, System.Windows.Input.Key.F2 => EngineKey.F2,
        System.Windows.Input.Key.F3 => EngineKey.F3, System.Windows.Input.Key.F4 => EngineKey.F4,
        System.Windows.Input.Key.F5 => EngineKey.F5, System.Windows.Input.Key.F6 => EngineKey.F6,
        System.Windows.Input.Key.F7 => EngineKey.F7, System.Windows.Input.Key.F8 => EngineKey.F8,
        System.Windows.Input.Key.F9 => EngineKey.F9, System.Windows.Input.Key.F10 => EngineKey.F10,
        System.Windows.Input.Key.F11 => EngineKey.F11, System.Windows.Input.Key.F12 => EngineKey.F12,
        _ => EngineKey.None,
    };

    #endregion
}
