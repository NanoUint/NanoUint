using System.Windows;
using System.Windows.Threading;

namespace NanoUint.Rendering;

/// <summary>ISystemServices backed by WPF MessageBox, Clipboard, and Window.</summary>
internal sealed class WpfSystemServices : ISystemServices
{
    private readonly Func<Window?> _getWindow;

    public WpfSystemServices(Func<Window?> getWindow)
    {
        _getWindow = getWindow;
    }

    public void ShowMessage(string text, string title)
    {
        var win = _getWindow();
        if (win != null)
            win.Dispatcher.Invoke(() =>
                MessageBox.Show(win, text, title, MessageBoxButton.OK, MessageBoxImage.Information));
        else
            MessageBox.Show(text, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public bool ShowConfirm(string text, string title)
    {
        var win = _getWindow();
        if (win != null)
        {
            bool result = false;
            win.Dispatcher.Invoke(() =>
            {
                var r = MessageBox.Show(win, text, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                result = r == MessageBoxResult.Yes;
            });
            return result;
        }
        return MessageBox.Show(text, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public void SetClipboard(string text)
    {
        try { Clipboard.SetText(text); }
        catch { }
    }

    public void SetWindowTitle(string title)
    {
        var win = _getWindow();
        if (win != null)
            win.Dispatcher.Invoke(() => win.Title = title);
    }

    public void MinimizeWindow()
    {
        var win = _getWindow();
        if (win != null)
            win.Dispatcher.Invoke(() => win.WindowState = WindowState.Minimized);
    }

    public void MaximizeWindow()
    {
        var win = _getWindow();
        if (win != null)
            win.Dispatcher.Invoke(() => win.WindowState = WindowState.Maximized);
    }

    public void RestoreWindow()
    {
        var win = _getWindow();
        if (win != null)
            win.Dispatcher.Invoke(() => win.WindowState = WindowState.Normal);
    }

    public void Shutdown(int code)
    {
        var win = _getWindow();
        if (win != null)
            win.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown(code));
        else
            System.Windows.Application.Current.Shutdown(code);
    }
}
