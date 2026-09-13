using NanoUint.Diagnostics;
using NanoUint.Debugging;

namespace NanoUint.Scripting;

/// <summary>Built-in script commands for Win32 and system operations.</summary>
public static class BuiltInCommands
{
    private static ISystemServices Sys => Application.Default!.System;
    private static IDispatcher Disp => Application.Default!.Dispatcher;

    #region System / Win32

    /// <summary>@sys_exit() — closes the application</summary>
    [RegistryInScript("sys_exit")]
    public static void SysExit(ScriptCommandContext ctx)
    {
        var code = ctx.Arg<double>(0, 0);
        Sys.Shutdown((int)code);
    }

    /// <summary>@sys_message: shows a Windows message box and pauses the script until the user closes it.</summary>
    [RegistryInScript("sys_message")]
    public static void SysMessage(ScriptCommandContext ctx)
    {
        var text = ctx.Arg<string>(0) ?? "";
        var title = ctx.Get<string>("title") ?? "FallenAltair";
        Sys.ShowMessage(text, title);
        ctx.Engine.RequestPause();
    }

    /// <summary>@sys_confirm: shows a Yes/No dialog. Flow bindings: -> yes:#label no:#label.</summary>
    [RegistryInScript("sys_confirm")]
    public static void SysConfirm(ScriptCommandContext ctx)
    {
        var text = ctx.Arg<string>(0) ?? "";
        var title = ctx.Get<string>("title") ?? "FallenAltair";

        var confirmed = Sys.ShowConfirm(text, title);
        if (confirmed)
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

        ctx.Engine.RequestPause();
    }

    // @sys_open_url / @sys_run were removed: passing script-controlled arguments to
    // Process.Start allowed arbitrary command execution (RCE). Do not reintroduce them.

    /// <summary>@sys_window_title("new title") — changes the main window title</summary>
    [RegistryInScript("sys_window_title")]
    public static void SysWindowTitle(ScriptCommandContext ctx)
    {
        var title = ctx.Arg<string>(0) ?? "";
        Sys.SetWindowTitle(title);
    }

    /// <summary>@sys_clipboard("text") — copies text to the clipboard</summary>
    [RegistryInScript("sys_clipboard")]
    public static void SysClipboard(ScriptCommandContext ctx)
    {
        var text = ctx.Arg<string>(0) ?? "";
        try { Sys.SetClipboard(text); }
        catch (Exception ex) { Logger.Warning("BuiltIn", $"[sys_clipboard] Clipboard locked: {ex.Message}"); }
    }

    #endregion

    #region Window state

    /// <summary>@sys_minimize() — minimizes the main window</summary>
    [RegistryInScript("sys_minimize")]
    public static void SysMinimize(ScriptCommandContext ctx)
    {
        Sys.MinimizeWindow();
    }

    /// <summary>@sys_maximize() — maximizes the main window</summary>
    [RegistryInScript("sys_maximize")]
    public static void SysMaximize(ScriptCommandContext ctx)
    {
        Sys.MaximizeWindow();
    }

    /// <summary>@sys_restore() — restores the main window to normal size</summary>
    [RegistryInScript("sys_restore")]
    public static void SysRestore(ScriptCommandContext ctx)
    {
        Sys.RestoreWindow();
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
        ctx.Engine.RequestPause();
        Disp.Wait(TimeSpan.FromSeconds(seconds), () => ctx.Engine.Continue());
    }

    #endregion
}
