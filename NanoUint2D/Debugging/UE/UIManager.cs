using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace NanoUint.Debugging.UE;

internal sealed class UIManager
{
    public static float TimeScale { get; set; } = 1f;

    private const double TopBarWidth = 1020;

    private readonly Canvas _canvas;
    private readonly Border _topBar;
    private readonly StackPanel _tabArea;
    private readonly List<(UEPanel Panel, Button Button)> _tabs = new();
    private bool _allVisible = true;

    public UIManager(Canvas canvas)
    {
        _canvas = canvas;

        _topBar = new Border
        {
            Background = new SolidColorBrush(UEPalette.TopBarBackground),
            Height = UEPalette.TopBarHeight,
            Width = ComputeWidth(),
            BorderBrush = new SolidColorBrush(UEPalette.InspectorBorder),
            BorderThickness = new Thickness(1),
        };
        Panel.SetZIndex(_topBar, 100000);

        var layout = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        #region UE title (14px, grey italic version)
        var title = new TextBlock { VerticalAlignment = VerticalAlignment.Center, MinWidth = 75 };
        title.Inlines.Add(new Run("UE ")
        {
            FontSize = 14,
            FontFamily = UEPalette.DefaultFont,
            Foreground = new SolidColorBrush(UEPalette.TextDefault),
        });
        title.Inlines.Add(new Run("v0.1")
        {
            FontSize = 14,
            FontFamily = UEPalette.DefaultFont,
            Foreground = new SolidColorBrush(UEPalette.TextInactive),
            FontStyle = FontStyles.Italic,
        });
        layout.Children.Add(title);

        #endregion

        #region Tab button area
        _tabArea = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 0, 8, 0),
        };
        layout.Children.Add(_tabArea);

        #endregion

        #region TimeScale widget
        layout.Children.Add(BuildTimeScaleWidget());

        #endregion

        #region spacer
        layout.Children.Add(new Border { Width = 15 });

        #endregion

        #region Hide button (red, shows hotkey)
        var hideBtn = UEFactory.ButtonAuto("Hide [F3]", 25, UEPalette.TopBarCloseNormal, 12);
        hideBtn.Margin = new Thickness(8, 0, 0, 0);
        hideBtn.Click += (_, _) => ToggleAll();
        layout.Children.Add(hideBtn);

        _topBar.Child = layout;
        canvas.Children.Add(_topBar);

        PositionTopCenter();
        canvas.SizeChanged += (_, _) =>
        {
            _topBar.Width = ComputeWidth();
            PositionTopCenter();
        };
        #endregion
    }

    #region Public API

    public void AddPanelTab(UEPanel panel, string label)
    {
        var btn = UEFactory.ButtonAuto(label, 25, UEPalette.ButtonDisabled, 12);
        btn.MinWidth = 80;
        btn.Margin = new Thickness(0, 0, 4, 0);
        btn.Click += (_, _) =>
        {
            if (panel.IsVisible) panel.Hide();
            else panel.Show();
            UpdateTabStates();
        };
        panel.Closed += UpdateTabStates;
        _tabs.Add((panel, btn));
        _tabArea.Children.Add(btn);
        UpdateTabStates();
    }

    public void UpdateTabStates()
    {
        foreach (var (panel, btn) in _tabs)
        {
            btn.Background = new SolidColorBrush(
                panel.IsVisible ? UEPalette.ButtonEnabled : UEPalette.ButtonDisabled);
            btn.Foreground = new SolidColorBrush(
                panel.IsVisible ? UEPalette.TextDefault : UEPalette.TextInactive);
        }
    }

    public void ToggleAll()
    {
        _allVisible = !_allVisible;
        _topBar.Visibility = _allVisible ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (panel, _) in _tabs)
        {
            if (_allVisible) panel.Show();
            else panel.Hide();
        }
        UpdateTabStates();
    }

    public void OnCanvasResized()
    {
        foreach (var (panel, _) in _tabs)
            panel.ReapplyAnchors();
    }

    #endregion

    #region Internals

    private double ComputeWidth()
    {
        var cw = _canvas.ActualWidth > 0 ? _canvas.ActualWidth : 1280;
        return Math.Min(TopBarWidth, Math.Max(300, cw - 20));
    }

    private void PositionTopCenter()
    {
        var cw = _canvas.ActualWidth > 0 ? _canvas.ActualWidth : 1280;
        Canvas.SetLeft(_topBar, Math.Max(0, (cw - _topBar.Width) / 2));
        Canvas.SetTop(_topBar, 0);
    }

    private UIElement BuildTimeScaleWidget()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var label = UEFactory.Label("TimeScale", 12, UEPalette.TextInactive);
        label.Margin = new Thickness(4, 0, 4, 0);
        panel.Children.Add(label);

        var slider = new System.Windows.Controls.Slider
        {
            Minimum = 0,
            Maximum = 5,
            Value = 1,
            Width = 100,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(UEPalette.ButtonEnabled),
            Background = new SolidColorBrush(UEPalette.InputBackground),
        };
        var input = UEFactory.Input(45, 20, "1.00", fontSize: 11);
        input.Margin = new Thickness(4, 0, 0, 0);

        void Apply(float v)
        {
            TimeScale = v;
            input.Text = v.ToString("F2");
        }

        slider.ValueChanged += (_, e) => Apply((float)e.NewValue);

        input.LostFocus += (_, _) =>
        {
            if (float.TryParse(input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                slider.Value = Math.Clamp(v, 0, 5);
            else
                input.Text = TimeScale.ToString("F2");
        };
        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter &&
                float.TryParse(input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                slider.Value = Math.Clamp(v, 0, 5);
        };

        panel.Children.Add(slider);
        panel.Children.Add(input);
        return panel;
    }
    #endregion
}
