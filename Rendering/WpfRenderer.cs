using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NanoUint.Diagnostics;

namespace NanoUint.Rendering;

/// <summary>
/// WPF 渲染器。将 Scene 中的 Component 映射到 Canvas 上的 UIElement。
/// 增量同步：只更新 RenderVersion 发生变化的组件。
/// 全部 internal —— 游戏开发者不可见。
/// </summary>
internal sealed class WpfRenderer
{
    private readonly Canvas _canvas;
    private Scene? _scene;
    private readonly Dictionary<Component, UIElement> _visualMap = new();
    private readonly Dictionary<Component, int> _lastVersion = new();
    private readonly HashSet<Component> _knownComponents = new();

    // BitmapImage 缓存（静态，跨场景复用 + 预加载可预热）。ConcurrentDictionary 保证后台预加载线程安全。
    private static readonly ConcurrentDictionary<string, BitmapImage> _bitmapCache = new();
    private static readonly ConcurrentDictionary<string, byte> _pendingLoads = new(); // 正在后台加载的 key，防重复提交

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
        // 不清空 _bitmapCache —— 图片解码结果跨场景复用
    }

    /// <summary>
    /// 增量更新：只同步 RenderVersion 发生变化的组件。
    /// 由 DispatcherTimer 在 UI 线程调用（不在渲染阶段）。
    /// </summary>
    public void UpdateDirtyComponents()
    {
        if (_scene == null) return;

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
            _ => null
        };
    }

    private bool UpdateVisual(Component comp, UIElement element)
    {
        var transform = comp.GameObject?.Transform;
        if (transform == null) return true;

        // 通用 Transform → Canvas 定位
        Canvas.SetZIndex(element, transform.SortingOrder);

        if (element is FrameworkElement fe)
        {
            fe.Opacity = transform.Opacity;

            if (transform.FlipX)
                fe.RenderTransform = new ScaleTransform(-1, 1, fe.ActualWidth > 0 ? fe.ActualWidth / 2 : 100, 0);
            else
                fe.RenderTransform = System.Windows.Media.Transform.Identity;

            // 位置：使用 ActualWidth/ActualHeight（已布局的值）
            var canvasW = _canvas.ActualWidth > 0 ? _canvas.ActualWidth : 1280;
            var canvasH = _canvas.ActualHeight > 0 ? _canvas.ActualHeight : 720;
            var elemW = fe.ActualWidth > 0 ? fe.ActualWidth : (fe.Width > 0 ? fe.Width : 280);
            var elemH = fe.ActualHeight > 0 ? fe.ActualHeight : (fe.Height > 0 ? fe.Height : 46);

            Canvas.SetLeft(fe, (canvasW - elemW) * transform.X);
            Canvas.SetTop(fe, (canvasH - elemH) * transform.Y);
        }

        // 根据组件类型特殊处理。返回 false 表示 bitmap 还在后台解码中。
        return comp switch
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
            _ => true
        };
    }

    private void RemoveVisual(Component comp)
    {
        if (_visualMap.TryGetValue(comp, out var element))
        {
            _canvas.Children.Remove(element);
            _visualMap.Remove(comp);
        }
    }

    // ── 组件 → WPF 控件创建 ──

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

    private static Border CreateDialogueBoxElement()
    {
        return new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(20, 14, 20, 14),
            MinHeight = 120,
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = new StackPanel
            {
                Children =
                {
                    new System.Windows.Controls.TextBlock
                    {
                        Name = "SpeakerLabel",
                        FontWeight = FontWeights.Bold,
                        FontSize = 16,
                        Foreground = Brushes.Cyan,
                        Margin = new Thickness(0, 0, 0, 8),
                    },
                    new System.Windows.Controls.TextBlock
                    {
                        Name = "TextContent",
                        FontSize = 18,
                        Foreground = Brushes.White,
                        TextWrapping = TextWrapping.Wrap,
                    }
                }
            }
        };
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

    // ── 组件属性 → WPF 控件更新 ──

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
                img.Width = activeSprite.Width > 0 ? activeSprite.Width : (bitmap.Width > 0 ? bitmap.Width : double.NaN);
                img.Height = activeSprite.Height > 0 ? activeSprite.Height : (bitmap.Height > 0 ? bitmap.Height : double.NaN);
            }
            else { sr.MarkDirty(); return false; } // 后台解码中，下帧重试
            img.Opacity = sr.Tint.A / 255f * (sr.GameObject?.Transform.Opacity ?? 1f);
        }
        return true;
    }

    private bool UpdateFlashOverlay(FlashOverlay overlay, UIElement element)
    {
        if (element is System.Windows.Shapes.Rectangle rect)
        {
            var alpha = overlay.GameObject?.Transform.Opacity ?? 0f;
            rect.Fill = new SolidColorBrush(
                System.Windows.Media.Color.FromArgb(
                    (byte)(overlay.Color.A * alpha),
                    overlay.Color.R, overlay.Color.G, overlay.Color.B));
            rect.Opacity = 1; // color alpha handles it
        }
        return true;
    }

    private bool UpdateBackgroundRenderer(BackgroundRenderer bg, UIElement element)
    {
        if (element is not Grid grid) return true;
        Panel.SetZIndex(grid, -100);

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
        if (element is Border border && border.Child is StackPanel sp)
        {
            var wpfColor = ColorConversion.ToWpf(db.BoxColor);
            border.Background = new SolidColorBrush(wpfColor);

            border.Width = (_canvas.ActualWidth > 0 ? _canvas.ActualWidth : 1280) - 40;
            Canvas.SetLeft(border, 20);
            Canvas.SetTop(border, (_canvas.ActualHeight > 0 ? _canvas.ActualHeight : 720) - 160);

            if (sp.Children[0] is System.Windows.Controls.TextBlock speakerLabel)
            {
                speakerLabel.Text = db.SpeakerName;
                speakerLabel.Visibility = string.IsNullOrEmpty(db.SpeakerName)
                    ? Visibility.Collapsed : Visibility.Visible;
            }

            if (sp.Children[1] is System.Windows.Controls.TextBlock textContent)
            {
                textContent.Text = db.Text; // 打字机渐进文本（非 FullText）
            }
        }
        return true;
    }

    private int _choiceGroupLastCount = -1;
    private string[]? _choiceGroupLastTexts;

    private bool UpdateChoiceGroup(ChoiceGroup cg, UIElement element)
    {
        if (element is not StackPanel sp) return true;

        // 居中定位
        var w = _canvas.ActualWidth > 10 ? _canvas.ActualWidth : 1280;
        var h = _canvas.ActualHeight > 10 ? _canvas.ActualHeight : 720;
        sp.Width = 300;
        Canvas.SetLeft(sp, (w - 300) / 2);
        Canvas.SetTop(sp, Math.Max(80, (h - cg.ChoiceCount * 56) / 2));

        // 仅在选项内容变化时重建按钮
        var texts = cg.ChoiceTexts ?? Array.Empty<string>();
        bool needsRebuild = cg.ChoiceCount != _choiceGroupLastCount ||
            !TextArraysEqual(texts, _choiceGroupLastTexts);

        if (needsRebuild)
        {
            _choiceGroupLastCount = cg.ChoiceCount;
            _choiceGroupLastTexts = texts.ToArray();

            sp.Children.Clear();
            for (int i = 0; i < texts.Length; i++)
            {
                var idx = i;
                var btn = new System.Windows.Controls.Button
                {
                    Content = texts[i],
                    Width = 280,
                    Height = 46,
                    Margin = new Thickness(0, 4, 0, 4),
                    Tag = idx,
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 40)),
                    Foreground = Brushes.White,
                    FontSize = 16,
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 80)),
                    BorderThickness = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    IsHitTestVisible = true,
                    Focusable = true,
                };
                btn.Click += (s, e) =>
                {
                    Diagnostics.Logger.Info("UI", $"Button [{idx}] '{texts[idx]}' clicked");
                    cg.Select(idx);
                };
                sp.Children.Add(btn);
            }
        }
        return true;
    }

    // ── BacklogView ──

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

        var w = _canvas.ActualWidth > 10 ? _canvas.ActualWidth : 1280;
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

    // ── AdvanceIndicator ──

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

        // 闪烁效果：基于帧号调透明度
        var alpha = (byte)(128 + (ai.CurrentFrame % 6) * 21); // 128-233 循环
        tb.Foreground = new SolidColorBrush(
            System.Windows.Media.Color.FromArgb(alpha, 0x3E, 0xBF, 0xBF));

        var w = _canvas.ActualWidth > 10 ? _canvas.ActualWidth : 1280;
        Canvas.SetLeft(tb, w - 80);
        Canvas.SetTop(tb, 500);
        Panel.SetZIndex(tb, 200);

        return true;
    }

    // ── PhoneScreen ──

    private static Border CreatePhoneElement()
    {
        return new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0A, 0x0A, 0x0A)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0xBF, 0xBF)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Width = 360, Height = 520,
            IsHitTestVisible = true,
            Child = new StackPanel { Name = "PhoneContent" },
        };
    }

    private PhoneScreen.State _phoneLastState = PhoneScreen.State.Closed;
    private int _phoneLastKeyword = -1;

    private bool UpdatePhoneScreen(PhoneScreen ps, UIElement element)
    {
        if (!ps.IsVisible) { element.Visibility = Visibility.Collapsed; return true; }
        element.Visibility = Visibility.Visible;

        if (element is not Border border || border.Child is not StackPanel sp) return true;

        var w = _canvas.ActualWidth > 10 ? _canvas.ActualWidth : 1280;
        Canvas.SetLeft(border, (w - 360) / 2);
        Canvas.SetTop(border, 60);
        Panel.SetZIndex(border, 800);

        // 只在状态变化时重建
        if (ps.CurrentState == _phoneLastState && ps.SelectedKeyword == _phoneLastKeyword) return true;
        _phoneLastState = ps.CurrentState;
        _phoneLastKeyword = ps.SelectedKeyword;

        sp.Children.Clear();

        switch (ps.CurrentState)
        {
            case PhoneScreen.State.IncomingCall:
                sp.Children.Add(new TextBlock
                {
                    Text = $"📱 INCOMING CALL",
                    FontSize = 20, Foreground = Brushes.Cyan, Margin = new Thickness(0, 20, 0, 10),
                });
                sp.Children.Add(new TextBlock
                {
                    Text = ps.CallerName,
                    FontSize = 28, FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10),
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $"⏱ {ps.CallTimer:F1}s",
                    FontSize = 18, Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88)),
                });
                break;

            case PhoneScreen.State.InCall:
                sp.Children.Add(new TextBlock
                {
                    Text = $"📱 IN CALL",
                    FontSize = 20, Foreground = Brushes.Cyan, Margin = new Thickness(0, 20, 0, 10),
                });
                sp.Children.Add(new TextBlock
                {
                    Text = ps.CallerName,
                    FontSize = 28, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
                });
                break;

            case PhoneScreen.State.MailList:
                sp.Children.Add(new TextBlock
                {
                    Text = "📧 INBOX",
                    FontSize = 22, Foreground = Brushes.Cyan, Margin = new Thickness(0, 10, 0, 10),
                });
                sp.Children.Add(new TextBlock
                {
                    Text = "收件箱为空（章节脚本驱动）",
                    FontSize = 16, Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88)),
                });
                break;

            case PhoneScreen.State.MailView:
                sp.Children.Add(new TextBlock
                {
                    Text = $"From: {ps.MailSender}",
                    FontSize = 16, Foreground = Brushes.Cyan, Margin = new Thickness(0, 10, 0, 5),
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $"Subject: {ps.MailSubject}",
                    FontSize = 18, FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10),
                });
                sp.Children.Add(new TextBlock
                {
                    Text = ps.MailBody,
                    FontSize = 16, Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xE8, 0xE8)),
                    TextWrapping = TextWrapping.Wrap,
                });
                break;

            case PhoneScreen.State.ReplySelect:
                sp.Children.Add(new TextBlock
                {
                    Text = "Reply with:",
                    FontSize = 18, Foreground = Brushes.Cyan, Margin = new Thickness(0, 10, 0, 10),
                });
                foreach (var kw in ps.MailKeywords)
                {
                    var kwBtn = new Button
                    {
                        Content = kw,
                        Width = 280, Height = 36,
                        Margin = new Thickness(0, 3, 0, 3),
                        Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 40)),
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0xBF, 0xBF)),
                        FontSize = 15,
                        BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x8A, 0x8A)),
                        BorderThickness = new Thickness(1),
                        Cursor = System.Windows.Input.Cursors.Hand,
                    };
                    var idx = Array.IndexOf(ps.MailKeywords, kw);
                    kwBtn.Click += (_, _) => ps.SelectKeyword(idx);
                    sp.Children.Add(kwBtn);
                }
                break;
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
            var w = _canvas.ActualWidth > 0 ? _canvas.ActualWidth : 1280;
            tb.Width = w * 0.8;
            Canvas.SetLeft(tb, w * 0.1);
            Canvas.SetTop(tb, tr.GameObject?.Transform.Y * (_canvas.ActualHeight > 0 ? _canvas.ActualHeight : 720) ?? 0);
        }
        return true;
    }

    // ── 工具 ──

    /// <summary>加载 BitmapImage，按 Sprite 路径缓存解码结果。
    /// 缓存命中 → 即时返回。缓存未命中 → 启动后台解码，返回 null（下帧重试即可命中）。</summary>
    private static BitmapImage? LoadCachedBitmap(Sprite sprite)
    {
        var key = sprite.Path;
        if (_bitmapCache.TryGetValue(key, out var cached))
            return cached;

        // 缓存未命中 — 启动后台解码，不阻塞 UI 线程
        if (_pendingLoads.TryAdd(key, 0))
        {
            Logger.Warning("WpfRenderer", $"Cache miss for '{Path.GetFileName(key)}' — starting background decode. Add to ChapterAssets to preload.");
            Task.Run(() =>
            {
                try
                {
                    var rawBytes = File.ReadAllBytes(key);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new MemoryStream(rawBytes);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    _bitmapCache[key] = bitmap;
                }
                catch (Exception ex)
                {
                    Logger.Error("WpfRenderer", $"Background decode failed for '{key}': {ex.Message}");
                }
                finally
                {
                    _pendingLoads.TryRemove(key, out _);
                }
            });
        }
        return null; // 下帧重试
    }

    private static BitmapImage? LoadBitmapFromFile(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
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

    /// <summary>预热 BitmapImage 缓存。在脚本预加载阶段调用，避免首次渲染时解码阻塞。</summary>
    internal static void WarmupBitmap(Sprite sprite)
    {
        LoadCachedBitmap(sprite);
    }

    /// <summary>
    /// 后台并行预加载所有图片。在 Splash/加载页面调用。
    /// 使用 Task.Run + Parallel.ForEach 在后台线程解码 PNG，Freeze 后缓存到 ConcurrentDictionary。
    /// 通过 IProgress 报告进度（0-100）。
    /// </summary>
    /// <param name="paths">图片文件的绝对路径列表</param>
    /// <param name="progress">可选进度回调（报告 0-100 的完成百分比）</param>
    internal static async Task WarmupBitmapsAsync(IEnumerable<string> paths, IProgress<int>? progress = null)
    {
        var toLoad = paths.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p) && !_bitmapCache.ContainsKey(p)).ToList();
        if (toLoad.Count == 0)
        {
            progress?.Report(100);
            return;
        }

        int loaded = 0;
        await Task.Run(() =>
        {
            Parallel.ForEach(toLoad, path =>
            {
                try
                {
                    var rawBytes = File.ReadAllBytes(path);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new MemoryStream(rawBytes);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze(); // 跨线程安全
                    _bitmapCache[path] = bitmap; // ConcurrentDictionary, 无需 lock
                }
                catch
                {
                    // 跳过损坏的文件
                }

                int done = Interlocked.Increment(ref loaded);
                progress?.Report(done * 100 / toLoad.Count);
            });
        });
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
}

/// <summary>
/// 内部颜色转换工具。
/// </summary>
internal static class ColorConversion
{
    public static System.Windows.Media.Color ToWpf(Drawing.Color c)
    {
        return System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
    }
}
