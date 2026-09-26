using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NanoUint.Diagnostics;
using Newtonsoft.Json;

namespace NanoUint.Debugging.UE;

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

    public Border Root { get; }

    public Grid ContentHost { get; }

    public string Id { get; }

    public string Title
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = value;
    }

    public double MinPanelWidth { get; set; } = 350;
    public double MinPanelHeight { get; set; } = 75;

    public double DefaultLeft { get; set; } = 0.1;
    public double DefaultTop { get; set; } = 0.1;
    public double DefaultWidth { get; set; } = 0.3;
    public double DefaultHeight { get; set; } = 0.5;

    public event Action? Closed;

    public bool IsVisible => Root.Visibility == Visibility.Visible;

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

        #region Content layer: title bar + content
        var contentGrid = new Grid();
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(UEPalette.TitleBarHeight) });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _rootGrid.Children.Add(contentGrid);

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

        _titleRight = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(_titleRight, 1);
        _titleGrid.Children.Add(_titleRight);

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

        ContentHost = new Grid { Background = new SolidColorBrush(contentBackground) };
        Grid.SetRow(ContentHost, 1);
        contentGrid.Children.Add(ContentHost);

        #endregion

        #region Dragging
        titleBar.MouseLeftButtonDown += OnDragStart;
        titleBar.MouseLeftButtonUp += OnDragEnd;
        titleBar.MouseMove += OnDragMove;
        titleBar.MouseLeave += OnDragEnd;

        #endregion

        #region Click to bring to front
        Root.MouseLeftButtonDown += (_, _) => SetAsLastSibling();

        #endregion

        #region 8-direction resize thumbs
        CreateResizeThumbs();

        _canvas.Children.Add(Root);

        LoadState();
        #endregion
    }

    #region Public API

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

    public void SetAsLastSibling()
    {
        if (Root.Parent is Canvas c && c.Children.Count > 1)
            c.Children.Remove(Root);
        _canvas.Children.Add(Root);
    }

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

    #region Dragging

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

    #region Resize

    private void CreateResizeThumbs()
    {
        var layer = new Grid();
        _rootGrid.Children.Add(layer);

        AddResizeThumb(layer, HorizontalAlignment.Stretch, VerticalAlignment.Top,
            double.NaN, UEPalette.ResizeThickness, Cursors.SizeNS, ResizeFlags.Top);
        AddResizeThumb(layer, HorizontalAlignment.Stretch, VerticalAlignment.Bottom,
            double.NaN, UEPalette.ResizeThickness, Cursors.SizeNS, ResizeFlags.Bottom);
        AddResizeThumb(layer, HorizontalAlignment.Left, VerticalAlignment.Stretch,
            UEPalette.ResizeThickness, double.NaN, Cursors.SizeWE, ResizeFlags.Left);
        AddResizeThumb(layer, HorizontalAlignment.Right, VerticalAlignment.Stretch,
            UEPalette.ResizeThickness, double.NaN, Cursors.SizeWE, ResizeFlags.Right);
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

    #region Size

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

    #region Persistence

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
        catch (Exception ex)
        {
            Logger.Warning("UE", $"Panel state save failed: {ex.Message}");
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
        catch (Exception ex)
        {
            Logger.Warning("UE", $"Panel state load failed, using default bounds: {ex.Message}");
            ApplyDefaultBounds();
        }
    }
    #endregion
}
