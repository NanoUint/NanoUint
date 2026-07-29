using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NanoUint.Debugging;

/// <summary>
/// 属性查看器 Tab。显示选中 GameObject 的所有可编辑属性。
/// Transform 的 X/Y/Opacity/FlipX/SortingOrder 可直接编辑，修改实时同步到画面。
/// </summary>
internal sealed class InspectorTab
{
    private ScrollViewer? _scrollViewer;
    private StackPanel? _contentPanel;
    private GameObject? _currentTarget;
    private TextBlock? _titleLabel;

    public UIElement Build()
    {
        _contentPanel = new StackPanel { Margin = new Thickness(0) };

        _scrollViewer = new ScrollViewer
        {
            Content = _contentPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        ShowEmpty();
        return _scrollViewer;
    }

    /// <summary>选择要查看的对象。</summary>
    public void Inspect(GameObject? go)
    {
        _currentTarget = go;

        if (_contentPanel == null) return;
        _contentPanel.Children.Clear();

        if (go == null || go.IsDestroyed)
        {
            ShowEmpty();
            return;
        }

        // ── 标题 ──
        var titleBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 4, 6, 4) };
        var activeDot = new TextBlock
        {
            Text = go.ActiveSelf ? "●" : "○",
            Foreground = go.ActiveSelf ? Brushes.LimeGreen : Brushes.Gray,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        };
        _titleLabel = new TextBlock
        {
            Text = go.Name,
            Foreground = Brushes.Cyan,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        titleBar.Children.Add(activeDot);
        titleBar.Children.Add(_titleLabel);
        _contentPanel.Children.Add(titleBar);

        // ActiveSelf 开关
        var activeRow = NewRow("Active", null);
        var activeChk = new CheckBox
        {
            IsChecked = go.ActiveSelf,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        };
        activeChk.Checked += (_, _) => go.ActiveSelf = true;
        activeChk.Unchecked += (_, _) => go.ActiveSelf = false;
        AddControl(activeRow, activeChk);
        _contentPanel.Children.Add(activeRow);

        // ── 分隔线 ──
        _contentPanel.Children.Add(Separator());

        // ── Transform ──
        _contentPanel.Children.Add(SectionHeader("Transform"));
        var t = go.Transform;

        // X
        var xRow = NewRow("X", "0=左, 0.5=中, 1=右");
        var xBox = NewFloatBox(t.X, v => t.X = v);
        AddControl(xRow, xBox);
        _contentPanel.Children.Add(xRow);

        // Y
        var yRow = NewRow("Y", "0=上, 1=下");
        var yBox = NewFloatBox(t.Y, v => t.Y = v);
        AddControl(yRow, yBox);
        _contentPanel.Children.Add(yRow);

        // Opacity
        var opacityRow = NewRow("Opacity", "0~1");
        var opacityPanel = new StackPanel { Orientation = Orientation.Horizontal };
        var opacityBox = NewFloatBox(t.Opacity, v => t.Opacity = v, 60);
        var opacitySlider = new Slider
        {
            Minimum = 0, Maximum = 1,
            Value = t.Opacity,
            Width = 120,
            TickFrequency = 0.05,
            IsSnapToTickEnabled = true,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        opacityBox.TextChanged += (_, _) =>
        {
            if (float.TryParse(opacityBox.Text, out var ov))
                opacitySlider.Value = Math.Clamp(ov, 0f, 1f);
        };
        opacitySlider.ValueChanged += (_, e) =>
        {
            t.Opacity = (float)e.NewValue;
            opacityBox.Text = t.Opacity.ToString("F2");
        };
        opacityPanel.Children.Add(opacityBox);
        opacityPanel.Children.Add(opacitySlider);
        AddControl(opacityRow, opacityPanel);
        _contentPanel.Children.Add(opacityRow);

        // FlipX
        var flipRow = NewRow("FlipX", "水平翻转");
        var flipChk = new CheckBox
        {
            IsChecked = t.FlipX,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        };
        flipChk.Checked += (_, _) => t.FlipX = true;
        flipChk.Unchecked += (_, _) => t.FlipX = false;
        AddControl(flipRow, flipChk);
        _contentPanel.Children.Add(flipRow);

        // SortingOrder
        var sortRow = NewRow("SortingOrder", "渲染层级");
        var sortBox = NewIntBox(t.SortingOrder, v => t.SortingOrder = v);
        AddControl(sortRow, sortBox);
        _contentPanel.Children.Add(sortRow);

        // ── 各组件属性 ──
        foreach (var comp in go.Components)
        {
            if (comp is Transform) continue;
            BuildComponentSection(comp);
        }

        // ── 占位 ──
        if (go.Components.Count <= 1) // only Transform
        {
            _contentPanel.Children.Add(new TextBlock
            {
                Text = "(no other components)",
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                Margin = new Thickness(8, 8, 8, 8),
            });
        }
    }

    private void BuildComponentSection(Component comp)
    {
        _contentPanel!.Children.Add(Separator());

        var typeName = comp.GetType().Name;
        var enabledStr = comp.Enabled ? "" : " [OFF]";
        _contentPanel.Children.Add(SectionHeader($"{typeName}{enabledStr}"));

        // Enabled 开关
        var enRow = NewRow("Enabled", null);
        var enChk = new CheckBox
        {
            IsChecked = comp.Enabled,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        };
        enChk.Checked += (_, _) => comp.Enabled = true;
        enChk.Unchecked += (_, _) => comp.Enabled = false;
        AddControl(enRow, enChk);
        _contentPanel.Children.Add(enRow);

        // 类型特有属性
        switch (comp)
        {
            case SpriteRenderer sr:
                AddReadOnlyRow("Sprite", sr.Sprite?.Name ?? "(null)");
                AddReadOnlyRow("Path", sr.Sprite?.Path ?? "");
                break;

            case BackgroundRenderer bg:
                AddReadOnlyRow("Sprite", bg.Sprite?.Name ?? "(null)");
                AddReadOnlyRow("Fading", bg.IsCrossfading ? $"{(bg.FadeProgress * 100):F0}%" : "no");
                break;

            case TextRenderer tr:
                AddEditableRow("Content", tr.Content, v => tr.Content = v);
                AddEditableRow("FontSize", tr.FontSize.ToString(), v =>
                { if (int.TryParse(v, out var n)) tr.FontSize = n; });
                AddReadOnlyRow("Color", $"R={tr.TextColor.R} G={tr.TextColor.G} B={tr.TextColor.B} A={tr.TextColor.A}");
                break;

            case DialogueBox db:
                AddEditableRow("Speaker", db.SpeakerName, v => db.SpeakerName = v);
                AddReadOnlyRow("Text", Truncate(db.FullText, 40));
                AddEditableRow("TextSpeed", db.TextSpeed.ToString("F3"), v =>
                { if (float.TryParse(v, out var s)) db.TextSpeed = s; });
                AddReadOnlyRow("BoxColor", $"R={db.BoxColor.R} G={db.BoxColor.G} B={db.BoxColor.B}");
                break;

            case ChoiceGroup cg:
                AddReadOnlyRow("Options", $"{cg.ChoiceCount} items");
                AddReadOnlyRow("HasResult", cg.HasResult.ToString());
                if (cg.ChoiceTexts is { Length: > 0 } texts)
                {
                    for (int i = 0; i < texts.Length; i++)
                        AddReadOnlyRow($"  [{i}]", texts[i]);
                }
                break;

            case AudioSource a:
                AddReadOnlyRow("Clip", a.Clip?.Name ?? "(null)");
                AddReadOnlyRow("Looping", a.IsLooping.ToString());
                AddReadOnlyRow("Volume", a.Volume.ToString("F2"));
                break;
        }
    }

    // ── UI 构建辅助 ──

    private void ShowEmpty()
    {
        _contentPanel?.Children.Clear();
        _contentPanel?.Children.Add(new TextBlock
        {
            Text = "Select a GameObject\nfrom the Scene Tree tab",
            Foreground = Brushes.Gray,
            FontStyle = FontStyles.Italic,
            FontSize = 12,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(8, 20, 8, 8),
        });
    }

    private static Border NewRow(string label, string? tooltip)
    {
        var row = new Border
        {
            Padding = new Thickness(6, 2, 6, 2),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 55)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = Brushes.Transparent,
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelTb = new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(160, 180, 200)),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = tooltip,
        };
        Grid.SetColumn(labelTb, 0);
        grid.Children.Add(labelTb);

        row.Child = grid;
        row.Tag = grid; // 用于后续 AddControl
        return row;
    }

    private static void AddControl(Border row, UIElement control)
    {
        if (row.Tag is Grid grid)
            Grid.SetColumn(control, 1);
        if (row.Child is Grid g)
        {
            // 确保控件放在第二列
            var existing = g.Children.OfType<UIElement>().FirstOrDefault(c => Grid.GetColumn(c) == 1);
            if (existing != null) g.Children.Remove(existing);
            Grid.SetColumn(control, 1);
            g.Children.Add(control);
        }
    }

    private static TextBox NewFloatBox(float value, Action<float> onChanged, int width = 70)
    {
        var box = new TextBox
        {
            Text = value.ToString("F3"),
            Width = width,
            FontSize = 12,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 80)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        box.LostFocus += (_, _) => Commit(box, onChanged);
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(box, onChanged); };
        return box;
    }

    private static TextBox NewIntBox(int value, Action<int> onChanged, int width = 70)
    {
        var box = new TextBox
        {
            Text = value.ToString(),
            Width = width,
            FontSize = 12,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 80)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        box.LostFocus += (_, _) => { if (int.TryParse(box.Text, out var v)) onChanged(v); };
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter && int.TryParse(box.Text, out var v)) onChanged(v); };
        return box;
    }

    private static void Commit(TextBox box, Action<float> onChanged)
    {
        if (float.TryParse(box.Text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
            onChanged(v);
    }

    private void AddReadOnlyRow(string label, string value)
    {
        var row = NewRow(label, null);
        var tb = new TextBlock
        {
            Text = value,
            Foreground = Brushes.LightGray,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 280,
        };
        AddControl(row, tb);
        _contentPanel!.Children.Add(row);
    }

    private void AddEditableRow(string label, string value, Action<string> onChanged)
    {
        var row = NewRow(label, null);
        var box = new TextBox
        {
            Text = value,
            FontSize = 12,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 40)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 80)),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 100,
        };
        box.LostFocus += (_, _) => onChanged(box.Text);
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) onChanged(box.Text); };
        AddControl(row, box);
        _contentPanel!.Children.Add(row);
    }

    private static Border Separator()
    {
        return new Border
        {
            Height = 1,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 70)),
            Margin = new Thickness(4, 2, 4, 2),
        };
    }

    private static TextBlock SectionHeader(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 100)),
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Margin = new Thickness(6, 6, 6, 2),
        };
    }

    private static string Truncate(string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= maxLen ? s : s[..(maxLen - 3)] + "...";
    }
}
