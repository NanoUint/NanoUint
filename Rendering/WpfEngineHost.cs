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

/// <summary>引擎内部的 WPF 宿主。创建 Window + Canvas，驱动主循环。</summary>
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

    /// <summary>当前鼠标在 Canvas 中的位置（WPF 像素坐标）。供 HintRenderer 使用。</summary>
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
            Width = 1280,
            Height = 720,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.SingleBorderWindow,
            AllowsTransparency = false,
            Background = Brushes.Black,
            ResizeMode = ResizeMode.CanResize,
            Focusable = true,
        };

        _rootCanvas = new Canvas
        {
            ClipToBounds = true,
            Background = Brushes.Black,
            Focusable = true,
            IsHitTestVisible = true,
        };
        _window.Content = _rootCanvas;

        // 输入事件 → InputManager
        _window.KeyDown += OnKeyDown;
        _window.KeyUp += OnKeyUp;
        _rootCanvas.MouseLeftButtonDown += OnCanvasClick;
        _rootCanvas.MouseRightButtonDown += OnCanvasRightClick;
        _rootCanvas.MouseMove += OnCanvasMouseMove;

        #region 主循环：CompositionTarget.Rendering
        // 在 WPF 渲染前触发（Render 优先级）。同时处理游戏逻辑和渲染同步。
        CompositionTarget.Rendering += OnFrame;

        _renderer = new WpfRenderer(_rootCanvas);
        _mainScene = new Scene("Main");
        _renderer.SetActiveScene(_mainScene);
        SceneManager.LoadScene(_mainScene);
#if DEBUG
        // UnityExplorer 1:1 DevPanel: 全局顶栏 + 三个独立浮动面板
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

    #region 事件处理

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _lastFrame = DateTime.UtcNow;
        Logger.Info("WPF", $"Window loaded. Size: {_window!.ActualWidth:F0}x{_window.ActualHeight:F0}");

        // 样式资源（引擎内嵌暗色主题）
        LoadEmbeddedStyles();

        #region 引擎 Splash：NanoUint Logo 渐显→停留→渐隐
        ResourceManager.Initialize();
        RunEngineSplash(() =>
        {
            // 引擎 Splash 结束 → 启动游戏
            Logger.Info("WPF", "Engine splash complete. Calling IGameBootstrapper.OnStart()");
            _bootstrapper.OnStart(_mainScene!);

            // 触发所有 Awake + Start
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

    #region 引擎 Splash（NanoUint Logo 渐显→停留→渐隐）

    /// <summary>在游戏 OnStart 之前播放引擎 Logo 动画。</summary>
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

        // 全屏黑色覆盖层
        var overlay = new Grid
        {
            Background = Brushes.Black,
            Width = w,
            Height = h,
        };

        // Logo 图片（居中，初始透明）
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

        // 渐显 0→1 (0.5s)，停留至 2.0s，渐隐 1→0 (0.5s)
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

        #region 让活跃 Screen 优先处理按键
        if (InputManager.KeyDispatch?.Invoke(e.Key) == true)
            return; // Screen 已处理，不再走全局逻辑

        switch (e.Key)
        {
            case Key.Space:
            case Key.Enter:
            case Key.Right:
                //  手机 Modal 拦截 — 手机打开时 Enter/空格/右键
                //  路由到手机（唤醒黑屏/交互），不推进对话。
                if (_mainScene.FindObject("__Phone")?.GetComponent<PhoneScreen>() is { } ps
                    && ps.CurrentState != PhoneScreen.State.Closed
                    && ps.CurrentState != PhoneScreen.State.Opening)
                {
                    if (ps.CurrentState == PhoneScreen.State.BlackScreen)
                    {
                        ps.WakePhone();  // 黑屏 → Home
                    }
                    // Home 等状态下 Enter 暂不处理（后续扩展 App 交互）
                    CoroutineScheduler.Instance.Tick(0f);
                    _renderer?.UpdateDirtyComponents();
                    break;
                }
                InputManager.FeedAdvancePress();
                // 立即推进等待输入的协程（不等下一帧）
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
                // S = Quick Save (单键快捷方式，原版 S;G 惯例)
                Logger.Info("Input", "S: Quick Save");
                InputManager.FireQuickSave();
                break;
            case Key.L:
                // L = Quick Load (单键快捷方式)
                Logger.Info("Input", "L: Quick Load");
                InputManager.FireQuickLoad();
                break;
            case Key.F:
                // F = Fullscreen toggle
                Logger.Info("Input", "F: Toggle fullscreen");
                ScreenManager.IsFullscreen = !ScreenManager.IsFullscreen;
                break;
            case Key.P:
                // Ctrl+P = Open phone
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

        // 手机 modal 拦截 — 手机打开时，点击唤醒黑屏
        if (_mainScene.FindObject("__Phone")?.GetComponent<PhoneScreen>() is { } ps
            && ps.CurrentState != PhoneScreen.State.Closed
            && ps.CurrentState != PhoneScreen.State.Opening)
        {
            if (ps.CurrentState == PhoneScreen.State.BlackScreen)
            {
                ps.WakePhone();
                CoroutineScheduler.Instance.Tick(0f);
                _renderer?.UpdateDirtyComponents();
            }
            return; // 不推进对话
        }

        // 只对 Canvas 背景的点击做 advance，不拦截按钮等子控件
        if (e.OriginalSource == _rootCanvas)
        {
            Logger.Trace("Input", "Canvas background clicked → advance");
            InputManager.FeedAdvancePress();
            // 立即推进等待输入的协程（不等下一帧）
            CoroutineScheduler.Instance.Tick(0f);
            _renderer?.UpdateDirtyComponents();
            InputManager.ConsumeAdvancePress();
        }
        // 子控件（按钮等）的点击由其自己的 Click 事件处理，不设 e.Handled=true
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

    /// <summary>捕获当前画面缩略图（320x180 JPEG Base64）。存档时调用。</summary>
    internal string? CaptureThumbnail()
    {
        if (_rootCanvas == null) return null;
        try
        {
            // 强制渲染以确保 Canvas 内容是最新的
            _rootCanvas.UpdateLayout();
            _renderer?.UpdateDirtyComponents();

            var actualW = _rootCanvas.ActualWidth > 0 ? (int)_rootCanvas.ActualWidth : 1280;
            var actualH = _rootCanvas.ActualHeight > 0 ? (int)_rootCanvas.ActualHeight : 720;
            var rtb = new RenderTargetBitmap(actualW, actualH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(_rootCanvas);

            // 缩放至 320x180
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

    #region 主循环

    /// <summary>每帧主循环：更新场景与协程 → 增量渲染同步 → 清理帧内输入状态。</summary>
    private void OnFrame(object? sender, EventArgs e)
    {
        if (!_isRunning || _mainScene == null) return;

        try
        {
            var now = DateTime.UtcNow;
            // 顶栏 TimeScale 控件缩放引擎时间 (UnityExplorer TimeScaleWidget)
            var dt = Math.Min((float)(now - _lastFrame).TotalSeconds, 0.1f) * Debugging.UE.UIManager.TimeScale;
            _lastFrame = now;

            // 1. 更新场景：GameObject.Update + Coroutine 推进
            //    协程中可能调用 MarkDirty()
            _mainScene.Update(dt);

            // 2. 增量渲染：只同步有变化的 Component 到 WPF 控件
            _renderer!.UpdateDirtyComponents();

            // 2.3 应用屏幕震动偏移
            if (_rootCanvas != null && (Application.ShakeOffsetX != 0 || Application.ShakeOffsetY != 0))
            {
                _rootCanvas.RenderTransform = new TranslateTransform(
                    Application.ShakeOffsetX, Application.ShakeOffsetY);
                // 每帧衰减回零（由协程重新设置非零值）
                Application.ShakeOffsetX = 0;
                Application.ShakeOffsetY = 0;
            }
            else if (_rootCanvas != null && _rootCanvas.RenderTransform != null)
            {
                _rootCanvas.RenderTransform = null;
            }

            // 2.5 ObjectExplorer 面板自带 1s 自动刷新 (UnityExplorer 风格)

            // 3. 清理帧内输入状态
            InputManager.EndFrame();
        }
        catch (Exception ex)
        {
            Logger.Error("Engine", "Frame exception", ex);
        }
    }

    #endregion

    #region 窗口控制（供 ScreenManager 调用）

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
                win.ResizeMode = ResizeMode.CanResize;
                win.Width = 1280; win.Height = 720;
            }
            Logger.Info("Screen", $"Fullscreen={fullscreen}, State={win.WindowState}, Size={win.Width}x{win.Height}");
        });
    }

    internal void SetResolution(int width, int height)
    {
        _window?.Dispatcher.Invoke(() =>
        {
            if (_window != null) { _window.Width = width; _window.Height = height; }
            Logger.Info("Screen", $"Resolution set to {width}x{height}");
        });
    }

    internal Window? GetWindow() => _window;

    #endregion

    #region 视频播放

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

    #region 内嵌样式 + 字体

    /// <summary>当前 UI 使用的字体族（Harmony Sans Bold 或系统回退）。</summary>
    internal static System.Windows.Media.FontFamily UIFontFamily { get; private set; }
        = new System.Windows.Media.FontFamily("Segoe UI");

    private void LoadEmbeddedStyles()
    {
        try
        {
            // 加载 Harmony Sans Bold 字体（如存在）
            LoadCustomFonts();

            // 通过 Window 级别字体继承，不影响控件模板内部元素
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

    /// <summary>尝试加载自定义字体（Harmony Sans Bold），找不到则回退 Segoe UI。</summary>
    private static void LoadCustomFonts()
    {
        try
        {
            // 搜索项目 Resources/Fonts/ 目录
            var fontPaths = new[]
            {
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Fonts", "HarmonySansBold.ttf"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Fonts", "HarmonySansBold.otf"),
            };

            foreach (var fontPath in fontPaths)
            {
                if (System.IO.File.Exists(fontPath))
                {
                    // 使用 FontFamily(Uri, string) 构造 — 第二参数是 "字体文件名#家族名"
                    // 省略 #家族名 时 WPF 自动使用字体文件中的默认家族名
                    var baseUri = new Uri(
                        System.IO.Path.GetDirectoryName(fontPath)! + System.IO.Path.DirectorySeparatorChar);
                    var fontFile = System.IO.Path.GetFileName(fontPath);
                    var fontFamily = new System.Windows.Media.FontFamily(baseUri, $"./{fontFile}");
                    UIFontFamily = fontFamily;
                    Logger.Info("WPF", $"Custom font loaded: {System.IO.Path.GetFileName(fontPath)}");
                    return;
                }
            }

            // 尝试系统已安装字体
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
}
