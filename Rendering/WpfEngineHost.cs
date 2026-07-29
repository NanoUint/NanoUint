using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NanoUint.Debugging;
using NanoUint.Diagnostics;

namespace NanoUint.Rendering;

/// <summary>
/// 引擎内部的 WPF 宿主。创建 Window + Canvas，驱动主循环。
/// 全部 internal —— 游戏开发者不可见。
/// </summary>
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
    private DevPanel? _devPanel;

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

        // ── 主循环：CompositionTarget.Rendering ──
        // 在 WPF 渲染前触发（Render 优先级）。同时处理游戏逻辑和渲染同步。
        // 改用增量渲染（UpdateDirtyComponents）后不再每帧重建控件树，
        // 因此不会像之前那样与 WPF hit-test 冲突。
        CompositionTarget.Rendering += OnFrame;

        _renderer = new WpfRenderer(_rootCanvas);
        _mainScene = new Scene("Main");
        _renderer.SetActiveScene(_mainScene);
        SceneManager.LoadScene(_mainScene);
        _devPanel = new DevPanel(_rootCanvas);

        _window.Loaded += OnLoaded;
        _window.Closed += OnClosed;
        _window.Activated += (s, e) =>
        {
            _window.Focus();
            Logger.Trace("WPF", "Window activated + focused");
        };

        Logger.Info("WPF", "WpfEngineHost starting WPF Application.Run()");
        app.Run(_window);
    }

    public void Shutdown()
    {
        Logger.Info("WPF", "Shutdown requested");
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            System.Windows.Application.Current.Shutdown();
        });
    }

    // ── 事件处理 ──

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _lastFrame = DateTime.UtcNow;
        Logger.Info("WPF", $"Window loaded. Size: {_window!.ActualWidth:F0}x{_window.ActualHeight:F0}");

        // 样式资源（引擎内嵌暗色主题）
        LoadEmbeddedStyles();

        // 调用游戏启动器
        Logger.Info("WPF", "Calling IGameBootstrapper.OnStart()");
        _bootstrapper.OnStart(_mainScene!);

        // 触发所有 Awake + Start
        foreach (var go in _mainScene!.RootObjects)
            go.NotifyStart();

        _isRunning = true;
        Logger.Info("WPF", $"Engine started. {_mainScene.RootObjects.Count} root objects. Frame loop via CompositionTarget.Rendering.");
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Logger.Info("WPF", "Window closed — stopping engine");
        _isRunning = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        Logger.Trace("Input", $"KeyDown: {e.Key}");
        switch (e.Key)
        {
            case Key.Space:
            case Key.Enter:
            case Key.Right:
                InputManager.FeedAdvancePress();
                // 立即推进等待输入的协程（不等下一帧），消除 16ms+ 的输入延迟
                CoroutineScheduler.Instance.Tick(0f);
                _renderer?.UpdateDirtyComponents();
                // 消费本帧输入 — 防止 OnFrame 中的 Tick 再次看到同一帧 Enter
                // （否则 TypewriterDelay 会被立即触发，导致新对白被秒跳）
                InputManager.ConsumeAdvancePress();
                break;
            case Key.A:
                InputManager.FeedAutoToggle();
                break;
            case Key.LeftCtrl:
            case Key.RightCtrl:
            case Key.LeftShift:
            case Key.RightShift:
                InputManager.FeedSkipState(true);
                break;
            case Key.F3:
                Logger.Info("Input", "F3: Toggle DevPanel");
                _devPanel?.Toggle();
                break;
            case Key.F2:
                Logger.Info("Input", "F2: Quick toggle DevPanel Inspector");
                _devPanel?.Toggle();
                break;
            case Key.F5:
                Logger.Info("Input", "F5: Quick Save");
                InputManager.FireQuickSave();
                break;
            case Key.F9:
                Logger.Info("Input", "F9: Quick Load");
                InputManager.FireQuickLoad();
                break;
            case Key.Escape:
                Logger.Info("Input", "Esc: System menu");
                InputManager.FeedMenuPress();
                break;
            case Key.PageUp:
                Logger.Info("Input", "PageUp: Backlog");
                InputManager.FireBacklog();
                break;
        }
    }

    private void OnKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
            e.Key == Key.LeftShift || e.Key == Key.RightShift)
        {
            InputManager.FeedSkipState(false);
        }
    }

    private void OnCanvasClick(object sender, MouseButtonEventArgs e)
    {
        Logger.Trace("Input", $"Canvas click: OriginalSource={e.OriginalSource?.GetType().Name}, " +
            $"Source={e.Source?.GetType().Name}, Position={e.GetPosition(_rootCanvas)}");

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

    // ── 主循环 ──

    /// <summary>每帧主循环（CompositionTarget.Rendering）。
    /// 1. 更新场景 + 协程 → MarkDirty()
    /// 2. 增量渲染同步 → 刷新脏组件到 WPF
    /// 3. 清理帧内输入状态</summary>
    private void OnFrame(object? sender, EventArgs e)
    {
        if (!_isRunning || _mainScene == null) return;

        try
        {
            var now = DateTime.UtcNow;
            var dt = Math.Min((float)(now - _lastFrame).TotalSeconds, 0.1f); // clamp
            _lastFrame = now;

            // 1. 更新场景：GameObject.Update + Coroutine 推进
            //    协程中可能调用 MarkDirty()
            _mainScene.Update(dt);

            // 2. 增量渲染：只同步有变化的 Component 到 WPF 控件
            _renderer!.UpdateDirtyComponents();

            // 2.5 DevPanel 自动刷新（Scene Tree 等）
            _devPanel?.AutoRefresh();

            // 3. 清理帧内输入状态
            InputManager.EndFrame();
        }
        catch (Exception ex)
        {
            Logger.Error("Engine", "Frame exception", ex);
        }
    }

    // ── 窗口控制（供 ScreenManager 调用） ──

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

    // ── 视频播放 ──

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

    // ── 内嵌样式 ──

    private void LoadEmbeddedStyles()
    {
        try
        {
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
}
