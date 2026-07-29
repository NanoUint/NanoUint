using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NanoUint.Diagnostics;

namespace NanoUint.Debugging;

/// <summary>
/// 日志查看器 Tab。从 Logger 订阅实时日志，按级别着色显示。
/// </summary>
internal sealed class LogTab
{
    private readonly List<LogEntry> _entries = new();
    private const int MaxEntries = 200;
    private TextBlock? _logText;
    private ScrollViewer? _scroller;
    private bool _autoScroll = true;

    public LogTab()
    {
        Logger.OnEntryWritten += OnLogEntry;
    }

    public UIElement Build()
    {
        _logText = new TextBlock
        {
            Foreground = Brushes.LightGray,
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(6),
        };

        _scroller = new ScrollViewer
        {
            Content = _logText,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        _scroller.ScrollChanged += (_, e) =>
        {
            // 用户手动滚动时取消自动滚动；滚到底部时恢复
            if (e.ExtentHeightChange == 0)
                _autoScroll = _scroller.VerticalOffset >= _scroller.ScrollableHeight - 5;
        };

        RefreshDisplay();
        return _scroller;
    }

    public void Refresh()
    {
        if (_logText != null)
            UpdateDisplay();
    }

    private void OnLogEntry(LogEntry entry)
    {
        _entries.Add(entry);
        while (_entries.Count > MaxEntries)
            _entries.RemoveAt(0);

        if (_logText != null)
        {
            _logText.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                () => UpdateDisplay());
        }
    }

    private void UpdateDisplay()
    {
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (_logText == null || _scroller == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== Log ({_entries.Count} entries) ===");

        var recent = _entries.Skip(Math.Max(0, _entries.Count - 50)).ToArray();
        foreach (var entry in recent)
        {
            var levelStr = entry.Level switch
            {
                LogLevel.Error => "ERR",
                LogLevel.Warning => "WRN",
                LogLevel.Info => "INF",
                _ => "TRC"
            };
            sb.AppendLine($"{entry.Timestamp:HH:mm:ss.fff} [{levelStr}] [{entry.Tag}] {entry.Message}");
        }

        _logText.Text = sb.ToString();

        if (_autoScroll)
            _scroller.ScrollToEnd();
    }
}
