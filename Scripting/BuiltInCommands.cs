using System.Diagnostics;
using System.Windows;
using NanoUint.Services;

namespace NanoUint.Scripting;

/// <summary>
/// 内置脚本命令，提供 Win32 / 系统操作。
/// 扫描 NanoUint 程序集时自动注册。
/// 所有命令使用 [RegistryInScript] 特性进行声明式注册。
///
/// .vns 中的用法：
///   @sys_message("你好世界", title: "信息")
///   @sys_open_url("https://bing.com")
///   @sys_exit()
///   @sys_run("notepad.exe")
///   @sys_window_title("第二章")
///   @sys_clipboard("复制的文本")
///   @log("调试消息")
/// </summary>
public static class BuiltInCommands
{
    #region 系统 / Win32

    /// <summary>@sys_exit() —— 关闭应用程序</summary>
    [RegistryInScript("sys_exit")]
    public static void SysExit(ScriptCommandContext ctx)
    {
        var code = ctx.Arg<double>(0, 0);
        Application.Current.Dispatcher.Invoke(() =>
            Application.Current.Shutdown((int)code));
    }

    /// <summary>
    /// @sys_message(text, title: "Info") —— 显示 Windows 消息框
    /// 暂停脚本直到用户关闭对话框。
    /// </summary>
    [RegistryInScript("sys_message")]
    public static void SysMessage(ScriptCommandContext ctx)
    {
        var text = ctx.Arg<string>(0) ?? "";
        var title = ctx.Get<string>("title") ?? "FallenAltair";

        Application.Current.Dispatcher.Invoke(() =>
        {
            System.Windows.MessageBox.Show(text, title, MessageBoxButton.OK, MessageBoxImage.Information);
        });

        // 暂停脚本以防止对话框打开时脚本继续执行
        ctx.Engine.RequestPause();
    }

    /// <summary>
    /// @sys_confirm(text, title: "Confirm") —— 显示是/否对话框
    /// 流程绑定：-> yes:#label no:#label
    /// </summary>
    [RegistryInScript("sys_confirm")]
    public static void SysConfirm(ScriptCommandContext ctx)
    {
        var text = ctx.Arg<string>(0) ?? "";
        var title = ctx.Get<string>("title") ?? "FallenAltair";

        Application.Current.Dispatcher.Invoke(() =>
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

    /// <summary>
    /// @sys_run(program, args: "") —— 运行外部程序
    /// 示例：@sys_run("notepad.exe", args: "readme.txt")
    /// </summary>
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
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.Title = title;
        });
    }

    /// <summary>@sys_clipboard("文本") —— 将文本复制到剪贴板</summary>
    [RegistryInScript("sys_clipboard")]
    public static void SysClipboard(ScriptCommandContext ctx)
    {
        var text = ctx.Arg<string>(0) ?? "";
        // 剪贴板必须在 STA 线程中调用
        Application.Current.Dispatcher.Invoke(() =>
        {
            try { Clipboard.SetText(text); }
            catch { /* 剪贴板可能被锁定 */ }
        });
    }

    #endregion

    #region 窗口状态

    /// <summary>@sys_minimize() —— 最小化主窗口</summary>
    [RegistryInScript("sys_minimize")]
    public static void SysMinimize(ScriptCommandContext ctx)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.WindowState = WindowState.Minimized;
        });
    }

    /// <summary>@sys_maximize() —— 最大化主窗口</summary>
    [RegistryInScript("sys_maximize")]
    public static void SysMaximize(ScriptCommandContext ctx)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.WindowState = WindowState.Maximized;
        });
    }

    /// <summary>@sys_restore() —— 恢复主窗口到正常大小</summary>
    [RegistryInScript("sys_restore")]
    public static void SysRestore(ScriptCommandContext ctx)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (Application.Current.MainWindow != null)
                Application.Current.MainWindow.WindowState = WindowState.Normal;
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

    /// <summary>
    /// @wait(seconds) —— 暂停脚本一段时间。
    /// 使用异步延迟，保持 UI 响应。
    /// </summary>
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
