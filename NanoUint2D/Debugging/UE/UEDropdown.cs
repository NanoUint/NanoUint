using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace NanoUint.Debugging.UE;

internal sealed class UEDropdown : ContentControl
{
    private readonly TextBlock _valueText;
    private readonly Popup _popup;
    private readonly ListBox _list;
    private IReadOnlyList<string> _items = Array.Empty<string>();
    private int _selectedIndex = -1;

    public event Action<int>? SelectionChanged;

    public UEDropdown(double width, double height, double fontSize = 13)
    {
        var display = new Border
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(UEPalette.SubtleButton),
            BorderBrush = new SolidColorBrush(UEPalette.InputBorder),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _valueText = UEFactory.Label("", fontSize, UEPalette.TextDefault);
        _valueText.Margin = new Thickness(4, 0, 0, 0);
        UEFactory.SetCell(_valueText, grid, 0);

        var arrow = UEFactory.Label("▾", fontSize, UEPalette.TextInactive);
        arrow.Margin = new Thickness(0, 0, 4, 0);
        UEFactory.SetCell(arrow, grid, 1);

        display.Child = grid;
        display.MouseLeftButtonDown += (_, _) => TogglePopup();
        display.MouseEnter += (_, _) =>
            display.Background = new SolidColorBrush(UEFactory.Lighten(UEPalette.SubtleButton, 0.25));
        display.MouseLeave += (_, _) =>
            display.Background = new SolidColorBrush(UEPalette.SubtleButton);

        _list = new ListBox
        {
            Background = new SolidColorBrush(UEPalette.PanelContent),
            Foreground = new SolidColorBrush(UEPalette.TextDefault),
            BorderBrush = new SolidColorBrush(UEPalette.InputBorder),
            BorderThickness = new Thickness(1),
            FontFamily = UEPalette.DefaultFont,
            FontSize = fontSize,
            MaxHeight = 220,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
        _list.Resources[typeof(ScrollBar)] = UEFactory.ScrollbarStyle();
        _list.ItemContainerStyle = CreateItemStyle(fontSize);

        _popup = new Popup
        {
            Child = _list,
            StaysOpen = false,
            AllowsTransparency = true,
            Placement = PlacementMode.Bottom,
            PlacementTarget = display,
        };

        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedIndex >= 0 && _list.SelectedIndex != _selectedIndex)
            {
                _selectedIndex = _list.SelectedIndex;
                _valueText.Text = _items[_selectedIndex];
                _popup.IsOpen = false;
                SelectionChanged?.Invoke(_selectedIndex);
            }
        };

        Content = display;
    }

    public IReadOnlyList<string> Items
    {
        get => _items;
        set
        {
            _items = value;
            _list.ItemsSource = value;
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            _selectedIndex = value;
            _list.SelectedIndex = value;
            _valueText.Text = value >= 0 && value < _items.Count ? _items[value] : "";
        }
    }

    public string DisplayText
    {
        get => _valueText.Text;
        set => _valueText.Text = value;
    }

    private void TogglePopup()
    {
        if (_items.Count == 0) return;
        _popup.IsOpen = !_popup.IsOpen;
        if (_popup.IsOpen && _selectedIndex >= 0)
        {
            _list.SelectedIndex = _selectedIndex;
            _list.ScrollIntoView(_list.SelectedItem);
        }
    }

    private static Style CreateItemStyle(double fontSize)
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(ListBoxItem.HeightProperty, 22d));
        style.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush(UEPalette.TextDefault)));
        style.Setters.Add(new Setter(ListBoxItem.FontSizeProperty, fontSize));
        style.Setters.Add(new Setter(ListBoxItem.FontFamilyProperty, UEPalette.DefaultFont));
        style.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(4, 0, 4, 0)));
        style.Setters.Add(new Setter(ListBoxItem.CursorProperty, Cursors.Hand));
        style.Setters.Add(new Setter(ListBoxItem.FocusableProperty, false));

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(UEPalette.ButtonDisabled)));
        style.Triggers.Add(hover);

        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(UEPalette.ButtonEnabled)));
        style.Triggers.Add(selected);

        return style;
    }
}
