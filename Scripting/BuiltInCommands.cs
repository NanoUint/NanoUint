using System.Windows;
using NanoUint.Diagnostics;
using NanoUint.Debugging;
using WpfApplication = System.Windows.Application;

namespace NanoUint.Scripting;

/// <summary>Built-in script commands for Win32 and system operations.</summary>
public static class BuiltInCommands
{
    #region System / Win32

    /// <summary>@sys_exit() — closes the application</summary>
    [RegistryInScript("sys_exit")]
    public static void SysExit(ScriptCommandContext ctx)
    {
        var code = ctx.Arg<double>(0, 0);
        WpfApplication.Current.Dispatcher.Invoke(() =>
            WpfApplication.Current.Shutdown((int)code));
    }

    /// <summary>@sys_message: shows a Windows message box and pauses the script until the user closes it.</summary>
    [RegistryInScript("sys_message")]
    public static void SysMessage(ScriptCommandContext ctx)
    {
        var text = ctx.Arg<string>(0) ?? "";
        var title = ctx.Get<string>("title") ?? "FallenAltair";

        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            System.Windows.MessageBox.Show(text, title, MessageBoxButton.OK, MessageBoxImage.Information);
        });

        ctx.Engine.RequestPause();
    }

    /// <summary>@sys_confirm: shows a Yes/No dialog. Flow bindings: -> yes:#label no:#label.</summary>
    [RegistryInScript("sys_confirm")]
    public static void SysConfirm(ScriptCommandContext ctx)
    {
        var text = ctx.Arg<string>(0) ?? "";
        var title = ctx.Get<string>("title") ?? "FallenAltair";

        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            var result = System.Windows.MessageBox.Show(text, title,
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var yesTarget = ctx.FlowBindings?.NamedTargets?.GetValueOrDefault("yes")
                    ?? ctx.FlowBindings?.DefaultTarget;
                if (!string.IsNullOrEmpty(yesTarget))
                    ctx.Engine.JumpToLabel(yesTarget);
            }
            else
            {
                var noTarget = ctx.FlowBindings?.NamedTargets?.GetValueOrDefault("no");
                if (!string.IsNullOrEmpty(noTarget))
                    ctx.Engine.JumpToLabel(noTarget);
            }
        });

        ctx.Engine.RequestPause();
    }

    // @sys_open_url / @sys_run were removed: passing script-controlled arguments to
    // Process.Start allowed arbitrary command execution (RCE). Do not reintroduce them.

    /// <summary>@sys_window_title("new title") — changes the main window title</summary>
    [RegistryInScript("sys_window_title")]
    public static void SysWindowTitle(ScriptCommandContext ctx)
    {
        var title = ctx.Arg<string>(0) ?? "";
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            if (WpfApplication.Current.MainWindow != null)
                WpfApplication.Current.MainWindow.Title = title;
        });
    }

    /// <summary>@sys_clipboard("text") — copies text to the clipboard</summary>
    [RegistryInScript("sys_clipboard")]
    public static void SysClipboard(ScriptCommandContext ctx)
    {
        var text = ctx.Arg<string>(0) ?? "";
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            try { Clipboard.SetText(text); }
            catch (Exception ex) { Logger.Warning("BuiltIn", $"[sys_clipboard] Clipboard locked: {ex.Message}"); }
        });
    }

    #endregion

    #region Window state

    /// <summary>@sys_minimize() — minimizes the main window</summary>
    [RegistryInScript("sys_minimize")]
    public static void SysMinimize(ScriptCommandContext ctx)
    {
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            if (WpfApplication.Current.MainWindow != null)
                WpfApplication.Current.MainWindow.WindowState = WindowState.Minimized;
        });
    }

    /// <summary>@sys_maximize() — maximizes the main window</summary>
    [RegistryInScript("sys_maximize")]
    public static void SysMaximize(ScriptCommandContext ctx)
    {
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            if (WpfApplication.Current.MainWindow != null)
                WpfApplication.Current.MainWindow.WindowState = WindowState.Maximized;
        });
    }

    /// <summary>@sys_restore() — restores the main window to normal size</summary>
    [RegistryInScript("sys_restore")]
    public static void SysRestore(ScriptCommandContext ctx)
    {
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            if (WpfApplication.Current.MainWindow != null)
                WpfApplication.Current.MainWindow.WindowState = WindowState.Normal;
        });
    }

    #endregion

    #region Debug / utilities

    /// <summary>@log("message") — writes to the debug console</summary>
    [RegistryInScript("log")]
    public static void Log(ScriptCommandContext ctx)
    {
        var message = ctx.Arg<string>(0) ?? "";
        DebugConsole.Log("Script", message);
    }

    /// <summary>@wait: pauses the script for a period of time.</summary>
    [RegistryInScript("wait")]
    public static void Wait(ScriptCommandContext ctx)
    {
        var seconds = ctx.Arg<double>(0, 0.5);
        if (seconds <= 0) return;

        // Dispatcher timer provides a non-blocking delay
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(seconds)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ctx.Engine.Continue();
        };
        timer.Start();
        ctx.Engine.RequestPause();
    }

    #endregion
}
