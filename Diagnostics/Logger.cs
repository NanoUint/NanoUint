using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;

namespace NanoUint.Diagnostics;

/// <summary>Log level.</summary>
public enum LogLevel
{
    Trace = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
}

/// <summary>A single log entry.</summary>
public readonly struct LogEntry
{
    public DateTime Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public string Tag { get; init; }
    public string Message { get; init; }
    public string? CallerPath { get; init; }
    public int CallerLine { get; init; }

    public override string ToString()
        => $"{Timestamp:HH:mm:ss.fff} [{Level.ToString().ToUpperInvariant()}] [{Tag}] {Message}";
}

/// <summary>Writes log entries with levels and tags.</summary>
public static class Logger
{
    private const int RingBufferSize = 1024;
    private static readonly ConcurrentQueue<LogEntry> _ringBuffer = new();
    private static int _bufferCount;
    private static StreamWriter? _fileWriter;
    private static string? _logDir;
    private static readonly object _fileLock = new();
    private static bool _initialized;

    #region Events

    /// <summary>Raised on every log write (including Trace).</summary>
    public static event Action<LogEntry>? OnEntryWritten;

    /// <summary>Raised on non-Trace log writes.</summary>
    public static event Action<LogEntry>? OnInfoWritten;

    /// <summary>Raised on Warning/Error log writes.</summary>
    public static event Action<LogEntry>? OnWarningWritten;

    /// <summary>Raised on Error log writes.</summary>
    public static event Action<LogEntry>? OnErrorWritten;

    #endregion

    #region Properties

    /// <summary>Current minimum output level.</summary>
    public static LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

    /// <summary>Whether to write to a file.</summary>
    public static bool FileLoggingEnabled { get; set; } = true;

    /// <summary>Most recent N log entries (snapshot).</summary>
    public static LogEntry[] RecentEntries => _ringBuffer.ToArray();

    /// <summary>Log file directory.</summary>
    public static string? LogDirectory => _logDir;

    #endregion

    #region Initialization

    /// <summary>Initializes the logging system and opens file output.</summary>
    public static void Initialize(string? logDir = null)
    {
        if (_initialized) return;

        _logDir = logDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "NanoUint", "logs");

        try
        {
            Directory.CreateDirectory(_logDir);

            var logFile = Path.Combine(_logDir,
                $"nano_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
            _fileWriter = new StreamWriter(logFile, append: false)
            {
                AutoFlush = true,
            };
            _initialized = true;

            FileLog($"=== NanoUint Logger initialized at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ===");
            FileLog($"=== OS: {Environment.OSVersion}  Runtime: {Environment.Version} ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Logger] Failed to initialize file logging: {ex.Message}");
            FileLoggingEnabled = false;
        }
    }

    /// <summary>Closes the log file.</summary>
    public static void Shutdown()
    {
        lock (_fileLock)
        {
            FileLog("=== Logger shutdown ===");
            _fileWriter?.Flush();
            _fileWriter?.Dispose();
            _fileWriter = null;
            _initialized = false;
        }
    }

    #endregion

    #region Public API

    public static void Trace(string tag, string message,
        [CallerFilePath] string? callerPath = null,
        [CallerLineNumber] int callerLine = 0)
    {
        Write(LogLevel.Trace, tag, message, callerPath, callerLine);
    }

    public static void Info(string tag, string message,
        [CallerFilePath] string? callerPath = null,
        [CallerLineNumber] int callerLine = 0)
    {
        Write(LogLevel.Info, tag, message, callerPath, callerLine);
    }

    public static void Warning(string tag, string message,
        [CallerFilePath] string? callerPath = null,
        [CallerLineNumber] int callerLine = 0)
    {
        Write(LogLevel.Warning, tag, message, callerPath, callerLine);
    }

    public static void Error(string tag, string message,
        [CallerFilePath] string? callerPath = null,
        [CallerLineNumber] int callerLine = 0)
    {
        Write(LogLevel.Error, tag, message, callerPath, callerLine);
    }

    /// <summary>Error level that also records an exception.</summary>
    public static void Error(string tag, string message, Exception ex,
        [CallerFilePath] string? callerPath = null,
        [CallerLineNumber] int callerLine = 0)
    {
        Write(LogLevel.Error, tag, $"{message}\n  Exception: {ex.GetType().Name}: {ex.Message}\n  Stack: {ex.StackTrace}",
            callerPath, callerLine);
    }

    #endregion

    #region Internal Write

    private static void Write(LogLevel level, string tag, string message,
        string? callerPath, int callerLine)
    {
        if (level < MinimumLevel) return;

        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Tag = tag,
            Message = message,
            CallerPath = callerPath,
            CallerLine = callerLine,
        };

        _ringBuffer.Enqueue(entry);
        var count = Interlocked.Increment(ref _bufferCount);
        while (count > RingBufferSize)
        {
            if (_ringBuffer.TryDequeue(out _))
                Interlocked.Decrement(ref _bufferCount);
            else break;
        }

        if (FileLoggingEnabled)
            FileLog(entry.ToString());

        System.Diagnostics.Debug.WriteLine(entry.ToString());

        try
        {
            OnEntryWritten?.Invoke(entry);
            if (level >= LogLevel.Info) OnInfoWritten?.Invoke(entry);
            if (level >= LogLevel.Warning) OnWarningWritten?.Invoke(entry);
            if (level >= LogLevel.Error) OnErrorWritten?.Invoke(entry);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Logger] Event handler threw: {ex}"); }
    }

    private static void FileLog(string line)
    {
        lock (_fileLock)
        {
            try { _fileWriter?.WriteLine(line); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Logger] File write failed: {ex.Message}"); }
        }
    }

    #endregion

    #region Utilities

    /// <summary>Gets recent log entries at or above the given level.</summary>
    public static LogEntry[] GetRecentEntries(LogLevel minLevel = LogLevel.Trace)
    {
        return _ringBuffer.Where(e => e.Level >= minLevel).ToArray();
    }

    /// <summary>Gets log statistics.</summary>
    public static (int total, int errors, int warnings) GetStats()
    {
        var all = _ringBuffer.ToArray();
        return (
            all.Length,
            all.Count(e => e.Level == LogLevel.Error),
            all.Count(e => e.Level == LogLevel.Warning)
        );
    }
    #endregion
}
