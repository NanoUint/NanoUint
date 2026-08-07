using System.Diagnostics;
using System.Windows;
using NanoUint.Diagnostics;
using NanoUint.Debugging;
using WpfApplication = System.Windows.Application;

namespace NanoUint.Scripting;

/// <summary>内置脚本命令。提供 Win32 / 系统操作，扫描 NanoUint 程序集时自动注册。</summary>
public static class BuiltInCommands
{
    #region 系统 / Win32

    /// <summary>@sys_exit() —— 关闭应用程序</summary>
    [RegistryInScript("sys_exit")]
    public static void SysExit(ScriptCommandContext ctx)
    {
        var code = ctx.Arg<double>(0, 0);
        WpfApplication.Current.Dispatcher.Invoke(() =>
            WpfApplication.Current.Shutdown((int)code));
    }

    /// <summary>@sys_message：显示 Windows 消息框，暂停脚本直到用户关闭。</summary>
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

    /// <summary>@sys_confirm：显示是/否对话框。流程绑定：-> yes:#label no:#label。</summary>
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

    /// <summary>@sys_open_url("https://...") —— 在默认浏览器中打开 URL</summary>
    [RegistryInScript("sys_open_url")]
    public static void SysOpenUrl(ScriptCommandContext ctx)
    {
        var url = ctx.Arg<string>(0) ?? "";
        if (string.IsNullOrEmpty(url)) return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DebugConsole.LogError("sys_open_url", ex);
        }
    }

    /// <summary>@sys_run：运行外部程序。</summary>
    [RegistryInScript("sys_run")]
    public static void SysRun(ScriptCommandContext ctx)
    {
        var program = ctx.Arg<string>(0) ?? "";
        var arguments = ctx.Get<string>("args") ?? "";

        if (string.IsNullOrEmpty(program)) return;

        try
        {
            if (string.IsNullOrEmpty(arguments))
                Process.Start(program);
            else
                Process.Start(program, arguments);
        }
        catch (Exception ex)
        {
            DebugConsole.LogError("sys_run", ex);
        }
    }

    /// <summary>@sys_window_title("新标题") —— 更改主窗口标题</summary>
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

    /// <summary>@sys_clipboard("文本") —— 将文本复制到剪贴板</summary>
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

    #region 窗口状态

    /// <summary>@sys_minimize() —— 最小化主窗口</summary>
    [RegistryInScript("sys_minimize")]
    public static void SysMinimize(ScriptCommandContext ctx)
    {
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            if (WpfApplication.Current.MainWindow != null)
                WpfApplication.Current.MainWindow.WindowState = WindowState.Minimized;
        });
    }

    /// <summary>@sys_maximize() —— 最大化主窗口</summary>
    [RegistryInScript("sys_maximize")]
    public static void SysMaximize(ScriptCommandContext ctx)
    {
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            if (WpfApplication.Current.MainWindow != null)
                WpfApplication.Current.MainWindow.WindowState = WindowState.Maximized;
        });
    }

    /// <summary>@sys_restore() —— 恢复主窗口到正常大小</summary>
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

    #region 调试 / 工具

    /// <summary>@log("消息") —— 写入调试控制台</summary>
    [RegistryInScript("log")]
    public static void Log(ScriptCommandContext ctx)
    {
        var message = ctx.Arg<string>(0) ?? "";
        DebugConsole.Log("Script", message);
    }

    /// <summary>@wait：暂停脚本一段时间。</summary>
    [RegistryInScript("wait")]
    public static void Wait(ScriptCommandContext ctx)
    {
        var seconds = ctx.Arg<double>(0, 0.5);
        if (seconds <= 0) return;

        // 使用 Dispatcher 定时器实现非阻塞延迟
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
