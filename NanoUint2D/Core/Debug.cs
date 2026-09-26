using NanoUint.Diagnostics;

namespace NanoUint;

/// <summary>Engine debug logging.</summary>
public static class Debug
{
    public static event Action<string>? OnLog;
    public static event Action<string>? OnLogWarning;
    public static event Action<string>? OnLogError;

    /// <summary>Minimum log level; messages below it are suppressed.</summary>
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

    /// <summary>Logs an error along with exception details.</summary>
    public static void LogError(string message, Exception ex)
    {
        Logger.Error("NanoUint", message, ex);
        var msg = $"[NanoUint][ERROR] {DateTime.Now:HH:mm:ss.fff} {message}\n  {ex.GetType().Name}: {ex.Message}";
        OnLogError?.Invoke(msg);
    }

    /// <summary>Logs a message with a tag for per-module filtering.</summary>
    public static void Log(string tag, string message)
    {
        Logger.Info(tag, message);
        var msg = $"[{tag}] {DateTime.Now:HH:mm:ss.fff} {message}";
        OnLog?.Invoke(msg);
    }
}
