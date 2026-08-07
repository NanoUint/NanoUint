using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;

namespace NanoUint.Debugging.UE;

/// <summary>UnityExplorer UEPanel 1:1 复刻。</summary>
internal class UEPanel
{
    private const double DefaultCanvasW = 1280;
    private const double DefaultCanvasH = 720;

    [Flags]
    private enum ResizeFlags { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8 }

    private readonly Canvas _canvas;
    private readonly Grid _rootGrid;
    private readonly Grid _titleGrid;
    private readonly TextBlock _titleLabel;
    private readonly StackPanel _titleRight;
    private bool _dragging;
    private Point _dragOffset;

    /// <summary>面板根 Border(挂到 Canvas 的元素)。</summary>
    public Border Root { get; }

    /// <summary>内容宿主(子类把 UI 放进来)。</summary>
    public Grid ContentHost { get; }

    /// <summary>持久化键。</summary>
    public string Id { get; }

    public string Title
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = value;
    }

    public double MinPanelWidth { get; set; } = 350;
    public double MinPanelHeight { get; set; } = 75;

    /// <summary>默认锚点(占 Canvas 比例 0-1)。</summary>
    public double DefaultLeft { get; set; } = 0.1;
    public double DefaultTop { get; set; } = 0.1;
    public double DefaultWidth { get; set; } = 0.3;
    public double DefaultHeight { get; set; } = 0.5;

    /// <summary>标题栏关闭按钮被点击(面板已隐藏)。</summary>
    public event Action? Closed;

    public bool IsVisible => Root.Visibility == Visibility.Visible;

    /// <summary>标题栏右侧附加控件容器(Inspector 的 Mouse Inspect / Close All 用)。</summary>
    public StackPanel TitleRightControls => _titleRight;

    public UEPanel(Canvas parentCanvas, string id, string title, Color contentBackground, int zIndex = 9999)
    {
        _canvas = parentCanvas;
        Id = id;

        Root = new Border
        {
            BorderBrush = new SolidColorBrush(UEPalette.InspectorBorder),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(contentBackground),
            ClipToBounds = true,
            Visibility = Visibility.Collapsed,
        };
        Panel.SetZIndex(Root, zIndex);

        _rootGrid = new Grid();
        Root.Child = _rootGrid;

        #region 内容层: 标题栏 + 内容
        var contentGrid = new Grid();
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(UEPalette.TitleBarHeight) });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _rootGrid.Children.Add(contentGrid);

        // 标题栏 (25px, #0F0F0F, 整栏拖拽)
        var titleBar = new Border
        {
            Background = new SolidColorBrush(UEPalette.PanelTitleBar),
            Cursor = Cursors.SizeAll,
        };
        _titleGrid = new Grid();
        _titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _titleLabel = new TextBlock
        {
            FontSize = 12,
            FontFamily = UEPalette.DefaultFont,
            Foreground = new SolidColorBrush(UEPalette.TextDefault),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = title,
        };
        Grid.SetColumn(_titleLabel, 0);
        _titleGrid.Children.Add(_titleLabel);

        // 标题栏右侧附加控件区
        _titleRight = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(_titleRight, 1);
        _titleGrid.Children.Add(_titleRight);

        // 关闭按钮 "—" (30x25, #54524F)
        var closeBtn = UEFactory.Button("—", 30, UEPalette.TitleBarHeight, UEPalette.PanelCloseButton, 12);
        closeBtn.HorizontalAlignment = HorizontalAlignment.Right;
        closeBtn.Click += (_, _) =>
        {
            Hide();
            Closed?.Invoke();
        };
        Grid.SetColumn(closeBtn, 2);
        _titleGrid.Children.Add(closeBtn);

        titleBar.Child = _titleGrid;
        Grid.SetRow(titleBar, 0);
        contentGrid.Children.Add(titleBar);

        // 内容区
        ContentHost = new Grid { Background = new SolidColorBrush(contentBackground) };
        Grid.SetRow(ContentHost, 1);
        contentGrid.Children.Add(ContentHost);

        #endregion

        #region 拖拽
        titleBar.MouseLeftButtonDown += OnDragStart;
        titleBar.MouseLeftButtonUp += OnDragEnd;
        titleBar.MouseMove += OnDragMove;
        titleBar.MouseLeave += OnDragEnd;

        #endregion

        #region 点击置顶
        Root.MouseLeftButtonDown += (_, _) => SetAsLastSibling();

        #endregion

        #region 8 方向缩放 Thumb
        CreateResizeThumbs();

        _canvas.Children.Add(Root);

        LoadState();
        #endregion
    }

    #region 公开 API

    public void Show()
    {
        if (double.IsNaN(Root.Width) || Root.Width <= 0)
            ApplyDefaultBounds();
        Root.Visibility = Visibility.Visible;
        SetAsLastSibling();
        SaveState();
    }

    public void Hide()
    {
        Root.Visibility = Visibility.Collapsed;
        SaveState();
    }

    public void Toggle()
    {
        if (IsVisible) Hide();
        else Show();
    }

    /// <summary>置顶。</summary>
    public void SetAsLastSibling()
    {
        if (Root.Parent is Canvas c && c.Children.Count > 1)
            c.Children.Remove(Root);
        _canvas.Children.Add(Root);
    }

    /// <summary>按当前 Canvas 尺寸重新应用比例位置(窗口尺寸变化时调用)。</summary>
    public void ReapplyAnchors()
    {
        var canvasW = CanvasWidth;
        var canvasH = CanvasHeight;
        var left = Canvas.GetLeft(Root);
        var top = Canvas.GetTop(Root);
        Canvas.SetLeft(Root, Math.Clamp(left, 0, Math.Max(0, canvasW - Root.Width)));
        Canvas.SetTop(Root, Math.Clamp(top, 0, Math.Max(0, canvasH - Root.Height)));
    }

    #endregion

    #region 拖拽

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _dragOffset = e.GetPosition(Root);
        ((UIElement)sender).CaptureMouse();
    }

    private void OnDragEnd(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
        SaveState();
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;

        var pos = e.GetPosition(_canvas);
        var newLeft = Math.Clamp(pos.X - _dragOffset.X, 0, Math.Max(0, CanvasWidth - Root.Width));
        var newTop = Math.Clamp(pos.Y - _dragOffset.Y, 0, Math.Max(0, CanvasHeight - Root.Height));

        Canvas.SetLeft(Root, newLeft);
        Canvas.SetTop(Root, newTop);
    }

    #endregion

    #region 缩放

    private void CreateResizeThumbs()
    {
        var layer = new Grid();
        _rootGrid.Children.Add(layer);

        // 四边
        AddResizeThumb(layer, HorizontalAlignment.Stretch, VerticalAlignment.Top,
            double.NaN, UEPalette.ResizeThickness, Cursors.SizeNS, ResizeFlags.Top);
        AddResizeThumb(layer, HorizontalAlignment.Stretch, VerticalAlignment.Bottom,
            double.NaN, UEPalette.ResizeThickness, Cursors.SizeNS, ResizeFlags.Bottom);
        AddResizeThumb(layer, HorizontalAlignment.Left, VerticalAlignment.Stretch,
            UEPalette.ResizeThickness, double.NaN, Cursors.SizeWE, ResizeFlags.Left);
        AddResizeThumb(layer, HorizontalAlignment.Right, VerticalAlignment.Stretch,
            UEPalette.ResizeThickness, double.NaN, Cursors.SizeWE, ResizeFlags.Right);
        // 四角
        AddResizeThumb(layer, HorizontalAlignment.Left, VerticalAlignment.Top,
            UEPalette.ResizeThickness, UEPalette.ResizeThickness, Cursors.SizeNWSE, ResizeFlags.Left | ResizeFlags.Top);
        AddResizeThumb(layer, HorizontalAlignment.Right, VerticalAlignment.Top,
            UEPalette.ResizeThickness, UEPalette.ResizeThickness, Cursors.SizeNESW, ResizeFlags.Right | ResizeFlags.Top);
        AddResizeThumb(layer, HorizontalAlignment.Left, VerticalAlignment.Bottom,
            UEPalette.ResizeThickness, UEPalette.ResizeThickness, Cursors.SizeNESW, ResizeFlags.Left | ResizeFlags.Bottom);
        AddResizeThumb(layer, HorizontalAlignment.Right, VerticalAlignment.Bottom,
            UEPalette.ResizeThickness, UEPalette.ResizeThickness, Cursors.SizeNWSE, ResizeFlags.Right | ResizeFlags.Bottom);
    }

    private void AddResizeThumb(Grid layer, HorizontalAlignment ha, VerticalAlignment va,
        double width, double height, Cursor cursor, ResizeFlags flags)
    {
        var thumb = new Thumb
        {
            HorizontalAlignment = ha,
            VerticalAlignment = va,
            Cursor = cursor,
            IsTabStop = false,
        };
        if (!double.IsNaN(width)) thumb.Width = width;
        if (!double.IsNaN(height)) thumb.Height = height;

        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        thumb.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = border };

        thumb.DragDelta += (_, e) => Resize(flags, e.HorizontalChange, e.VerticalChange);
        layer.Children.Add(thumb);
    }

    private void Resize(ResizeFlags flags, double dx, double dy)
    {
        var canvasW = CanvasWidth;
        var canvasH = CanvasHeight;

        var left = Canvas.GetLeft(Root);
        var top = Canvas.GetTop(Root);
        var width = Root.Width;
        var height = Root.Height;

        if ((flags & ResizeFlags.Left) != 0)
        {
            var newLeft = Math.Clamp(left + dx, 0, Math.Max(0, left + width - MinPanelWidth));
            width = width + left - newLeft;
            left = newLeft;
        }
        else if ((flags & ResizeFlags.Right) != 0)
        {
            width = Math.Clamp(width + dx, MinPanelWidth, Math.Max(MinPanelWidth, canvasW - left));
        }

        if ((flags & ResizeFlags.Top) != 0)
        {
            var newTop = Math.Clamp(top + dy, 0, Math.Max(0, top + height - MinPanelHeight));
            height = height + top - newTop;
            top = newTop;
        }
        else if ((flags & ResizeFlags.Bottom) != 0)
        {
            height = Math.Clamp(height + dy, MinPanelHeight, Math.Max(MinPanelHeight, canvasH - top));
        }

        Canvas.SetLeft(Root, left);
        Canvas.SetTop(Root, top);
        Root.Width = width;
        Root.Height = height;
        SaveState();
    }

    #endregion

    #region 尺寸

    private double CanvasWidth => _canvas.ActualWidth > 0 ? _canvas.ActualWidth : DefaultCanvasW;
    private double CanvasHeight => _canvas.ActualHeight > 0 ? _canvas.ActualHeight : DefaultCanvasH;

    private void ApplyDefaultBounds()
    {
        Canvas.SetLeft(Root, DefaultLeft * CanvasWidth);
        Canvas.SetTop(Root, DefaultTop * CanvasHeight);
        Root.Width = Math.Max(MinPanelWidth, DefaultWidth * CanvasWidth);
        Root.Height = Math.Max(MinPanelHeight, DefaultHeight * CanvasHeight);
    }

    #endregion

    #region 持久化

    private static string StatePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NanoUint", "ue_panels.json");

    private sealed class PanelState
    {
        public bool Active { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    private sealed class StateFile
    {
        public Dictionary<string, PanelState> Panels { get; set; } = new();
    }

    private void SaveState()
    {
        try
        {
            var dir = Path.GetDirectoryName(StatePath);
            if (dir != null) Directory.CreateDirectory(dir);

            StateFile file;
            if (File.Exists(StatePath))
                file = JsonConvert.DeserializeObject<StateFile>(File.ReadAllText(StatePath)) ?? new StateFile();
            else
                file = new StateFile();

            var canvasW = CanvasWidth;
            var canvasH = CanvasHeight;
            file.Panels[Id] = new PanelState
            {
                Active = IsVisible,
                Left = Canvas.GetLeft(Root) / canvasW,
                Top = Canvas.GetTop(Root) / canvasH,
                Width = Root.Width / canvasW,
                Height = Root.Height / canvasH,
            };
            File.WriteAllText(StatePath, JsonConvert.SerializeObject(file, Formatting.Indented));
        }
        catch
        {
            // 持久化失败不致命
        }
    }

    private void LoadState()
    {
        try
        {
            if (File.Exists(StatePath))
            {
                var file = JsonConvert.DeserializeObject<StateFile>(File.ReadAllText(StatePath));
                if (file != null && file.Panels.TryGetValue(Id, out var st))
                {
                    Canvas.SetLeft(Root, st.Left * CanvasWidth);
                    Canvas.SetTop(Root, st.Top * CanvasHeight);
                    Root.Width = Math.Max(MinPanelWidth, st.Width * CanvasWidth);
                    Root.Height = Math.Max(MinPanelHeight, st.Height * CanvasHeight);
                    if (st.Active) Show();
                    return;
                }
            }
            ApplyDefaultBounds();
        }
        catch
        {
            ApplyDefaultBounds();
        }
    }
    #endregion
}
