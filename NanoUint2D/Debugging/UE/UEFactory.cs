using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Shapes;

namespace NanoUint.Debugging.UE;

internal static class UEFactory
{

    #region Color Utilities

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

    #region Check (17x17 square checkbox)

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

        // FrameworkElementFactory.AppendChild requires the PARENT type to implement IAddChild (the
        // exception names the parent); child type is irrelevant. Grid/Border/StackPanel/TextBlock/AccessText/ScrollViewer do; Path/Image/ContentPresenter/Track/Thumb/ScrollBar do not.
        var check = new FrameworkElementFactory(typeof(Path));
        check.Name = "check";
        check.SetValue(Path.DataProperty, Geometry.Parse("M 1,7 L 6,12 L 15,3"));
        check.SetValue(Path.StrokeProperty, new TemplateBindingExtension(CheckBox.ForegroundProperty));
        check.SetValue(Path.StrokeThicknessProperty, 2d);
        check.SetValue(Path.StrokeStartLineCapProperty, PenLineCap.Round);
        check.SetValue(Path.StrokeEndLineCapProperty, PenLineCap.Round);
        check.SetValue(Path.StrokeLineJoinProperty, PenLineJoin.Round);
        check.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        check.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        grid.AppendChild(check);

        var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = grid };

        var checkedT = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        checkedT.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "check"));
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

    #region Dark Scrollbar

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
        // Track hosts several children but does not implement IAddChild, so its subtree cannot be
        // built with FrameworkElementFactory; XamlReader.Parse is used instead, bypassing that restriction.
        var thumb = UEPalette.ScopeUnselected;
        var orientation = vertical ? "Vertical" : "Horizontal";
        string xaml = $@"<ControlTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                                 xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                                 TargetType=""ScrollBar"">
  <Grid Background=""Transparent"">
    <Track x:Name=""PART_Track"" IsDirectionReversed=""{vertical}"" Orientation=""{orientation}"">
      <Track.DecreaseRepeatButton>
        <RepeatButton Command=""ScrollBar.PageUpCommand"" Opacity=""0"" Focusable=""False"">
          <RepeatButton.Template>
            <ControlTemplate TargetType=""RepeatButton""><Border Background=""Transparent""/></ControlTemplate>
          </RepeatButton.Template>
        </RepeatButton>
      </Track.DecreaseRepeatButton>
      <Track.Thumb>
        <Thumb Background=""#{thumb.R:X2}{thumb.G:X2}{thumb.B:X2}"" BorderThickness=""0"" IsTabStop=""False""/>
      </Track.Thumb>
      <Track.IncreaseRepeatButton>
        <RepeatButton Command=""ScrollBar.PageDownCommand"" Opacity=""0"" Focusable=""False"">
          <RepeatButton.Template>
            <ControlTemplate TargetType=""RepeatButton""><Border Background=""Transparent""/></ControlTemplate>
          </RepeatButton.Template>
        </RepeatButton>
      </Track.IncreaseRepeatButton>
    </Track>
  </Grid>
</ControlTemplate>";
        return (ControlTemplate)XamlReader.Parse(xaml);
    }

    public static void ApplyScrollbarStyle(ScrollViewer scroller)
    {
        scroller.Resources[typeof(ScrollBar)] = ScrollbarStyle();
    }

    #endregion

    #region Containers

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
