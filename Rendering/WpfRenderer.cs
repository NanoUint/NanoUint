using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NanoUint.Diagnostics;

namespace NanoUint.Rendering;

internal sealed class WpfRenderer
{
    private readonly Canvas _canvas;
    private Scene? _scene;
    private readonly Dictionary<Component, UIElement> _visualMap = new();
    private readonly Dictionary<Component, IRenderNode> _nodeMap = new();
    private readonly Dictionary<Component, int> _lastVersion = new();
    private readonly HashSet<Component> _knownComponents = new();
    private readonly RendererRegistry _registry = new();
    private double _lastCanvasWidth;
    private double _lastCanvasHeight;
    private float _lastScaleFactor = 1f;

    private CanvasScaler? _canvasScaler;
    private double _effectiveWidth;
    private double _effectiveHeight;

    private GameObject? _hoveredGo;
    private static Point _lastMousePos;
    private static readonly Type[] PointerHandlerTypes =
    {
        typeof(EventSystems.IPointerEnterHandler),
        typeof(EventSystems.IPointerExitHandler),
        typeof(EventSystems.IPointerDownHandler),
        typeof(EventSystems.IPointerUpHandler),
        typeof(EventSystems.IPointerClickHandler),
    };

    private Border? _hintPopup;
    private TextBlock? _hintText;
    private string? _lastHintText;

    internal static Point LastMousePosition
    {
        get => _lastMousePos;
        set => _lastMousePos = value;
    }

    public WpfRenderer(Canvas canvas)
    {
        _canvas = canvas;
        RegisterDefaults();
    }

    public RendererRegistry Registry => _registry;

    private void RegisterDefaults()
    {
        _registry.Register(new DelegateRenderer<FlashOverlay>(10, CreateOverlayElement, (c, e) => UpdateFlashOverlay(c, e)));
        _registry.Register(new DelegateRenderer<SpriteRenderer>(20, CreateImageElement, (c, e) => UpdateSpriteRenderer(c, e)));
        _registry.Register(new DelegateRenderer<BackgroundRenderer>(30, CreateBackgroundElement, (c, e) => UpdateBackgroundRenderer(c, e)));
        _registry.Register(new DelegateRenderer<DialogueBox>(40, CreateDialogueBoxElement, (c, e) => UpdateDialogueBox(c, e)));
        _registry.Register(new DelegateRenderer<ChoiceGroup>(50, CreateChoiceGroupElement, (c, e) => UpdateChoiceGroup(c, e)));
        _registry.Register(new DelegateRenderer<TextRenderer>(60, CreateTextElement, (c, e) => UpdateTextRenderer(c, e)));
        _registry.Register(new DelegateRenderer<BacklogView>(70, CreateBacklogElement, (c, e) => UpdateBacklogView(c, e)));
        _registry.Register(new DelegateRenderer<AdvanceIndicator>(80, CreateAdvanceIndicatorElement, (c, e) => UpdateAdvanceIndicator(c, e)));
        _registry.Register(new DelegateRenderer<Slider>(90, CreateSliderElement, (c, e) => UpdateSlider(c, e)));
        _registry.Register(new DelegateRenderer<LineRenderer>(100, CreateLineRendererElement, (c, e) => UpdateLineRenderer(c, e)));
    }

    public void SetActiveScene(Scene scene)
    {
        _scene = scene;
        _canvas.Children.Clear();
        _visualMap.Clear();
        _lastVersion.Clear();
        _knownComponents.Clear();
    }

    public void UpdateDirtyComponents()
    {
        if (_scene == null) return;

        var currentW = _canvas.ActualWidth;
        var currentH = _canvas.ActualHeight;
        bool sizeChanged = Math.Abs(currentW - _lastCanvasWidth) > 0.5 || Math.Abs(currentH - _lastCanvasHeight) > 0.5;

        if (_canvasScaler == null || _canvasScaler.IsDestroyed)
            _canvasScaler = FindCanvasScaler();

        if (_canvasScaler != null && _canvasScaler.ScaleMode == CanvasScaleMode.ScaleWithScreenSize)
        {
            float scale = _canvasScaler.ComputeScaleFactor(currentW, currentH);
            if (sizeChanged || Math.Abs(scale - _lastScaleFactor) > 0.001f)
            {
                _lastScaleFactor = scale;
                _effectiveWidth = currentW / scale;
                _effectiveHeight = currentH / scale;
                _canvas.RenderTransform = new ScaleTransform(scale, scale, 0, 0);
                _canvas.RenderTransformOrigin = new Point(0, 0);
                _lastVersion.Clear();
            }
        }
        else
        {
            _effectiveWidth = currentW;
            _effectiveHeight = currentH;
            if (_lastScaleFactor != 1f)
            {
                _lastScaleFactor = 1f;
                _canvas.RenderTransform = System.Windows.Media.Transform.Identity;
                _lastVersion.Clear();
            }
        }

        if (sizeChanged)
        {
            _lastCanvasWidth = currentW;
            _lastCanvasHeight = currentH;
            if (_canvasScaler == null)
                _lastVersion.Clear();
        }

        var currentComponents = new HashSet<Component>();
        foreach (var go in _scene.RootObjects)
        {
            if (!go.ActiveSelf || go.IsDestroyed) continue;
            foreach (var comp in go.Components)
            {
                if (comp.IsDestroyed || comp is Transform) continue;
                currentComponents.Add(comp);
            }
        }

        var toRemove = new List<Component>();
        foreach (var comp in _knownComponents)
        {
            if (!currentComponents.Contains(comp) || comp.IsDestroyed || comp.GameObject?.ActiveSelf != true)
                toRemove.Add(comp);
        }
        foreach (var comp in toRemove)
        {
            RemoveVisual(comp);
            _knownComponents.Remove(comp);
            _lastVersion.Remove(comp);
        }

        foreach (var comp in currentComponents)
        {
            if (!_knownComponents.Contains(comp))
            {
                var visual = CreateVisual(comp);
                if (visual != null)
                {
                    _visualMap[comp] = visual;
                    _canvas.Children.Add(visual);
                    _knownComponents.Add(comp);
                    if (UpdateVisual(comp, visual))
                        _lastVersion[comp] = comp.RenderVersion;
                }
            }
            else if (_visualMap.TryGetValue(comp, out var existing))
            {
                var lastVer = _lastVersion.GetValueOrDefault(comp, -1);
                if (comp.RenderVersion != lastVer)
                {
                    if (UpdateVisual(comp, existing))
                        _lastVersion[comp] = comp.RenderVersion;
                    // If UpdateVisual returns false (bitmap still decoding), keep the version so we retry next frame.
                }
            }
        }

        UpdateHintTooltip();
    }

    private UIElement? CreateVisual(Component comp)
    {
        if (_registry.TryGet(comp, out var renderer) && renderer.TryCreate(comp, out var node))
        {
            if (node != null)
                _nodeMap[comp] = node;
            return node?.Element;
        }

        return null;
    }

    private bool UpdateVisual(Component comp, UIElement element)
    {
        var transform = comp.GameObject?.Transform;
        if (transform == null) return true;

        Canvas.SetZIndex(element, transform.SortingOrder);

        bool typeUpdateOk = true;

        if (_nodeMap.TryGetValue(comp, out var node))
        {
            if (comp is PhoneScreen ps)
            {
                ps.RenderCanvasW = _effectiveWidth;
                ps.RenderCanvasH = _effectiveHeight;
            }
            node.Sync(comp);
            var key = new SortingKey { SortingOrder = transform.SortingOrder };
            node.SetSorting(key);
        }

        if (element is FrameworkElement fe)
        {
            fe.Opacity = transform.Opacity;

            if (transform.FlipX)
                fe.RenderTransform = new ScaleTransform(-1, 1, fe.ActualWidth > 0 ? fe.ActualWidth / 2 : 100, 0);
            else
                fe.RenderTransform = System.Windows.Media.Transform.Identity;

            if (comp is DialogueBox or FlashOverlay or AdvanceIndicator or BackgroundRenderer
                || (comp is SpriteRenderer fullscreenSr && fullscreenSr.FullScreen))
            {
                return typeUpdateOk;
            }

            var canvasW = _effectiveWidth > 0 ? _effectiveWidth : 1280;
            var canvasH = _effectiveHeight > 0 ? _effectiveHeight : 720;
            var elemW = fe.ActualWidth > 0 ? fe.ActualWidth : (fe.Width > 0 ? fe.Width : 280);
            var elemH = fe.ActualHeight > 0 ? fe.ActualHeight : (fe.Height > 0 ? fe.Height : 46);

            if (comp.GameObject?.GetComponent<RectTransform>() is { } rt)
            {
                var (left, top) = rt.ComputeCanvasPosition(canvasW, canvasH, elemW, elemH);
                Canvas.SetLeft(fe, left);
                Canvas.SetTop(fe, top);

                if (rt.IsStretchX)
                {
                    double stretchW = canvasW * (rt.AnchorMax.X - rt.AnchorMin.X) + rt.SizeDelta.X;
                    if (stretchW > 0) fe.Width = stretchW;
                }
                if (rt.IsStretchY)
                {
                    double stretchH = canvasH * (rt.AnchorMax.Y - rt.AnchorMin.Y) + rt.SizeDelta.Y;
                    if (stretchH > 0) fe.Height = stretchH;
                }
            }
            else
            {
                Canvas.SetLeft(fe, (canvasW - elemW) * transform.WorldX);
                Canvas.SetTop(fe, (canvasH - elemH) * transform.WorldY);
            }
        }

        return typeUpdateOk;
    }

    private void RemoveVisual(Component comp)
    {
        if (_visualMap.TryGetValue(comp, out var element))
        {
            _canvas.Children.Remove(element);
            _visualMap.Remove(comp);
        }
        if (_nodeMap.TryGetValue(comp, out var node))
        {
            node.Dispose();
            _nodeMap.Remove(comp);
        }
    }

    #region Component → WPF Control Creation

    private static System.Windows.Shapes.Rectangle CreateOverlayElement()
    {
        return new System.Windows.Shapes.Rectangle
        {
            Width = double.NaN, Height = double.NaN,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false,
        };
    }

    private static Image CreateImageElement()
    {
        return new Image
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.Both,
            IsHitTestVisible = true,
        };
    }

    private static Grid CreateBackgroundElement()
    {
        return new Grid
        {
            Width = 1280, Height = 720,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false,
        };
    }

    private static Border CreateDialogueBoxElement()
    {
        var boxBg = ResourceManager.GetBitmap("dialogue/box00.png");
        var nameTagBg = ResourceManager.GetBitmap("dialogue/name01.png");

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var accentBar = new System.Windows.Shapes.Rectangle
        {
            Height = 4, Width = 120, RadiusX = 2, RadiusY = 2,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0xBF, 0xBF)),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Grid.SetRow(accentBar, 0);
        grid.Children.Add(accentBar);

        var speakerLabel = new TextBlock
        {
            Name = "SpeakerLabel",
            FontWeight = FontWeights.Bold,
            FontSize = 17,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0xBF, 0xBF)),
        };
        Grid.SetRow(speakerLabel, 1);
        grid.Children.Add(speakerLabel);

        var textContent = new TextBlock
        {
            Name = "TextContent",
            FontSize = 20,
            FontWeight = FontWeights.Normal,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xE8, 0xE8)),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 2,
        };
        Grid.SetRow(textContent, 3);
        grid.Children.Add(textContent);

        var border = new Border
        {
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            Padding = new Thickness(28, 16, 28, 20),
            MinHeight = 140,
            Child = grid,
        };

        if (boxBg != null)
        {
            border.Background = new ImageBrush(boxBg) { Stretch = Stretch.Fill };
            border.BorderBrush = new SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x50, 0x3E, 0xBF, 0xBF));
            border.BorderThickness = new Thickness(0, 1.5, 0, 0);
        }
        else
        {
            border.Background = new SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0xEE, 0x0A, 0x0C, 0x14));
            border.BorderBrush = new SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x60, 0x3E, 0xBF, 0xBF));
            border.BorderThickness = new Thickness(0, 1.5, 0, 0);
        }

        return border;
    }

    private static StackPanel CreateChoiceGroupElement()
    {
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = Brushes.Transparent,
            IsHitTestVisible = true,
        };
    }

    private static System.Windows.Controls.TextBlock CreateTextElement()
    {
        return new System.Windows.Controls.TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 18,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
        };
    }

    #endregion

    #region Component Properties → WPF Control Updates

    private bool UpdateSpriteRenderer(SpriteRenderer sr, UIElement element)
    {
        var activeSprite = sr.GetActiveMouthSprite();
        if (activeSprite == null) return true;

        if (element is Image img)
        {
            var bitmap = LoadCachedBitmap(activeSprite);
            if (bitmap != null)
            {
                img.Source = bitmap;
                // Use PixelWidth/PixelHeight (native pixels), not Width/Height (DIP, DPI-scaled):
                // a 72-DPI image's Width overstates native pixels by 33%, mis-sizing sprites.
                if (sr.FullScreen)
                {
                    var w = _effectiveWidth > 0 ? _effectiveWidth : ScreenManager.Width;
                    var h = _effectiveHeight > 0 ? _effectiveHeight : ScreenManager.Height;
                    img.Width = w;
                    img.Height = h;
                    img.MaxWidth = w;
                    img.MaxHeight = h;
                    img.Stretch = Stretch.Fill;
                    Canvas.SetLeft(img, 0);
                    Canvas.SetTop(img, 0);
                }
                else
                {
                    var nativeW = activeSprite.Width > 0 ? activeSprite.Width : (bitmap.PixelWidth > 0 ? bitmap.PixelWidth : double.NaN);
                    var nativeH = activeSprite.Height > 0 ? activeSprite.Height : (bitmap.PixelHeight > 0 ? bitmap.PixelHeight : double.NaN);
                    var ppu = activeSprite.PixelsPerUnit > 0 ? activeSprite.PixelsPerUnit : 100f;
                    var scale = sr.Scale;
                    img.Width = nativeW * 100.0 / ppu * scale;
                    img.Height = nativeH * 100.0 / ppu * scale;
                    img.Stretch = Stretch.Uniform;
                    img.MaxHeight = (_effectiveHeight > 0 ? _effectiveHeight : 1080) * 0.78 * scale;
                }
            }
            else { sr.MarkDirty(); return false; }
            img.Opacity = sr.Tint.A / 255f * (sr.GameObject?.Transform.Opacity ?? 1f);

            if (img.Tag is not "mouse_hooked")
            {
                HookSpriteMouseEvents(img, sr);
            }
        }
        return true;
    }

    private void HookSpriteMouseEvents(Image img, SpriteRenderer sr)
    {
        if (!HasPointerHandlers(sr.GameObject)) return;

        img.Tag = "mouse_hooked";

        img.MouseEnter += (_, e) =>
        {
            if (sr.IsDestroyed || sr.GameObject == null) return;
            _hoveredGo = sr.GameObject;
            DispatchPointerEvent<EventSystems.IPointerEnterHandler>(sr.GameObject, h => h.OnPointerEnter());
            e.Handled = true;
        };

        img.MouseLeave += (_, e) =>
        {
            if (sr.IsDestroyed || sr.GameObject == null) return;
            if (_hoveredGo == sr.GameObject) _hoveredGo = null;
            DispatchPointerEvent<EventSystems.IPointerExitHandler>(sr.GameObject, h => h.OnPointerExit());
        };

        img.MouseLeftButtonDown += (_, e) =>
        {
            if (sr.IsDestroyed || sr.GameObject == null) return;
            DispatchPointerEvent<EventSystems.IPointerDownHandler>(sr.GameObject, h => h.OnPointerDown());
        };

        img.MouseLeftButtonUp += (_, e) =>
        {
            if (sr.IsDestroyed || sr.GameObject == null) return;
            DispatchPointerEvent<EventSystems.IPointerUpHandler>(sr.GameObject, h => h.OnPointerUp());
            DispatchPointerEvent<EventSystems.IPointerClickHandler>(sr.GameObject, h => h.OnPointerClick());
        };
    }

    private static bool HasPointerHandlers(GameObject? go)
    {
        if (go == null) return false;
        foreach (var comp in go.Components)
        {
            if (!comp.Enabled || comp.IsDestroyed) continue;
            foreach (var t in PointerHandlerTypes)
                if (t.IsAssignableFrom(comp.GetType()))
                    return true;
        }
        return false;
    }

    private static void DispatchPointerEvent<T>(GameObject go, Action<T> action)
    {
        foreach (var comp in go.Components)
        {
            if (!comp.Enabled || comp.IsDestroyed || comp is Transform) continue;
            if (comp is T handler) action(handler);
        }
    }

    private bool UpdateFlashOverlay(FlashOverlay overlay, UIElement element)
    {
        if (element is System.Windows.Shapes.Rectangle rect)
        {
            var w = _effectiveWidth > 0 ? _effectiveWidth : 1280;
            var h = _effectiveHeight > 0 ? _effectiveHeight : 720;
            rect.Width = w; rect.Height = h;
            Canvas.SetLeft(rect, 0); Canvas.SetTop(rect, 0);

            // Use only the color's own alpha; Transform.Opacity is applied centrally via fe.Opacity in UpdateVisual.
            rect.Fill = new SolidColorBrush(
                System.Windows.Media.Color.FromArgb(
                    overlay.Color.A,
                    overlay.Color.R, overlay.Color.G, overlay.Color.B));
            rect.Opacity = 1;
        }
        return true;
    }

    private bool UpdateBackgroundRenderer(BackgroundRenderer bg, UIElement element)
    {
        if (element is not Grid grid) return true;
        Panel.SetZIndex(grid, -100);

        var effectiveW = _effectiveWidth > 0 ? _effectiveWidth : 1280;
        var effectiveH = _effectiveHeight > 0 ? _effectiveHeight : 720;
        grid.Width = effectiveW;
        grid.Height = effectiveH;
        Canvas.SetLeft(grid, 0);
        Canvas.SetTop(grid, 0);

        if (bg.IsCrossfading && bg.PreviousSprite != null)
        {
            while (grid.Children.Count < 2)
                grid.Children.Add(new Image { Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false });

            bool allReady = true;
            if (grid.Children[0] is Image oldImg)
            {
                var oldBmp = LoadCachedBitmap(bg.PreviousSprite);
                if (oldBmp != null) oldImg.Source = oldBmp;
                else allReady = false;
                oldImg.Opacity = 1f - bg.FadeProgress;
            }
            if (grid.Children[1] is Image newImg)
            {
                var newBmp = LoadCachedBitmap(bg.Sprite!);
                if (newBmp != null) newImg.Source = newBmp;
                else allReady = false;
                newImg.Opacity = bg.FadeProgress;
            }
            if (!allReady) { bg.MarkDirty(); return false; }
        }
        else if (bg.Sprite != null)
        {
            while (grid.Children.Count > 2)
                grid.Children.RemoveAt(grid.Children.Count - 1);
            if (grid.Children.Count == 0)
                grid.Children.Add(new Image { Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false });
            if (grid.Children[0] is Image img)
            {
                var bmp = LoadCachedBitmap(bg.Sprite);
                if (bmp != null) { img.Source = bmp; img.Opacity = 1f; }
                else { bg.MarkDirty(); return false; }
            }
        }

        if (bg.TintColor.HasValue)
        {
            while (grid.Children.Count < 2)
                grid.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    IsHitTestVisible = false,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                });
            if (grid.Children.Count > 1 && grid.Children[1] is System.Windows.Shapes.Rectangle tintRect)
            {
                var tc = bg.TintColor.Value;
                tintRect.Fill = new SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(tc.A, tc.R, tc.G, tc.B));
                tintRect.Opacity = 1;
            }
        }
        else
        {
            while (grid.Children.Count > 1 && grid.Children[^1] is System.Windows.Shapes.Rectangle)
                grid.Children.RemoveAt(grid.Children.Count - 1);
        }

        return true;
    }

    private bool UpdateDialogueBox(DialogueBox db, UIElement element)
    {
        if (element is not Border border || border.Child is not Grid grid) return true;

        var effectiveW = _effectiveWidth > 0 ? _effectiveWidth : 1280;
        var effectiveH = _effectiveHeight > 0 ? _effectiveHeight : 720;

        border.Width = effectiveW;
        Canvas.SetLeft(border, 0);
        border.Measure(new System.Windows.Size(effectiveW, double.PositiveInfinity));
        var h = border.DesiredSize.Height > 10 ? border.DesiredSize.Height : border.MinHeight;
        Canvas.SetTop(border, Math.Max(0, effectiveH - h));

        if (grid.Children.Count > 0 && grid.Children[0] is System.Windows.Shapes.Rectangle accentBar)
            accentBar.Visibility = string.IsNullOrEmpty(db.SpeakerName)
                ? Visibility.Collapsed : Visibility.Visible;

        if (grid.Children.Count > 1 && grid.Children[1] is TextBlock speakerLabel)
        {
            speakerLabel.Text = db.SpeakerName;
            speakerLabel.Visibility = string.IsNullOrEmpty(db.SpeakerName)
                ? Visibility.Collapsed : Visibility.Visible;
        }

        if (grid.Children.Count > 2 && grid.Children[2] is TextBlock textContent)
        {
            textContent.Text = db.Text;

            if (db.RevealProgress < 1f && db.Text.Length > 0)
            {
                if (textContent.OpacityMask is not LinearGradientBrush mask)
                {
                    mask = new LinearGradientBrush
                    {
                        StartPoint = new System.Windows.Point(0, 0),
                        EndPoint = new System.Windows.Point(1, 0),
                        MappingMode = BrushMappingMode.RelativeToBoundingBox,
                    };
                    mask.GradientStops.Add(new GradientStop(Colors.Black, 0));
                    mask.GradientStops.Add(new GradientStop(Colors.Black, 0));
                    mask.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
                    mask.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
                    textContent.OpacityMask = mask;
                }

                float p = db.RevealProgress;
                mask.GradientStops[0].Offset = 0;
                mask.GradientStops[1].Offset = Math.Max(0, p - 0.04);
                mask.GradientStops[2].Offset = Math.Min(1, p + 0.06);
                mask.GradientStops[3].Offset = 1;
            }
            else
            {
                textContent.OpacityMask = null;
            }
        }

        return true;
    }

    private int _choiceGroupLastCount = -1;
    private string[]? _choiceGroupLastTexts;

    private bool UpdateChoiceGroup(ChoiceGroup cg, UIElement element)
    {
        if (element is not StackPanel sp) return true;

        var w = _effectiveWidth > 10 ? _effectiveWidth : 1280;
        var h = _effectiveHeight > 10 ? _effectiveHeight : 720;

        if (cg.Inline)
        {
            sp.Orientation = Orientation.Horizontal;
        }
        else
        {
            sp.Orientation = Orientation.Vertical;
            sp.Width = 320;
            Canvas.SetLeft(sp, (w - 320) / 2);
            Canvas.SetTop(sp, Math.Max(80, (h - cg.ChoiceCount * 56) / 2));
        }

        var texts = cg.ChoiceTexts ?? Array.Empty<string>();
        bool needsRebuild = cg.ChoiceCount != _choiceGroupLastCount ||
            !TextArraysEqual(texts, _choiceGroupLastTexts);

        if (needsRebuild)
        {
            _choiceGroupLastCount = cg.ChoiceCount;
            _choiceGroupLastTexts = texts.ToArray();

            sp.Children.Clear();

            var isInline = cg.Inline;
            for (int i = 0; i < texts.Length; i++)
            {
                var idx = i;
                var btn = new System.Windows.Controls.Button
                {
                    Content = texts[i],
                    Tag = idx,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    IsHitTestVisible = true,
                    Focusable = true,
                };

                if (isInline)
                {
                    btn.Width = 120;
                    btn.Height = 36;
                    btn.FontSize = 14;
                    btn.Margin = new Thickness(4, 0, 4, 0);
                }
                else
                {
                    btn.Width = 300;
                    btn.Height = 48;
                    btn.FontSize = 16;
                    btn.Margin = new Thickness(0, 4, 0, 4);
                }

                bool isHighlighted = cg.HighlightIndex == idx;
                btn.Background = new SolidColorBrush(isHighlighted
                    ? System.Windows.Media.Color.FromRgb(62, 191, 191)
                    : System.Windows.Media.Color.FromRgb(12, 16, 22));
                btn.Foreground = new SolidColorBrush(isHighlighted
                    ? System.Windows.Media.Color.FromRgb(0, 0, 0)
                    : System.Windows.Media.Color.FromRgb(220, 240, 240));
                btn.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(62, 191, 191));
                btn.BorderThickness = new Thickness(1.5);

                btn.Click += (s, e) =>
                {
                    Diagnostics.Logger.Info("UI", $"Button [{idx}] '{texts[idx]}' clicked");
                    cg.Select(idx);
                };
                sp.Children.Add(btn);
            }
        }
        else if (cg.Inline)
        {
            for (int i = 0; i < sp.Children.Count; i++)
            {
                if (sp.Children[i] is System.Windows.Controls.Button btn)
                {
                    bool isHighlighted = cg.HighlightIndex == i;
                    btn.Background = new SolidColorBrush(isHighlighted
                        ? System.Windows.Media.Color.FromRgb(62, 191, 191)
                        : System.Windows.Media.Color.FromRgb(12, 16, 22));
                    btn.Foreground = new SolidColorBrush(isHighlighted
                        ? System.Windows.Media.Color.FromRgb(0, 0, 0)
                        : System.Windows.Media.Color.FromRgb(220, 240, 240));
                }
            }
        }
        return true;
    }

    #endregion

    #region BacklogView

    private static ScrollViewer CreateBacklogElement()
    {
        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            IsHitTestVisible = true,
            Focusable = true,
            Content = new StackPanel { Name = "BacklogStack" },
        };
    }

    private int _backlogLastCount = -1;

    private bool UpdateBacklogView(BacklogView bl, UIElement element)
    {
        if (!bl.IsOpen) { element.Visibility = Visibility.Collapsed; return true; }
        element.Visibility = Visibility.Visible;

        if (element is not ScrollViewer sv) return true;
        if (sv.Content is not StackPanel sp) return true;

        var w = _effectiveWidth > 10 ? _effectiveWidth : 1280;
        sv.Width = w * 0.85;
        sv.Height = 500;
        Canvas.SetLeft(sv, w * 0.075);
        Canvas.SetTop(sv, 50);
        Panel.SetZIndex(sv, 600);

        if (bl.Entries.Count != _backlogLastCount)
        {
            _backlogLastCount = bl.Entries.Count;
            sp.Children.Clear();
            foreach (var entry in bl.Entries)
            {
                var tb = new TextBlock
                {
                    Text = string.IsNullOrEmpty(entry.SpeakerName)
                        ? $"  {entry.Text}"
                        : $"[{entry.SpeakerName}] {entry.Text}",
                    FontSize = 16,
                    Foreground = string.IsNullOrEmpty(entry.SpeakerName)
                        ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88))
                        : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xE8, 0xE8)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 3),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = entry.VoicePath,
                };
                tb.MouseLeftButtonDown += (s, e) =>
                {
                    if (s is TextBlock tagTb && tagTb.Tag is string vp && !string.IsNullOrEmpty(vp))
                        AudioManager.PlayVoice(vp);
                };
                sp.Children.Add(tb);
            }
        }
        return true;
    }

    #endregion

    #region AdvanceIndicator

    private static TextBlock CreateAdvanceIndicatorElement()
    {
        return new TextBlock
        {
            Text = "▼",
            FontSize = 18,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0xBF, 0xBF)),
            HorizontalAlignment = HorizontalAlignment.Right,
            IsHitTestVisible = false,
        };
    }

    private int _advanceLastFrame = -1;

    private bool UpdateAdvanceIndicator(AdvanceIndicator ai, UIElement element)
    {
        if (!ai.Visible) { element.Visibility = Visibility.Collapsed; return true; }
        element.Visibility = Visibility.Visible;

        if (element is not TextBlock tb) return true;
        if (ai.CurrentFrame == _advanceLastFrame) return true;
        _advanceLastFrame = ai.CurrentFrame;

        var alpha = (byte)(128 + (ai.CurrentFrame % 6) * 21);
        tb.Foreground = new SolidColorBrush(
            System.Windows.Media.Color.FromArgb(alpha, 0x3E, 0xBF, 0xBF));

        var w = _effectiveWidth > 10 ? _effectiveWidth : 1280;
        var h = _effectiveHeight > 10 ? _effectiveHeight : 720;
        Canvas.SetLeft(tb, w - 70);
        Canvas.SetTop(tb, h - 50);
        Panel.SetZIndex(tb, 200);

        return true;
    }

    #endregion

    #region Slider

    private static Border CreateSliderElement()
    {
        var border = new Border
        {
            Width = 420, Height = 52,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, 0x3E, 0xBF, 0xBF)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x8A, 0x8A)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 3, 0, 3),
        };

        var grid = new Grid { Margin = new Thickness(12, 4, 12, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

        var label = new TextBlock
        {
            FontSize = 15,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0xBF, 0xBF)),
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var slider = new System.Windows.Controls.Slider
        {
            Minimum = 0, Maximum = 1,
            SmallChange = 0.05, LargeChange = 0.1,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x30)),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0xBF, 0xBF)),
        };
        Grid.SetColumn(slider, 1);
        grid.Children.Add(slider);

        var valueText = new TextBlock
        {
            FontSize = 14,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(valueText, 2);
        grid.Children.Add(valueText);

        border.Child = grid;

        border.Tag = new SliderRefs(label, slider, valueText);

        return border;
    }

    private sealed record SliderRefs(
        System.Windows.Controls.TextBlock Label,
        System.Windows.Controls.Slider Slider,
        System.Windows.Controls.TextBlock ValueText);

    private bool UpdateSlider(Slider sl, UIElement element)
    {
        if (element is not Border border || border.Tag is not SliderRefs refs) return true;

        refs.Label.Text = sl.Label;
        refs.Slider.Minimum = sl.MinValue;
        refs.Slider.Maximum = sl.MaxValue;
        refs.ValueText.Text = sl.ValueText;

        if (Math.Abs(refs.Slider.Value - sl.Value) > 0.001f)
        {
            refs.Slider.Value = sl.Value;
        }

        if (refs.Slider.Tag is not "hooked")
        {
            refs.Slider.Tag = "hooked";
            refs.Slider.ValueChanged += (_, _) =>
            {
                sl.Value = (float)refs.Slider.Value;
            };
        }

        return true;
    }

    private static bool TextArraysEqual(string[]? a, string[]? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private bool UpdateTextRenderer(TextRenderer tr, UIElement element)
    {
        if (element is System.Windows.Controls.TextBlock tb)
        {
            tb.Text = tr.Content;
            tb.FontSize = tr.FontSize;
            tb.Foreground = new SolidColorBrush(ColorConversion.ToWpf(tr.TextColor));
            Panel.SetZIndex(tb, tr.GameObject?.Transform.SortingOrder ?? 200);
            var w = _effectiveWidth > 0 ? _effectiveWidth : 1280;
            tb.Width = w * 0.8;
            Canvas.SetLeft(tb, w * 0.1);
            Canvas.SetTop(tb, tr.GameObject?.Transform.WorldY * (_effectiveHeight > 0 ? _effectiveHeight : 720) ?? 0);
        }
        return true;
    }

    #endregion

    #region Utilities

    private CanvasScaler? FindCanvasScaler()
    {
        if (_scene == null) return null;
        foreach (var go in _scene.RootObjects)
        {
            if (go.IsDestroyed || !go.ActiveSelf) continue;
            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler != null) return scaler;
        }
        return null;
    }

    private static BitmapImage? LoadCachedBitmap(Sprite sprite)
    {
        // Synchronous decode: a Sprite with embedded ImageData (e.g. save thumbnails) decodes inline, no background thread.
        if (sprite.ImageData is { Length: > 0 })
        {
            var key = sprite.Path;
            var bitmap = DecodeBitmap(sprite.ImageData);
            if (bitmap != null)
                return bitmap;
            return null;
        }

        var result = ResourceManager.GetBitmap(sprite.Path);
        if (result != null) return result;

        _ = ResourceManager.GetBitmapAsync(sprite.Path);
        return null;
    }

    internal static void WarmupBitmap(Sprite sprite)
    {
        ResourceManager.WarmupSync(sprite.Path);
    }

    internal static void WarmupBitmapSync(string logicalPath)
    {
        ResourceManager.WarmupSync(logicalPath);
    }

    internal static async Task WarmupBitmapsAsync(IEnumerable<string> paths, IProgress<int>? progress = null)
    {
        await ResourceManager.PreloadAllAsync(progress);
    }

    #endregion

    #region LineRenderer

    private static System.Windows.Shapes.Shape CreateLineRendererElement()
    {
        return new System.Windows.Shapes.Polyline
        {
            Stroke = Brushes.White,
            StrokeThickness = 2,
            IsHitTestVisible = false,
        };
    }

    private bool UpdateLineRenderer(LineRenderer lr, UIElement element)
    {
        var transform = lr.GameObject?.Transform;
        var needsShape = lr.Loop ? typeof(System.Windows.Shapes.Polygon) : typeof(System.Windows.Shapes.Polyline);
        if (element.GetType() != needsShape)
        {
            _canvas.Children.Remove(element);
            _visualMap.Remove(lr);
            _knownComponents.Remove(lr);
            _lastVersion.Remove(lr);

            var newShape = lr.Loop
                ? (System.Windows.Shapes.Shape)new System.Windows.Shapes.Polygon
                    { Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false }
                : new System.Windows.Shapes.Polyline
                    { Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false };

            _visualMap[lr] = newShape;
            _canvas.Children.Add(newShape);
            _knownComponents.Add(lr);
            lr.MarkDirty();
            return UpdateLineRenderer(lr, newShape);
        }

        var wpfColor = ColorConversion.ToWpf(lr.Color);
        var points = new PointCollection();
        var canvasW = _effectiveWidth > 0 ? _effectiveWidth : 1280;
        var canvasH = _effectiveHeight > 0 ? _effectiveHeight : 720;

        foreach (var pos in lr.Positions)
        {
            points.Add(new Point(pos.X * canvasW, pos.Y * canvasH));
        }

        if (element is System.Windows.Shapes.Polyline pl)
        {
            pl.Points = points;
            pl.Stroke = new SolidColorBrush(wpfColor);
            pl.StrokeThickness = lr.Width;
            pl.Opacity = transform?.Opacity ?? 1f;
        }
        else if (element is System.Windows.Shapes.Polygon pg)
        {
            pg.Points = points;
            pg.Stroke = new SolidColorBrush(wpfColor);
            pg.StrokeThickness = lr.Width;
            pg.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
                (byte)(lr.Color.A * 0.3f), lr.Color.R, lr.Color.G, lr.Color.B));
            pg.Opacity = transform?.Opacity ?? 1f;
        }

        return true;
    }

    #endregion

    #region Hint Overlay (tooltip text beside cursor)

    private void UpdateHintTooltip()
    {
        string? hint = null;
        if (_hoveredGo != null && !_hoveredGo.IsDestroyed)
        {
            foreach (var comp in _hoveredGo.Components)
            {
                var t = comp.HintText;
                if (!string.IsNullOrEmpty(t)) { hint = t; break; }
            }
        }

        if (string.IsNullOrEmpty(hint))
        {
            if (_hintPopup != null)
                _hintPopup.Visibility = Visibility.Collapsed;
            _lastHintText = null;
            return;
        }

        if (_hintPopup == null)
        {
            _hintText = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0xBF, 0xBF)),
                Padding = new Thickness(10, 6, 10, 6),
            };
            _hintPopup = new Border
            {
                Child = _hintText,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xE0, 0x0A, 0x0A, 0x0A)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x8A, 0x8A)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                IsHitTestVisible = false,
            };
            _canvas.Children.Add(_hintPopup);
            Panel.SetZIndex(_hintPopup, 10000);
        }

        if (hint != _lastHintText)
        {
            _lastHintText = hint;
            _hintText!.Text = hint;
        }

        _hintPopup.Visibility = Visibility.Visible;
        Canvas.SetLeft(_hintPopup, _lastMousePos.X + 16);
        Canvas.SetTop(_hintPopup, _lastMousePos.Y + 16);
    }

    private static BitmapImage? DecodeBitmap(byte[] data)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new System.IO.MemoryStream(data);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            Logger.Warning("Render", $"Bitmap decode failed ({data.Length} bytes): {ex.Message}");
            return null;
        }
    }
    #endregion
}

internal static class ColorConversion
{
    public static System.Windows.Media.Color ToWpf(Drawing.Color c)
    {
        return System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
    }
}
