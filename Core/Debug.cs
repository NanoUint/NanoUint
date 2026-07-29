using NanoUint.Diagnostics;

namespace NanoUint;

/// <summary>
/// 引擎调试日志。类似 UnityEngine.Debug。
/// 底层委托给 NanoUint.Diagnostics.Logger。
/// </summary>
public static class Debug
{
    public static event Action<string>? OnLog;
    public static event Action<string>? OnLogWarning;
    public static event Action<string>? OnLogError;

    /// <summary>最低日志级别。设为 LogLevel.Warning 可抑制 Info/Trace。</summary>
    public static LogLevel MinimumLevel
    {
        get => Logger.MinimumLevel;
        set => Logger.MinimumLevel = value;
    }

    public static void Log(string message)
    {
        Logger.Info("NanoUint", message);
        var msg = $"[NanoUint] {DateTime.Now:HH:mm:ss.fff} {message}";
        OnLog?.Invoke(msg);
    }

    public static void LogWarning(string message)
    {
        Logger.Warning("NanoUint", message);
        var msg = $"[NanoUint][WARN] {DateTime.Now:HH:mm:ss.fff} {message}";
        OnLogWarning?.Invoke(msg);
    }

    public static void LogError(string message)
    {
        Logger.Error("NanoUint", message);
        var msg = $"[NanoUint][ERROR] {DateTime.Now:HH:mm:ss.fff} {message}";
        OnLogError?.Invoke(msg);
    }

    /// <summary>记录错误并附带异常详情。</summary>
    public static void LogError(string message, Exception ex)
    {
        Logger.Error("NanoUint", message, ex);
        var msg = $"[NanoUint][ERROR] {DateTime.Now:HH:mm:ss.fff} {message}\n  {ex.GetType().Name}: {ex.Message}";
        OnLogError?.Invoke(msg);
    }

    /// <summary>带标签的日志——游戏层推荐使用，方便按模块过滤。</summary>
    public static void Log(string tag, string message)
    {
        Logger.Info(tag, message);
        var msg = $"[{tag}] {DateTime.Now:HH:mm:ss.fff} {message}";
        OnLog?.Invoke(msg);
    }
}
