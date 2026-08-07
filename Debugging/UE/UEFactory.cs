using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace NanoUint.Debugging.UE;

/// <summary>UnityExplorer 风格控件工厂。</summary>
internal static class UEFactory
{

    #region 颜色工具

    internal static Color Lighten(Color c, double factor)
    {
        return Color.FromRgb(
            (byte)Math.Min(255, c.R + (255 - c.R) * factor),
            (byte)Math.Min(255, c.G + (255 - c.G) * factor),
            (byte)Math.Min(255, c.B + (255 - c.B) * factor));
    }

    internal static Color Darken(Color c, double factor)
    {
        return Color.FromRgb(
            (byte)(c.R * (1 - factor)),
            (byte)(c.G * (1 - factor)),
            (byte)(c.B * (1 - factor)));
    }

    #endregion

    #region Label

    /// <summary>UE 风格文本标签。</summary>
    public static TextBlock Label(string text, double fontSize = 12,
        Color? foreground = null, FontStyle? fontStyle = null,
        FontWeight? weight = null, FontFamily? font = null,
        TextTrimming trimming = TextTrimming.None)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontFamily = font ?? UEPalette.DefaultFont,
            Foreground = new SolidColorBrush(foreground ?? UEPalette.TextDefault),
            FontStyle = fontStyle ?? FontStyles.Normal,
            FontWeight = weight ?? FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = trimming,
        };
    }

    #endregion

    #region Button

    /// <summary>UE 风格按钮: 方形、无圆角、hover 提亮 25%、pressed 变暗 20%、禁用 #404040。</summary>
    public static Button Button(string text, double width, double height, Color background,
        double fontSize = 12, Color? foreground = null, FontWeight? weight = null)
    {
        var btn = new Button
        {
            Content = text,
            Width = width,
            Height = height,
            FontSize = fontSize,
            FontFamily = UEPalette.DefaultFont,
            Background = new SolidColorBrush(background),
            Foreground = new SolidColorBrush(foreground ?? UEPalette.TextDefault),
            FontWeight = weight ?? FontWeights.Normal,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Focusable = false,
        };
        btn.Template = CreateButtonTemplate(background);
        return btn;
    }

    /// <summary>文本自动适应宽度的按钮(UnityExplorer 的自适应按钮)。</summary>
    public static Button ButtonAuto(string text, double height, Color background,
        double fontSize = 12, Color? foreground = null, double minWidth = 0,
        double horizontalPadding = 8)
    {
        var btn = Button(text, double.NaN, height, background, fontSize, foreground);
        btn.MinWidth = minWidth;
        btn.Padding = new Thickness(horizontalPadding, 0, horizontalPadding, 0);
        return btn;
    }

    private static ControlTemplate CreateButtonTemplate(Color background)
    {
        var hover = Lighten(background, 0.25);
        var pressed = Darken(background, 0.20);

        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "bd";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(System.Windows.Controls.Button.BackgroundProperty));
        border.SetValue(Border.SnapsToDevicePixelsProperty, true);

        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(cp);

        var template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = border };

        var hoverT = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverT.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hover), "bd"));
        template.Triggers.Add(hoverT);

        var pressedT = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressedT.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(pressed), "bd"));
        template.Triggers.Add(pressedT);

        var disabledT = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabledT.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(UEPalette.ButtonDisabled), "bd"));
        template.Triggers.Add(disabledT);

        return template;
    }

    #endregion

    #region Input

    /// <summary>UE 风格输入框: 深色、1px 边框、聚焦时边框提亮。</summary>
    public static TextBox Input(double width, double height, string text = "",
        Color? background = null, Color? border = null, double fontSize = 12,
        bool readOnly = false, bool acceptReturn = false)
    {
        var box = new TextBox
        {
            Text = text,
            Width = width,
            Height = height,
            FontSize = fontSize,
            FontFamily = UEPalette.DefaultFont,
            Background = new SolidColorBrush(background ?? UEPalette.InputBackground),
            BorderBrush = new SolidColorBrush(border ?? UEPalette.InputBorder),
            Foreground = new SolidColorBrush(UEPalette.TextDefault),
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(3, 0, 3, 0),
            CaretBrush = new SolidColorBrush(UEPalette.TextDefault),
            IsReadOnly = readOnly,
            AcceptsReturn = acceptReturn,
            SelectionBrush = new SolidColorBrush(UEPalette.ButtonEnabled),
        };
        box.Template = CreateInputTemplate();
        return box;
    }

    private static ControlTemplate CreateInputTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "bd";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(TextBox.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(TextBox.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.SnapsToDevicePixelsProperty, true);

        var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
        scroll.Name = "PART_ContentHost";
        scroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        scroll.SetValue(ScrollViewer.MarginProperty, new Thickness(2, 0, 2, 0));
        border.AppendChild(scroll);

        var template = new ControlTemplate(typeof(TextBox)) { VisualTree = border };

        var focusT = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        focusT.Setters.Add(new Setter(Border.BorderBrushProperty,
            new SolidColorBrush(Lighten(UEPalette.InputBorder, 0.5)), "bd"));
        template.Triggers.Add(focusT);

        return template;
    }

    #endregion

    #region Check (17x17 方形勾选框)

    /// <summary>UE 风格方形勾选框。</summary>
    public static CheckBox Check(bool isChecked, Action<bool>? onChanged = null,
        Color? graphic = null, double size = 17)
    {
        var chk = new CheckBox
        {
            IsChecked = isChecked,
            Width = size,
            Height = size,
            Cursor = Cursors.Hand,
            Foreground = new SolidColorBrush(graphic ?? UEPalette.TextDefault),
            Focusable = false,
        };
        chk.Template = CreateCheckTemplate();
        if (onChanged != null)
            chk.Click += (_, _) => onChanged(chk.IsChecked == true);
        return chk;
    }

    private static ControlTemplate CreateCheckTemplate()
    {
        var grid = new FrameworkElementFactory(typeof(Grid));
        grid.SetValue(Grid.BackgroundProperty, Brushes.Transparent);

        var box = new FrameworkElementFactory(typeof(Border));
        box.Name = "box";
        box.SetValue(Border.BorderBrushProperty, new SolidColorBrush(UEPalette.InputBorder));
        box.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        box.SetValue(Border.BackgroundProperty, new SolidColorBrush(UEPalette.InputBackground));
        box.SetValue(Border.SnapsToDevicePixelsProperty, true);
        grid.AppendChild(box);

        var check = new FrameworkElementFactory(typeof(TextBlock));
        check.Name = "check";
        check.SetValue(TextBlock.TextProperty, "✓");
        check.SetValue(TextBlock.ForegroundProperty, new TemplateBindingExtension(CheckBox.ForegroundProperty));
        check.SetValue(TextBlock.FontSizeProperty, 11d);
        check.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        check.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        check.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        check.SetValue(TextBlock.VisibilityProperty, Visibility.Collapsed);
        grid.AppendChild(check);

        var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = grid };

        var checkedT = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        checkedT.Setters.Add(new Setter(TextBlock.VisibilityProperty, Visibility.Visible, "check"));
        template.Triggers.Add(checkedT);

        var hoverT = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverT.Setters.Add(new Setter(Border.BorderBrushProperty,
            new SolidColorBrush(Lighten(UEPalette.InputBorder, 0.5)), "box"));
        template.Triggers.Add(hoverT);

        return template;
    }

    #endregion

    #region Separator

    public static Border Separator(Color? color = null, double margin = 4, double thickness = 1)
    {
        return new Border
        {
            Height = thickness,
            Background = new SolidColorBrush(color ?? new Color { R = 0x40, G = 0x40, B = 0x40, A = 0xFF }),
            Margin = new Thickness(margin, 2, margin, 2),
        };
    }

    #endregion

    #region 暗色滚动条

    /// <summary>暗色细滚动条样式 (UnityExplorer uGUI scrollbar 风格)。</summary>
    public static Style ScrollbarStyle()
    {
        var style = new Style(typeof(ScrollBar));
        style.Setters.Add(new Setter(ScrollBar.WidthProperty, 10d));
        style.Setters.Add(new Setter(ScrollBar.BackgroundProperty, Brushes.Transparent));

        var vT = new Trigger { Property = ScrollBar.OrientationProperty, Value = Orientation.Vertical };
        vT.Setters.Add(new Setter(Control.TemplateProperty, CreateScrollBarTemplate(vertical: true)));
        style.Triggers.Add(vT);

        var hT = new Trigger { Property = ScrollBar.OrientationProperty, Value = Orientation.Horizontal };
        hT.Setters.Add(new Setter(Control.TemplateProperty, CreateScrollBarTemplate(vertical: false)));
        style.Triggers.Add(hT);

        return style;
    }

    private static ControlTemplate CreateScrollBarTemplate(bool vertical)
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        root.SetValue(Grid.BackgroundProperty, Brushes.Transparent);

        var track = new FrameworkElementFactory(typeof(Track));
        track.Name = "PART_Track";
        track.SetValue(Track.IsDirectionReversedProperty, vertical);
        track.SetValue(Track.OrientationProperty,
            vertical ? Orientation.Vertical : Orientation.Horizontal);

        // 隐藏的 RepeatButton(上下箭头区域透明)
        var dec = new FrameworkElementFactory(typeof(RepeatButton));
        dec.SetValue(RepeatButton.CommandProperty, ScrollBar.PageUpCommand);
        dec.SetValue(UIElement.OpacityProperty, 0d);
        dec.SetValue(UIElement.FocusableProperty, false);
        var decT = new FrameworkElementFactory(typeof(Border));
        decT.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        dec.SetValue(Control.TemplateProperty, new ControlTemplate(typeof(RepeatButton)) { VisualTree = decT });
        track.AppendChild(dec); // DecreaseRepeatButton

        var thumb = new FrameworkElementFactory(typeof(Thumb));
        thumb.SetValue(Thumb.BackgroundProperty, new SolidColorBrush(UEPalette.ScopeUnselected));
        thumb.SetValue(Thumb.BorderThicknessProperty, new Thickness(0));
        thumb.SetValue(Thumb.IsTabStopProperty, false);
        track.AppendChild(thumb); // Thumb

        var inc = new FrameworkElementFactory(typeof(RepeatButton));
        inc.SetValue(RepeatButton.CommandProperty, ScrollBar.PageDownCommand);
        inc.SetValue(UIElement.OpacityProperty, 0d);
        inc.SetValue(UIElement.FocusableProperty, false);
        var incT = new FrameworkElementFactory(typeof(Border));
        incT.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        inc.SetValue(Control.TemplateProperty, new ControlTemplate(typeof(RepeatButton)) { VisualTree = incT });
        track.AppendChild(inc); // IncreaseRepeatButton

        root.AppendChild(track);
        return new ControlTemplate(typeof(ScrollBar)) { VisualTree = root };
    }

    /// <summary>把暗色滚动条样式应用到 ScrollViewer。</summary>
    public static void ApplyScrollbarStyle(ScrollViewer scroller)
    {
        scroller.Resources[typeof(ScrollBar)] = ScrollbarStyle();
    }

    #endregion

    #region 容器

    /// <summary>UE 风格行容器(两列: 名称列 + 值列, UnityExplorer 成员行)。</summary>
    public static Grid Row(double leftWidth, bool nameRightAlign = false)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(leftWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return grid;
    }

    public static void SetCell(UIElement element, Grid grid, int column)
    {
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }
    #endregion
}
