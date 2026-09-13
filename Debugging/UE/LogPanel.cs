using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NanoUint.Diagnostics;

namespace NanoUint.Debugging.UE;

internal sealed class LogPanel : UEPanel
{
    private const int MaxEntries = 500;

    private readonly StackPanel _rows;
    private readonly ScrollViewer _scroller;
    private int _entryCounter;
    private bool _logUnityDebug = true;

    public LogPanel(Canvas parentCanvas)
        : base(parentCanvas, "Log", "Log", UEPalette.LogScroll)
    {
        DefaultLeft = 0.5;
        DefaultTop = 0.03;
        DefaultWidth = 0.40;
        DefaultHeight = 0.17;
        MinPanelWidth = 350;
        MinPanelHeight = 75;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) });

        #region Log area (#080808)
        _rows = new StackPanel { Background = new SolidColorBrush(UEPalette.LogScroll) };
        _scroller = new ScrollViewer
        {
            Content = _rows,
            Background = new SolidColorBrush(UEPalette.LogScroll),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        UEFactory.ApplyScrollbarStyle(_scroller);
        Grid.SetRow(_scroller, 0);
        root.Children.Add(_scroller);

        #endregion

        #region OptionsRow: Clear / Open Log File / Log Unity Debug?
        var options = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Background = new SolidColorBrush(UEPalette.InspectorTopRow),
            Height = 25,
        };

        var clearBtn = UEFactory.Button("Clear", 60, 23, UEPalette.NormalButton, 12);
        clearBtn.Margin = new Thickness(4, 1, 4, 1);
        clearBtn.Click += (_, _) => _rows.Children.Clear();
        options.Children.Add(clearBtn);

        var openBtn = UEFactory.Button("Open Log File", 100, 23, UEPalette.NormalButton, 12);
        openBtn.Margin = new Thickness(0, 1, 4, 1);
        openBtn.Click += (_, _) => OpenLogFile();
        options.Children.Add(openBtn);

        var debugWrap = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 1),
        };
        var debugCheck = UEFactory.Check(_logUnityDebug, v => _logUnityDebug = v, UEPalette.BehaviourToggleGraphic);
        debugCheck.Margin = new Thickness(0, 0, 4, 0);
        debugWrap.Children.Add(debugCheck);
        debugWrap.Children.Add(UEFactory.Label("Log Unity Debug?", 12));
        options.Children.Add(debugWrap);

        Grid.SetRow(options, 1);
        root.Children.Add(options);

        ContentHost.Children.Add(root);

        Logger.OnEntryWritten += OnEntry;

        foreach (var entry in Logger.RecentEntries.TakeLast(200))
            AppendEntry(entry);
        #endregion
    }

    private void OnEntry(LogEntry entry)
    {
        _rows.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () => AppendEntry(entry));
    }

    private void AppendEntry(LogEntry entry)
    {
        _entryCounter++;

        var color = entry.Level switch
        {
            LogLevel.Error => UEPalette.LogError,
            LogLevel.Warning => UEPalette.LogWarning,
            _ => UEPalette.LogInfo,
        };

        var row = new Border { Background = Brushes.Transparent };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var indexLabel = UEFactory.Label(_entryCounter.ToString(), 12, UEPalette.LogIndex, font: UEPalette.ConsoleFont);
        indexLabel.Margin = new Thickness(4, 0, 4, 0);
        UEFactory.SetCell(indexLabel, grid, 0);

        var timeLabel = UEFactory.Label(entry.Timestamp.ToString("HH:mm:ss.fff"), 12, UEPalette.TextInactive, font: UEPalette.ConsoleFont);
        timeLabel.Margin = new Thickness(0, 0, 4, 0);
        UEFactory.SetCell(timeLabel, grid, 1);

        var msg = new TextBlock
        {
            Text = $"[{entry.Tag}] {entry.Message}",
            FontFamily = UEPalette.ConsoleFont,
            FontSize = 12,
            Foreground = new SolidColorBrush(color),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 1, 4, 1),
        };
        UEFactory.SetCell(msg, grid, 2);

        row.Child = grid;
        _rows.Children.Insert(0, row);
        while (_rows.Children.Count > MaxEntries)
            _rows.Children.RemoveAt(_rows.Children.Count - 1);

        _scroller.ScrollToTop();
    }

    private static void OpenLogFile()
    {
        try
        {
            var dir = Logger.LogDirectory;
            if (dir == null || !Directory.Exists(dir))
            {
                Logger.Warning("UE", "Log directory not found");
                return;
            }

            var latest = new DirectoryInfo(dir).GetFiles("*.log")
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();
            if (latest == null)
            {
                Logger.Warning("UE", "No log file found");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = latest.FullName,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Logger.Warning("UE", $"Cannot open log file: {ex.Message}");
        }
    }
}
