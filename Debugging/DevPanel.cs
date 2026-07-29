using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NanoUint.Diagnostics;

namespace NanoUint.Debugging;

/// <summary>
/// 运行时调试面板（类似 UnityExplorer）。
/// F3 切换显示/隐藏。包含 Scene Tree、Inspector、Logs 三个 Tab。
/// 可拖拽标题栏移动面板位置。
/// 放在 NanoUint 引擎内部（internal），由 WpfEngineHost 创建和管理。
/// </summary>
internal sealed class DevPanel
{
    private readonly Canvas _parentCanvas;
    private Border? _panel;
    private Grid? _tabContent;
    private bool _visible;

    // Tabs
    private readonly SceneTreeTab _sceneTreeTab;
    private readonly InspectorTab _inspectorTab;
    private readonly LogTab _logTab;
    private readonly (string Label, UIElement Content)[] _tabs;
    private int _activeTabIndex;
    private Border?[] _tabButtons = Array.Empty<Border>();

    // 拖拽状态
    private bool _isDragging;
    private Point _dragOffset;

    // 吸附边缘
    private double _snapRight = 8;
    private double _snapTop = 8;

    public DevPanel(Canvas parentCanvas)
    {
        _parentCanvas = parentCanvas;

        _sceneTreeTab = new SceneTreeTab();
        _inspectorTab = new InspectorTab();
        _logTab = new LogTab();

        _tabs = new (string, UIElement)[]
        {
            ("Scene Tree", _sceneTreeTab.Build()),
            ("Inspector", _inspectorTab.Build()),
            ("Logs", _logTab.Build()),
        };

        // 选中对象 → 自动切到 Inspector
        _sceneTreeTab.OnGameObjectSelected += go =>
        {
            _inspectorTab.Inspect(go);
            SwitchTab(1);
        };
    }

    // ── 公开 API ──

    /// <summary>切换显示/隐藏。</summary>
    public void Toggle()
    {
        if (_panel == null) CreateUI();

        _visible = !_visible;
        _panel!.Visibility = _visible ? Visibility.Visible : Visibility.Collapsed;

        if (_visible)
        {
            _sceneTreeTab.Refresh();
            Show();
        }
    }

    /// <summary>每帧刷新（由引擎主循环调用）。</summary>
    public void AutoRefresh()
    {
        if (!_visible || _panel == null) return;
        _sceneTreeTab.AutoRefresh();
    }

    public bool IsVisible => _visible;

    // ── UI 构建 ──

    private void CreateUI()
    {
        // ── Tab 按钮栏 ──
        var tabBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 20, 30)),
        };

        _tabButtons = new Border[_tabs.Length];
        for (int i = 0; i < _tabs.Length; i++)
        {
            var idx = i;
            var btnBorder = new Border
            {
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand,
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 80)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Background = idx == 0
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(35, 35, 50))
                    : Brushes.Transparent,
            };
            var btnLabel = new TextBlock
            {
                Text = _tabs[i].Label,
                FontSize = 12,
                Foreground = idx == 0 ? Brushes.Cyan : Brushes.Gray,
                FontWeight = idx == 0 ? FontWeights.Bold : FontWeights.Normal,
            };
            btnBorder.Child = btnLabel;
            btnBorder.MouseLeftButtonDown += (_, _) => SwitchTab(idx);
            tabBar.Children.Add(btnBorder);
            _tabButtons[i] = btnBorder;
        }

        // ── Tab 内容区域 ──
        _tabContent = new Grid();
        foreach (var (_, content) in _tabs)
        {
            content.Visibility = Visibility.Collapsed;
            _tabContent.Children.Add(content);
        }
        if (_tabs.Length > 0)
            _tabs[0].Content.Visibility = Visibility.Visible;

        // ── 标题栏（可拖拽） ──
        var titleBar = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(25, 25, 40)),
            Padding = new Thickness(8, 4, 8, 4),
            Cursor = Cursors.SizeAll,
        };
        var titleGrid = new Grid();
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleLabel = new TextBlock
        {
            Text = "NanoUint Dev [F3=hide] [F2=inspect]",
            Foreground = Brushes.Cyan,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(titleLabel, 0);
        titleGrid.Children.Add(titleLabel);

        // F2 快速切换按钮
        var inspectBtn = new Button
        {
            Content = "🔍",
            FontSize = 12,
            Width = 28, Height = 22,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = Brushes.LightGray,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "快速切换到 Inspector (F2)",
        };
        inspectBtn.Click += (_, _) =>
        {
            if (_activeTabIndex == 1) SwitchTab(0); else SwitchTab(1);
        };
        Grid.SetColumn(inspectBtn, 1);
        titleGrid.Children.Add(inspectBtn);

        // 关闭按钮
        var closeBtn = new Button
        {
            Content = "✕",
            FontSize = 12,
            Width = 28, Height = 22,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = Brushes.LightGray,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
        };
        closeBtn.Click += (_, _) => Toggle();
        Grid.SetColumn(closeBtn, 2);
        titleGrid.Children.Add(closeBtn);

        titleBar.Child = titleGrid;

        // 拖拽事件
        titleBar.MouseLeftButtonDown += OnDragStart;
        titleBar.MouseLeftButtonUp += OnDragEnd;
        titleBar.MouseMove += OnDragMove;
        titleBar.MouseLeave += OnDragEnd;

        // ── 组装 ──
        var rootStack = new StackPanel();
        rootStack.Children.Add(titleBar);
        rootStack.Children.Add(tabBar);
        rootStack.Children.Add(_tabContent);

        _panel = new Border
        {
            Width = 600,
            Height = 420,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(230, 10, 10, 18)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 80)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = rootStack,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = true,
            ClipToBounds = true,
        };

        // 初始定位：右上角
        Canvas.SetRight(_panel, _snapRight);
        Canvas.SetTop(_panel, _snapTop);
        Panel.SetZIndex(_panel, 9999);

        _parentCanvas.Children.Add(_panel);

        // 初始化第一个 tab
        SwitchTab(0);
    }

    private void Show()
    {
        // 右键边缘吸附
        Canvas.SetRight(_panel!, _snapRight);
        Canvas.SetTop(_panel!, _snapTop);
        _sceneTreeTab.AutoRefresh();
    }

    // ── Tab 切换 ──

    private void SwitchTab(int index)
    {
        if (_tabContent == null || index < 0 || index >= _tabs.Length) return;
        _activeTabIndex = index;

        for (int i = 0; i < _tabs.Length; i++)
            _tabs[i].Content.Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;

        for (int i = 0; i < _tabButtons!.Length; i++)
        {
            var border = _tabButtons[i];
            if (border == null) continue;
            border.Background = i == index
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(35, 35, 50))
                : Brushes.Transparent;
            if (border.Child is TextBlock tb)
            {
                tb.Foreground = i == index ? Brushes.Cyan : Brushes.Gray;
                tb.FontWeight = i == index ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        if (index == 0) _sceneTreeTab.Refresh();
    }

    // ── 拖拽 ──

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        if (_panel == null) return;
        _isDragging = true;
        _dragOffset = e.GetPosition(_panel);
        (sender as UIElement)?.CaptureMouse();
    }

    private void OnDragEnd(object sender, MouseEventArgs e)
    {
        _isDragging = false;
        (sender as UIElement)?.ReleaseMouseCapture();
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _panel == null) return;

        var pos = e.GetPosition(_parentCanvas);
        var newLeft = pos.X - _dragOffset.X;
        var newTop = pos.Y - _dragOffset.Y;

        // 限制在 Canvas 范围内
        var canvasW = _parentCanvas.ActualWidth > 0 ? _parentCanvas.ActualWidth : 1280;
        var canvasH = _parentCanvas.ActualHeight > 0 ? _parentCanvas.ActualHeight : 720;
        newLeft = Math.Clamp(newLeft, 0, canvasW - _panel.Width);
        newTop = Math.Clamp(newTop, 0, canvasH - _panel.Height);

        Canvas.SetLeft(_panel, newLeft);
        Canvas.SetTop(_panel, newTop);

        // 清除 Right 定位（避免冲突）
        Canvas.SetRight(_panel, double.NaN);

        // 记录吸附位置
        _snapRight = canvasW - newLeft - _panel.Width;
        _snapTop = newTop;
    }
}
