using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NanoUint.Diagnostics;

namespace NanoUint.Rendering;

/// <summary>WPF 渲染器。将 Scene 中的 Component 映射到 Canvas 上的 UIElement。</summary>
internal sealed class WpfRenderer
{
    private readonly Canvas _canvas;
    private Scene? _scene;
    private readonly Dictionary<Component, UIElement> _visualMap = new();
    private readonly Dictionary<Component, int> _lastVersion = new();
    private readonly HashSet<Component> _knownComponents = new();
    private double _lastCanvasWidth;
    private double _lastCanvasHeight;
    private float _lastScaleFactor = 1f;

    // Canvas Scaler support
    private CanvasScaler? _canvasScaler;
    private double _effectiveWidth;
    private double _effectiveHeight;

    // 鼠标交互追踪（Unity EventSystem 风格接口调度）
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

    // Hint 提示浮层
    private Border? _hintPopup;
    private TextBlock? _hintText;
    private string? _lastHintText;

    /// <summary>当前鼠标在 Canvas 中的位置（由 WpfEngineHost 更新）。</summary>
    internal static Point LastMousePosition
    {
        get => _lastMousePos;
        set => _lastMousePos = value;
    }

    // 图片统一通过 ResourceManager.GetBitmap() 获取。

    public WpfRenderer(Canvas canvas)
    {
        _canvas = canvas;
    }

    public void SetActiveScene(Scene scene)
    {
        _scene = scene;
        _canvas.Children.Clear();
        _visualMap.Clear();
        _lastVersion.Clear();
        _knownComponents.Clear();
    }

    /// <summary>增量更新：只同步 RenderVersion 发生变化的组件。</summary>
    public void UpdateDirtyComponents()
    {
        if (_scene == null) return;

        // 检测 Canvas 尺寸变化（分辨率切换 / 窗口缩放）→ 强制所有组件重新布局
        var currentW = _canvas.ActualWidth;
        var currentH = _canvas.ActualHeight;
        bool sizeChanged = Math.Abs(currentW - _lastCanvasWidth) > 0.5 || Math.Abs(currentH - _lastCanvasHeight) > 0.5;

        // Canvas Scaler: 查找并更新缩放
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
                _lastVersion.Clear(); // 强制重新布局
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
                _lastVersion.Clear(); // 强制所有组件下一帧重新定位（已在 scaler 分支中处理）
        }

        // 收集当前场景中所有需要渲染的组件
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

        // 移除已销毁/不可见的组件
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

        // 新增或更新组件
        foreach (var comp in currentComponents)
        {
            if (!_knownComponents.Contains(comp))
            {
                // 新组件：创建 UIElement
                var visual = CreateVisual(comp);
                if (visual != null)
                {
                    _visualMap[comp] = visual;
                    _canvas.Children.Add(visual);
                    _knownComponents.Add(comp);
                    if (UpdateVisual(comp, visual)) // 首次更新：全部成功才记 version
                        _lastVersion[comp] = comp.RenderVersion;
                }
            }
            else if (_visualMap.TryGetValue(comp, out var existing))
            {
                // 已有组件：仅在 version 变化时更新
                var lastVer = _lastVersion.GetValueOrDefault(comp, -1);
                if (comp.RenderVersion != lastVer)
                {
                    if (UpdateVisual(comp, existing))
                        _lastVersion[comp] = comp.RenderVersion;
                    // 若 UpdateVisual 返回 false（bitmap 后台加载中），不更新 version → 下帧重试
                }
            }
        }

        // 更新 Hint 浮层
        UpdateHintTooltip();
    }

    private UIElement? CreateVisual(Component comp)
    {
        return comp switch
        {
            FlashOverlay => CreateOverlayElement(),
            SpriteRenderer => CreateImageElement(),
            BackgroundRenderer => CreateBackgroundElement(),
            DialogueBox => CreateDialogueBoxElement(),
            ChoiceGroup => CreateChoiceGroupElement(),
            TextRenderer => CreateTextElement(),
            BacklogView => CreateBacklogElement(),
            AdvanceIndicator => CreateAdvanceIndicatorElement(),
            PhoneScreen => CreatePhoneElement(),
            Slider => CreateSliderElement(),
            LineRenderer => CreateLineRendererElement(),
            _ => null
        };
    }

    private bool UpdateVisual(Component comp, UIElement element)
    {
        var transform = comp.GameObject?.Transform;
        if (transform == null) return true;

        // 1. ZIndex — no size dependency
        Canvas.SetZIndex(element, transform.SortingOrder);

        // 2. Per-type update FIRST — sets Width/Height so positioning below reads correct values.
        bool typeUpdateOk = comp switch
        {
            FlashOverlay fo => UpdateFlashOverlay(fo, element),
            SpriteRenderer sr => UpdateSpriteRenderer(sr, element),
            BackgroundRenderer bg => UpdateBackgroundRenderer(bg, element),
            DialogueBox db => UpdateDialogueBox(db, element),
            ChoiceGroup cg => UpdateChoiceGroup(cg, element),
            TextRenderer tr => UpdateTextRenderer(tr, element),
            BacklogView bl => UpdateBacklogView(bl, element),
            AdvanceIndicator ai => UpdateAdvanceIndicator(ai, element),
            PhoneScreen ps => UpdatePhoneScreen(ps, element),
            Slider sl => UpdateSlider(sl, element),
            LineRenderer lr => UpdateLineRenderer(lr, element),
            _ => true
        };

        // 3. Opacity, FlipX, positioning.
        //    Self-positioning components (DialogueBox, FlashOverlay, AdvanceIndicator)
        //    set their own Canvas.Left/Top in step 2 — only Opacity is applied here.
        if (element is FrameworkElement fe)
        {
            fe.Opacity = transform.Opacity;

            if (transform.FlipX)
                fe.RenderTransform = new ScaleTransform(-1, 1, fe.ActualWidth > 0 ? fe.ActualWidth / 2 : 100, 0);
            else
                fe.RenderTransform = System.Windows.Media.Transform.Identity;

            // Skip positioning for components that handle it in step 2
            if (comp is DialogueBox or FlashOverlay or AdvanceIndicator or BackgroundRenderer or PhoneScreen)
            {
                return typeUpdateOk;
            }

            var canvasW = _effectiveWidth > 0 ? _effectiveWidth : 1280;
            var canvasH = _effectiveHeight > 0 ? _effectiveHeight : 720;
            var elemW = fe.ActualWidth > 0 ? fe.ActualWidth : (fe.Width > 0 ? fe.Width : 280);
            var elemH = fe.ActualHeight > 0 ? fe.ActualHeight : (fe.Height > 0 ? fe.Height : 46);

            // RectTransform: 锚点定位（替代基础 Transform 的归一化 X/Y 定位）
            if (comp.GameObject?.GetComponent<RectTransform>() is { } rt)
            {
                var (left, top) = rt.ComputeCanvasPosition(canvasW, canvasH, elemW, elemH);
                Canvas.SetLeft(fe, left);
                Canvas.SetTop(fe, top);

                // 拉伸模式：设置元素宽高
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
    }

    #region 组件 → WPF 控件创建

    private static System.Windows.Shapes.Rectangle CreateOverlayElement()
    {
        return new System.Windows.Shapes.Rectangle
        {
            Width = double.NaN, Height = double.NaN, // 拉伸填充
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
            IsHitTestVisible = true, // 允许 Sprite 接收鼠标事件
        };
    }

    private static Grid CreateBackgroundElement()
    {
        return new Grid
        {
            Width = 1280, Height = 720,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
    }

    /// <summary>系统 UI 图片统一通过 ResourceManager.GetBitmap() 加载。</summary>

    private static Border CreateDialogueBoxElement()
    {
        var boxBg = ResourceManager.GetBitmap("dialogue/box00.png");
        var nameTagBg = ResourceManager.GetBitmap("dialogue/name01.png");

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Accent bar
        var accentBar = new System.Windows.Shapes.Rectangle
        {
            Height = 4, Width = 120, RadiusX = 2, RadiusY = 2,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0xBF, 0xBF)),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Grid.SetRow(accentBar, 0);
        grid.Children.Add(accentBar);

        // Speaker name
        var speakerLabel = new TextBlock
        {
            Name = "SpeakerLabel",
            FontWeight = FontWeights.Bold,
            FontSize = 17,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0xBF, 0xBF)),
        };
        Grid.SetRow(speakerLabel, 1);
        grid.Children.Add(speakerLabel);

        // Dialogue text
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

    #region 组件属性 → WPF 控件更新

    private bool UpdateSpriteRenderer(SpriteRenderer sr, UIElement element)
    {
        // 口型动画：使用当前口型帧 Sprite（有 fallback 到主 Sprite）
        var activeSprite = sr.GetActiveMouthSprite();
        if (activeSprite == null) return true;

        if (element is Image img)
        {
            var bitmap = LoadCachedBitmap(activeSprite);
            if (bitmap != null)
            {
                img.Source = bitmap;
                var nativeW = activeSprite.Width > 0 ? activeSprite.Width : (bitmap.Width > 0 ? bitmap.Width : double.NaN);
                var nativeH = activeSprite.Height > 0 ? activeSprite.Height : (bitmap.Height > 0 ? bitmap.Height : double.NaN);
                var ppu = activeSprite.PixelsPerUnit > 0 ? activeSprite.PixelsPerUnit : 100f; // 默认 100，像素 1:1 显示
                img.Width = nativeW * 100.0 / ppu;
                img.Height = nativeH * 100.0 / ppu;

                // 限制角色立绘最大高度为画面 78%，为对话筐留空间
                var maxH = (_effectiveHeight > 0 ? _effectiveHeight : 1080) * 0.78;
                img.MaxHeight = maxH;
            }
            else { sr.MarkDirty(); return false; } // 后台解码中，下帧重试
            img.Opacity = sr.Tint.A / 255f * (sr.GameObject?.Transform.Opacity ?? 1f);

            // 首次挂接鼠标事件
            if (img.Tag is not "mouse_hooked")
            {
                HookSpriteMouseEvents(img, sr);
            }
        }
        return true;
    }

    /// <summary>为 SpriteRenderer 挂接 WPF 鼠标事件 → IPointer*Handler 接口调度。</summary>
    private void HookSpriteMouseEvents(Image img, SpriteRenderer sr)
    {
        // 检查此 GameObject 是否有任何组件实现了指针接口
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

    /// <summary>检查 GameObject 上是否有任何组件实现了指针事件接口。</summary>
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

    /// <summary>向 GameObject 上所有实现了 T 接口的组件派发事件。</summary>
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

            // 只用 Color 自身的 alpha，Transform.Opacity 由 UpdateVisual 统一通过 fe.Opacity 处理
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

        // 背景填满整个画布（覆盖 Canvas Scaler 的参考分辨率 1280×720 限制）
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
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });

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
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            if (grid.Children[0] is Image img)
            {
                var bmp = LoadCachedBitmap(bg.Sprite);
                if (bmp != null) { img.Source = bmp; img.Opacity = 1f; }
                else { bg.MarkDirty(); return false; }
            }
        }

        // Tint 叠加层
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
            // 移除 tint 层
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

        // 全宽、底部对齐：先测量内容高度再贴底
        border.Width = effectiveW;
        Canvas.SetLeft(border, 0);
        border.Measure(new System.Windows.Size(effectiveW, double.PositiveInfinity));
        var h = border.DesiredSize.Height > 10 ? border.DesiredSize.Height : border.MinHeight;
        Canvas.SetTop(border, Math.Max(0, effectiveH - h));

        // Accent bar: only visible when speaker present
        if (grid.Children.Count > 0 && grid.Children[0] is System.Windows.Shapes.Rectangle accentBar)
            accentBar.Visibility = string.IsNullOrEmpty(db.SpeakerName)
                ? Visibility.Collapsed : Visibility.Visible;

        // Speaker name
        if (grid.Children.Count > 1 && grid.Children[1] is TextBlock speakerLabel)
        {
            speakerLabel.Text = db.SpeakerName;
            speakerLabel.Visibility = string.IsNullOrEmpty(db.SpeakerName)
                ? Visibility.Collapsed : Visibility.Visible;
        }

        // Dialogue text
        if (grid.Children.Count > 2 && grid.Children[2] is TextBlock textContent)
        {
            textContent.Text = db.Text;

            // 渐进式渐显 OpacityMask（0→1 从左到右展开）
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
            // 内联模式：不覆盖位置，由 UpdateVisual 的 Transform 来控制
            sp.Orientation = Orientation.Horizontal;
        }
        else
        {
            // 默认模式：全屏居中菜单
            sp.Orientation = Orientation.Vertical;
            sp.Width = 320;
            Canvas.SetLeft(sp, (w - 320) / 2);
            Canvas.SetTop(sp, Math.Max(80, (h - cg.ChoiceCount * 56) / 2));
        }

        // 仅在选项内容变化时重建按钮
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

                // 高亮当前选中项
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
            // 无重建但需刷新高亮（按钮已存在）
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

        // 只在条目数变化时重建
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

    #region PhoneScreen
    // Canvas 子元素索引 (CreatePhoneElement 创建顺序)
    private const int CI_FRAME = 0, CI_SCREEN = 1, CI_BADGE = 2, CI_GLASS = 3, CI_ANIM = 4;
    // screenGrid 子元素索引
    private const int SI_WP = 0, SI_CONTENT = 1, SI_BLACK = 2;

    private static Canvas CreatePhoneElement()
    {
        var frameImg = new Image { Stretch = Stretch.Uniform };
        var screenGrid = new Grid { ClipToBounds = true };

        var wpImg = new Image { Stretch = Stretch.UniformToFill };
        var contentPanel = new StackPanel();
        var blackFill = new System.Windows.Shapes.Rectangle
        {
            Fill = Brushes.Black,
            Visibility = Visibility.Collapsed,
        };

        var badgeImg = new Image { Stretch = Stretch.None, Visibility = Visibility.Collapsed };
        var glassImg = new Image { Stretch = Stretch.Uniform, Opacity = 0.25, IsHitTestVisible = false };
        var animImg = new Image { Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed };

        screenGrid.Children.Add(wpImg);       // SI_WP
        screenGrid.Children.Add(contentPanel); // SI_CONTENT
        screenGrid.Children.Add(blackFill);    // SI_BLACK

        var canvas = new Canvas { Visibility = Visibility.Collapsed };
        canvas.Children.Add(frameImg);   // CI_FRAME
        canvas.Children.Add(screenGrid); // CI_SCREEN
        canvas.Children.Add(badgeImg);   // CI_BADGE
        canvas.Children.Add(glassImg);   // CI_GLASS
        canvas.Children.Add(animImg);    // CI_ANIM

        return canvas;
    }

    private PhoneScreen.State _phoneLastState = PhoneScreen.State.Closed;
    private string _phoneLastContact = "";
    private int _phoneLastWallpaper = -1;

    private bool UpdatePhoneScreen(PhoneScreen ps, UIElement element)
    {
        if (element is not Canvas canvas) return true;
        var cfg = ps.Config;
        if (cfg == null) { element.Visibility = Visibility.Collapsed; return true; }

        if (ps.CurrentState == PhoneScreen.State.Closed)
        {
            element.Visibility = Visibility.Collapsed;
            return true;
        }

        var canvasW = _effectiveWidth > 10 ? _effectiveWidth : 1280;
        var canvasH = _effectiveHeight > 10 ? _effectiveHeight : 720;
        double s = cfg.Scale;
        double fw = cfg.FrameWidth * s;
        double fh = cfg.FrameHeight * s;
        double sx = cfg.ScreenX * s;
        double sy = cfg.ScreenY * s;
        double sw = cfg.ScreenW * s;
        double sh = cfg.ScreenH * s;

        double phoneX = canvasW - fw - 20;
        double phoneY = canvasH - fh - 10;

        element.Visibility = Visibility.Visible;
        canvas.Width = fw;
        canvas.Height = fh;
        Panel.SetZIndex(canvas, cfg.ZOrder > 0 ? cfg.ZOrder : 800);
        Canvas.SetLeft(canvas, phoneX);
        Canvas.SetTop(canvas, phoneY);

        var frameImg = (Image)canvas.Children[CI_FRAME];
        var screenGrid = (Grid)canvas.Children[CI_SCREEN];
        var badgeImg = (Image)canvas.Children[CI_BADGE];
        var glassImg = (Image)canvas.Children[CI_GLASS];
        var wpImg = (Image)screenGrid.Children[SI_WP];
        var contentPanel = (StackPanel)screenGrid.Children[SI_CONTENT];

        frameImg.Width = fw;
        frameImg.Height = fh;
        if (frameImg.Source == null)
            frameImg.Source = ResourceManager.GetBitmap(cfg.FramePath);

        Canvas.SetLeft(screenGrid, sx);
        Canvas.SetTop(screenGrid, sy);
        screenGrid.Width = sw;
        screenGrid.Height = sh;
        screenGrid.Clip = new RectangleGeometry(new Rect(0, 0, sw, sh));

        if (ps.WallpaperIndex != _phoneLastWallpaper && cfg.WallpaperPaths.Length > ps.WallpaperIndex)
        {
            _phoneLastWallpaper = ps.WallpaperIndex;
            var wpBmp = ResourceManager.GetBitmap(cfg.WallpaperPaths[ps.WallpaperIndex]);
            if (wpBmp != null) wpImg.Source = wpBmp;
        }
        wpImg.Width = sw;
        wpImg.Height = sh;

        Canvas.SetLeft(glassImg, sx);
        Canvas.SetTop(glassImg, sy);
        glassImg.Width = sw;
        glassImg.Height = sh;
        if (glassImg.Source == null)
            glassImg.Source = ResourceManager.GetBitmap(cfg.GlassPath);

        badgeImg.Visibility = Visibility.Collapsed;

        var animImg = (Image)canvas.Children[CI_ANIM];
        var blackFill = (System.Windows.Shapes.Rectangle)screenGrid.Children[SI_BLACK];

        #region Opening: ShowAnimation 帧动画
        // Canvas 扩展到全屏高度 + animImg 底部对齐 → 手机从屏幕底部逐帧伸出
        // PhoneScreen.Update() 每帧递增 SlideTimer → MarkDirty() 驱动此段逐帧刷新
        // 动画结束自动切 BlackScreen（在 PhoneScreen.Update 中检测 SlideTimer >= duration）
        if (ps.CurrentState == PhoneScreen.State.Opening)
        {
            // Canvas 扩展到全屏高度，让手机从底部向上伸出
            canvas.Height = canvasH;
            Canvas.SetTop(canvas, 0);

            frameImg.Visibility = Visibility.Collapsed;
            screenGrid.Visibility = Visibility.Collapsed;
            glassImg.Visibility = Visibility.Collapsed;
            animImg.Visibility = Visibility.Visible;

            int frameIdx = Math.Min((int)(ps.SlideTimer / cfg.SlideDurationSec * cfg.SlideFrameCount),
                cfg.SlideFrameCount - 1);
            if (frameIdx < cfg.SlideFramePaths.Length)
                animImg.Source = ResourceManager.GetBitmap(cfg.SlideFramePaths[frameIdx]);

            animImg.Width = fw;
            animImg.Height = fh;
            Canvas.SetRight(animImg, 0);
            Canvas.SetBottom(animImg, 0);
            return true;
        }

        #endregion

        #region 非 Opening: 常规位置与尺寸
        canvas.Height = fh;
        Canvas.SetTop(canvas, phoneY);

        frameImg.Visibility = Visibility.Visible;
        screenGrid.Visibility = Visibility.Visible;
        glassImg.Visibility = Visibility.Visible;
        animImg.Visibility = Visibility.Collapsed;

        #endregion

        #region BlackScreen: 锁屏黑屏
        // 隐藏壁纸 + 显示黑色矩形覆盖屏幕区 + 清空内容
        if (ps.CurrentState == PhoneScreen.State.BlackScreen)
        {
            // 壁纸设为首帧（纯黑），或隐藏壁纸 + 显示黑矩形
            wpImg.Source = null; // 无壁纸
            blackFill.Width = sw;
            blackFill.Height = sh;
            blackFill.Visibility = Visibility.Visible;
            contentPanel.Children.Clear();
            _phoneLastState = ps.CurrentState;
            return true;
        }
        blackFill.Visibility = Visibility.Collapsed;

        #endregion

        #region 内容重建（仅在状态/联系人变化时）
        if (ps.CurrentState == _phoneLastState &&
            ps.ActiveRineContact == _phoneLastContact)
            return true;

        _phoneLastState = ps.CurrentState;
        _phoneLastContact = ps.ActiveRineContact;
        contentPanel.Children.Clear();

        switch (ps.CurrentState)
        {
            case PhoneScreen.State.Home:
                BuildHomeScreen(contentPanel, cfg, ps, sw, sh);
                break;
            case PhoneScreen.State.DenhaIncoming:
                BuildDenhaIncoming(contentPanel, cfg, ps,
                    UintToColor(cfg.TextPrimary), UintToColor(cfg.TextSecondary));
                break;
            case PhoneScreen.State.DenhaInCall:
                BuildDenhaInCall(contentPanel, cfg, ps,
                    UintToColor(cfg.TextPrimary), UintToColor(cfg.TextSecondary));
                break;
            case PhoneScreen.State.RineList:
                BuildRineList(contentPanel, cfg, ps,
                    UintToColor(cfg.TextPrimary), UintToColor(cfg.TextSecondary), sw);
                break;
            case PhoneScreen.State.RineChat:
                BuildRineChat(contentPanel, cfg, ps,
                    UintToColor(cfg.TextPrimary), UintToColor(cfg.TextSecondary), sw);
                break;
            case PhoneScreen.State.RineStamp:
                BuildRineStampPicker(contentPanel, cfg, ps, sw);
                break;
            case PhoneScreen.State.Settings:
                BuildSettings(contentPanel, cfg, ps, UintToColor(cfg.TextPrimary), sw);
                break;
            case PhoneScreen.State.SettingsWallpaper:
                BuildSettingsWallpaper(contentPanel, cfg, ps, sw, sh);
                break;
        }

        return true;
        #endregion
    }

    #endregion

    #region 各状态的 UI 构建

    private static void BuildHomeScreen(StackPanel panel, PhoneRenderConfig cfg, PhoneScreen ps,
        double screenW, double screenH)
    {
        // 图标栏在底部
        var iconRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, screenH - 100, 0, 12),
        };

        iconRow.Children.Add(CreateAppIcon(cfg.DenhaIconPath, cfg.DenhaHighlightPath,
            () => ps.OpenDenha()));
        iconRow.Children.Add(CreateAppIcon(cfg.RineIconPath, cfg.RineHighlightPath,
            () => ps.OpenRine()));
        iconRow.Children.Add(CreateAppIcon(cfg.SettingsIconPath, cfg.SettingsHighlightPath,
            () => ps.OpenSettings()));

        panel.Children.Add(iconRow);
    }

    private static void BuildDenhaIncoming(StackPanel panel, PhoneRenderConfig cfg, PhoneScreen ps,
        System.Windows.Media.Color textPri, System.Windows.Media.Color textSec)
    {
        panel.Margin = new Thickness(16, 40, 16, 16);
        panel.Children.Add(CreatePhoneText(ps.CallerName, 22, true, textPri));
        panel.Children.Add(CreatePhoneSpacer(8));
        panel.Children.Add(CreatePhoneText("is calling...", 14, false, textSec));
        panel.Children.Add(CreatePhoneSpacer(16));
        panel.Children.Add(CreatePhoneText($"{ps.CallTimer:F1}s", 12, false, textSec));

        panel.Children.Add(CreatePhoneSpacer(24));
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        btnRow.Children.Add(CreateImageButton(cfg.DenhaAcceptPath, 80, 80, () => ps.AnswerCall()));
        btnRow.Children.Add(CreateImageButton(cfg.DenhaRefusePath, 80, 80, () => ps.DeclineCall()));
        panel.Children.Add(btnRow);
    }

    private static void BuildDenhaInCall(StackPanel panel, PhoneRenderConfig cfg, PhoneScreen ps,
        System.Windows.Media.Color textPri, System.Windows.Media.Color textSec)
    {
        panel.Margin = new Thickness(16, 40, 16, 16);
        panel.Children.Add(CreatePhoneText(ps.CallerName, 20, true, textPri));
        panel.Children.Add(CreatePhoneSpacer(4));
        panel.Children.Add(CreatePhoneText("通话中...", 14, false, textSec));
        panel.Children.Add(CreatePhoneSpacer(20));

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        btnRow.Children.Add(CreateImageButton(cfg.DenhaSpeakerPath, 64, 64, () => { }));
        btnRow.Children.Add(CreateImageButton(cfg.DenhaRefusePath, 64, 64, () => ps.EndCall()));
        panel.Children.Add(btnRow);
    }

    private void BuildRineList(StackPanel panel, PhoneRenderConfig cfg, PhoneScreen ps,
        System.Windows.Media.Color textPri, System.Windows.Media.Color textSec, double screenW)
    {
        panel.Margin = new Thickness(8, 8, 8, 8);

        // 标题
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(CreatePhoneButtonSmall("←", () => ps.BackToHome()));
        header.Children.Add(CreatePhoneText("RINE", 16, true, textPri));
        panel.Children.Add(header);
        panel.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Height = 1, Margin = new Thickness(0, 4, 0, 8),
            Fill = new SolidColorBrush(UintToColor(cfg.Divider)),
        });

        var conversations = PhoneService.GetConversations();
        if (conversations.Count == 0)
        {
            panel.Children.Add(CreatePhoneText("No messages.", 13, false, textSec));
        }
        else
        {
            foreach (var conv in conversations.Take(20))
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                // 头像
                bool hasAvatar = cfg.ContactAvatars.TryGetValue(conv.ContactName, out var avatarPath);
                var avatarBmp = hasAvatar ? ResourceManager.GetBitmap(avatarPath) : ResourceManager.GetBitmap(cfg.RineDefaultAvatarPath);
                if (avatarBmp != null)
                {
                    var avatar = new Image { Source = avatarBmp, Width = 36, Height = 36, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 8, 0) };
                    row.Children.Add(avatar);
                }

                var previewText = $"{conv.ContactName}: {conv.LastMessagePreview}";
                if (previewText.Length > 28) previewText = previewText[..25] + "...";
                var unreadMark = conv.HasUnread ? "● " : "";
                var txt = CreatePhoneText($"{unreadMark}{previewText}", 12, conv.HasUnread,
                    conv.HasUnread ? UintToColor(cfg.Accent) : textSec);

                var contact = conv.ContactName; // capture
                row.MouseLeftButtonDown += (_, _) => ps.OpenRineChat(contact);
                row.Children.Add(txt);
                panel.Children.Add(row);
            }
        }
    }

    private void BuildRineChat(StackPanel panel, PhoneRenderConfig cfg, PhoneScreen ps,
        System.Windows.Media.Color textPri, System.Windows.Media.Color textSec, double screenW)
    {
        panel.Margin = new Thickness(8, 8, 8, 8);

        // 标题栏
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(CreatePhoneButtonSmall("←", () => ps.OpenRine()));
        bool hasAvatar = cfg.ContactAvatars.TryGetValue(ps.ActiveRineContact, out var avatarPath);
        var avatarBmp = hasAvatar ? ResourceManager.GetBitmap(avatarPath) : ResourceManager.GetBitmap(cfg.RineDefaultAvatarPath);
        if (avatarBmp != null)
            header.Children.Add(new Image { Source = avatarBmp, Width = 24, Height = 24, Stretch = Stretch.Uniform, Margin = new Thickness(4, 0, 4, 0) });
        header.Children.Add(CreatePhoneText(ps.ActiveRineContact, 14, true, textPri));
        panel.Children.Add(header);
        panel.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Height = 1, Margin = new Thickness(0, 4, 0, 6),
            Fill = new SolidColorBrush(UintToColor(cfg.Divider)),
        });

        // 消息列表（scrollable）
        var msgScroll = new ScrollViewer { MaxHeight = screenW > 0 ? screenW * 0.9 : 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var msgPanel = new StackPanel();
        var messages = PhoneService.GetMessages(ps.ActiveRineContact);
        var bubbleOther = ResourceManager.GetBitmap(cfg.RineBubbleOtherPath);
        var bubbleSelf = ResourceManager.GetBitmap(cfg.RineBubbleSelfPath);

        foreach (var msg in messages)
        {
            bool isOut = msg.IsOutgoing;
            var bubbleSrc = isOut ? bubbleSelf : bubbleOther;

            var bubbleGrid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            if (bubbleSrc != null)
                bubbleGrid.Children.Add(new Image { Source = bubbleSrc, Stretch = Stretch.Fill });

            var bubbleText = new TextBlock
            {
                Text = msg.Text,
                FontSize = 12,
                Foreground = new SolidColorBrush(isOut ? Colors.White : UintToColor(cfg.TextPrimary)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12, 6, 12, 6),
                MaxWidth = screenW * 0.55,
            };
            bubbleGrid.Children.Add(bubbleText);

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            if (isOut)
                row.HorizontalAlignment = HorizontalAlignment.Right;
            row.Children.Add(bubbleGrid);
            msgPanel.Children.Add(row);
        }

        msgScroll.Content = msgPanel;
        panel.Children.Add(msgScroll);

        // 底部操作栏
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        footer.Children.Add(CreatePhoneButtonSmall("Stamp", () =>
        {
            var stampNames = cfg.RineStamps.Keys.Take(12).ToArray();
            ps.OpenRineStampPicker(stampNames, idx =>
            {
                if (idx >= 0 && idx < stampNames.Length)
                    PhoneService.SendMessage(ps.ActiveRineContact, $"[Sticker: {stampNames[idx]}]");
            });
        }));
        footer.Children.Add(CreatePhoneButtonSmall("Close", () => ps.Close()));
        panel.Children.Add(footer);
    }

    private void BuildRineStampPicker(StackPanel panel, PhoneRenderConfig cfg, PhoneScreen ps, double screenW)
    {
        panel.Margin = new Thickness(8, 8, 8, 8);
        panel.Children.Add(CreatePhoneText("Choose Sticker:", 14, true, UintToColor(cfg.TextPrimary)));
        panel.Children.Add(CreatePhoneSpacer(8));

        var grid = new WrapPanel { MaxWidth = screenW - 16 };
        for (int i = 0; i < ps.AvailableStamps.Length; i++)
        {
            var stampName = ps.AvailableStamps[i];
            if (!cfg.RineStamps.TryGetValue(stampName, out var stampPath)) continue;
            var idx = i;
            var stampBmp = ResourceManager.GetBitmap(stampPath);
            if (stampBmp == null) continue;
            var img = new Image { Source = stampBmp, Width = 60, Height = 60, Stretch = Stretch.Uniform, Margin = new Thickness(4) };
            img.MouseLeftButtonDown += (_, _) => ps.SelectStamp(idx);
            grid.Children.Add(img);
        }
        panel.Children.Add(grid);
        panel.Children.Add(CreatePhoneSpacer(8));
        panel.Children.Add(CreatePhoneButtonSmall("Back", () => ps.OpenRineChat(ps.ActiveRineContact)));
    }

    private static void BuildSettings(StackPanel panel, PhoneRenderConfig cfg, PhoneScreen ps,
        System.Windows.Media.Color textPri, double screenW)
    {
        panel.Margin = new Thickness(12, 12, 12, 12);
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(CreatePhoneButtonSmall("←", () => ps.BackToHome()));
        header.Children.Add(CreatePhoneText("Settings", 16, true, textPri));
        panel.Children.Add(header);
        panel.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Height = 1, Margin = new Thickness(0, 6, 0, 10),
            Fill = new SolidColorBrush(UintToColor(cfg.Divider)),
        });

        panel.Children.Add(CreatePhoneButtonSmall("Wallpaper", () => ps.OpenWallpaperPicker()));
        panel.Children.Add(CreatePhoneSpacer(8));
        panel.Children.Add(CreatePhoneButtonSmall("Close Phone", () => ps.Close()));
    }

    private void BuildSettingsWallpaper(StackPanel panel, PhoneRenderConfig cfg, PhoneScreen ps,
        double screenW, double screenH)
    {
        panel.Margin = new Thickness(8, 8, 8, 8);
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(CreatePhoneButtonSmall("←", () => ps.OpenSettings()));
        header.Children.Add(CreatePhoneText("Wallpaper", 14, true, UintToColor(cfg.TextPrimary)));
        panel.Children.Add(header);
        panel.Children.Add(CreatePhoneSpacer(8));

        // 壁纸缩略图网格
        var grid = new WrapPanel { MaxWidth = screenW - 16 };
        for (int i = 0; i < cfg.WallpaperPaths.Length; i++)
        {
            var idx = i;
            var thumb = ResourceManager.GetBitmap(cfg.WallpaperPaths[i]);
            if (thumb == null) continue;

            var border = new Border
            {
                Width = 70, Height = 100, Margin = new Thickness(3),
                Background = new SolidColorBrush(UintToColor(cfg.BgSecondary)),
                BorderBrush = idx == ps.WallpaperIndex
                    ? new SolidColorBrush(UintToColor(cfg.Accent))
                    : new SolidColorBrush(UintToColor(cfg.Divider)),
                BorderThickness = idx == ps.WallpaperIndex ? new Thickness(2) : new Thickness(1),
            };
            var wpThumb = new Image { Source = thumb, Stretch = Stretch.UniformToFill };
            border.Child = wpThumb;
            border.MouseLeftButtonDown += (_, _) => ps.SelectWallpaper(idx);
            grid.Children.Add(border);
        }
        panel.Children.Add(grid);

        // 左右切换箭头
        var arrowRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        arrowRow.Children.Add(CreateImageButton(cfg.ArrowLeftPath, 32, 32, () => ps.CycleWallpaper(-1)));
        arrowRow.Children.Add(CreatePhoneText($" {ps.WallpaperIndex + 1}/{cfg.WallpaperPaths.Length} ", 12, false, UintToColor(cfg.TextSecondary)));
        arrowRow.Children.Add(CreateImageButton(cfg.ArrowRightPath, 32, 32, () => ps.CycleWallpaper(1)));
        panel.Children.Add(arrowRow);
    }

    #endregion

    #region Phone UI 帮助方法

    private static double EaseOutCubic(double t) => 1 - Math.Pow(1 - t, 3);

    private static System.Windows.Media.Color UintToColor(uint c) =>
        System.Windows.Media.Color.FromArgb(
            (byte)((c >> 24) & 0xFF),
            (byte)((c >> 16) & 0xFF),
            (byte)((c >> 8) & 0xFF),
            (byte)(c & 0xFF));

    /// <summary>创建 App 图标 Button。hover 高亮通过 WPF 本地事件直接换 Image.Source。</summary>
    private static Button CreateAppIcon(string normalPath, string highlightPath, Action onClick)
    {
        var bmpNormal = ResourceManager.GetBitmap(normalPath);
        var bmpHighlight = ResourceManager.GetBitmap(highlightPath);
        var img = new Image
        {
            Source = bmpNormal,
            Width = 56, Height = 72,
            Stretch = Stretch.Uniform,
        };
        var btn = new Button
        {
            Content = img,
            Width = 64, Height = 80,
            Margin = new Thickness(3, 0, 3, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        btn.MouseEnter += (_, _) => img.Source = bmpHighlight;
        btn.MouseLeave += (_, _) => img.Source = bmpNormal;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button CreateImageButton(string imagePath, double w, double h, Action onClick)
    {
        var bmp = ResourceManager.GetBitmap(imagePath);
        var img = new Image { Source = bmp, Width = w, Height = h, Stretch = Stretch.Uniform };
        var btn = new Button
        {
            Content = img,
            Width = w + 8, Height = h + 8,
            Margin = new Thickness(4),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static TextBlock CreatePhoneText(string text, double fontSize, bool bold,
        System.Windows.Media.Color? color = null)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            Foreground = new SolidColorBrush(color ?? Colors.White),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2),
        };
    }

    private static TextBlock CreatePhoneSpacer(int height)
    {
        return new TextBlock { Height = height };
    }

    private static Button CreatePhoneButtonSmall(string text, Action onClick)
    {
        var btn = new Button
        {
            Content = text,
            Height = 30,
            MinWidth = 50,
            Margin = new Thickness(3, 0, 3, 0),
            FontSize = 12,
            FontWeight = FontWeights.Normal,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x66, 0x66, 0x66)),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(8, 0, 8, 0),
        };
        btn.Click += (_, _) => onClick();
        return btn;
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

    /// <summary>Slider WPF 子元素引用（避开 FindName 问题）。</summary>
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

        // 只订阅一次（用 Tag 标记）
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

    #region 工具

    /// <summary>在场景中查找 CanvasScaler 组件。</summary>
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

    /// <summary>加载 BitmapImage，按 Sprite.Path 从 ResourceManager 缓存获取。</summary>
    private static BitmapImage? LoadCachedBitmap(Sprite sprite)
    {
        // 同步解码：Sprite 内嵌 ImageData（如存档缩略图）→ 直接同步解码，无需后台线程
        if (sprite.ImageData is { Length: > 0 })
        {
            var key = sprite.Path;
            var bitmap = DecodeBitmap(sprite.ImageData);
            if (bitmap != null)
                return bitmap;
            return null;
        }

        // 委托给 ResourceManager（统一内嵌资源缓存）
        var result = ResourceManager.GetBitmap(sprite.Path);
        if (result != null) return result;

        // 缓存未命中 — 启动后台加载，不阻塞 UI 线程
        _ = ResourceManager.GetBitmapAsync(sprite.Path);
        return null; // 下帧重试
    }



    /// <summary>预热 BitmapImage 缓存。委托给 ResourceManager。</summary>
    internal static void WarmupBitmap(Sprite sprite)
    {
        ResourceManager.WarmupSync(sprite.Path);
    }

    /// <summary>同步预热单个图片。委托给 ResourceManager。</summary>
    internal static void WarmupBitmapSync(string logicalPath)
    {
        ResourceManager.WarmupSync(logicalPath);
    }

    /// <summary>后台并行预加载所有图片。委托给 ResourceManager.PreloadAllAsync()。</summary>
    internal static async Task WarmupBitmapsAsync(IEnumerable<string> paths, IProgress<int>? progress = null)
    {
        await ResourceManager.PreloadAllAsync(progress);
    }

    #endregion

    #region LineRenderer

    private static System.Windows.Shapes.Shape CreateLineRendererElement()
    {
        // 默认创建 Polyline，Loop 时切换为 Polygon
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
        // 确保形状类型匹配（Loop 用 Polygon，非 Loop 用 Polyline）
        var needsShape = lr.Loop ? typeof(System.Windows.Shapes.Polygon) : typeof(System.Windows.Shapes.Polyline);
        if (element.GetType() != needsShape)
        {
            // 类型不匹配：移除旧元素，创建新元素，交换
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

    #region Hint 浮层（鼠标旁提示文本）

    /// <summary>更新鼠标旁的 Hint 提示浮层。每帧调用。</summary>
    private void UpdateHintTooltip()
    {
        // 获取当前 hover 组件的 HintText
        string? hint = null;
        if (_hoveredGo != null && !_hoveredGo.IsDestroyed)
        {
            foreach (var comp in _hoveredGo.Components)
            {
                var t = comp.HintText;
                if (!string.IsNullOrEmpty(t)) { hint = t; break; }
            }
        }

        // 无 hint → 隐藏浮层
        if (string.IsNullOrEmpty(hint))
        {
            if (_hintPopup != null)
                _hintPopup.Visibility = Visibility.Collapsed;
            _lastHintText = null;
            return;
        }

        // 首次创建浮层
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

        // 更新文本（只在变化时）
        if (hint != _lastHintText)
        {
            _lastHintText = hint;
            _hintText!.Text = hint;
        }

        // 定位到鼠标旁边（偏移 16px）
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
        catch
        {
            return null;
        }
    }
    #endregion
}

/// <summary>内部颜色转换工具。</summary>
internal static class ColorConversion
{
    public static System.Windows.Media.Color ToWpf(Drawing.Color c)
    {
        return System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
    }
}
