using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;

namespace NanoUint.Diagnostics;

/// <summary>
/// 日志级别。
/// </summary>
public enum LogLevel
{
    Trace = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
}

/// <summary>
/// 单条日志记录。
/// </summary>
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

/// <summary>
/// 引擎级日志系统。支持分级、标签、文件写入、内存环形缓冲。
/// 所有引擎和游戏层都应使用此 Logger 记录关键事件。
/// </summary>
public static class Logger
{
    private const int RingBufferSize = 1024;
    private static readonly ConcurrentQueue<LogEntry> _ringBuffer = new();
    private static int _bufferCount;
    private static StreamWriter? _fileWriter;
    private static string? _logDir;
    private static readonly object _fileLock = new();
    private static bool _initialized;

    // ── 事件 ──

    /// <summary>每当有日志写入时触发（含 Trace 级别）。可用于游戏内控制台。</summary>
    public static event Action<LogEntry>? OnEntryWritten;

    /// <summary>非 Trace 日志写入时触发。</summary>
    public static event Action<LogEntry>? OnInfoWritten;

    /// <summary>Warning/Error 级别日志写入时触发。</summary>
    public static event Action<LogEntry>? OnWarningWritten;

    /// <summary>Error 级别日志写入时触发。</summary>
    public static event Action<LogEntry>? OnErrorWritten;

    // ── 属性 ──

    /// <summary>当前最低输出级别。</summary>
    public static LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

    /// <summary>是否写入文件。</summary>
    public static bool FileLoggingEnabled { get; set; } = true;

    /// <summary>最近 N 条日志（快照）。</summary>
    public static LogEntry[] RecentEntries => _ringBuffer.ToArray();

    /// <summary>日志文件目录。</summary>
    public static string? LogDirectory => _logDir;

    // ── 初始化 ──

    /// <summary>初始化日志系统，打开文件写入。</summary>
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

    /// <summary>关闭日志文件。</summary>
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

    // ── 公开 API ──

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

    /// <summary>Error 级别，同时记录异常。</summary>
    public static void Error(string tag, string message, Exception ex,
        [CallerFilePath] string? callerPath = null,
        [CallerLineNumber] int callerLine = 0)
    {
        Write(LogLevel.Error, tag, $"{message}\n  Exception: {ex.GetType().Name}: {ex.Message}\n  Stack: {ex.StackTrace}",
            callerPath, callerLine);
    }

    // ── 内部写入 ──

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

        // 环形缓冲
        _ringBuffer.Enqueue(entry);
        var count = Interlocked.Increment(ref _bufferCount);
        while (count > RingBufferSize)
        {
            if (_ringBuffer.TryDequeue(out _))
                Interlocked.Decrement(ref _bufferCount);
            else break;
        }

        // 文件写入
        if (FileLoggingEnabled)
            FileLog(entry.ToString());

        // Debug 输出
        System.Diagnostics.Debug.WriteLine(entry.ToString());

        // 事件
        try
        {
            OnEntryWritten?.Invoke(entry);
            if (level >= LogLevel.Info) OnInfoWritten?.Invoke(entry);
            if (level >= LogLevel.Warning) OnWarningWritten?.Invoke(entry);
            if (level >= LogLevel.Error) OnErrorWritten?.Invoke(entry);
        }
        catch { /* 事件处理器的异常不应影响日志写入 */ }
    }

    private static void FileLog(string line)
    {
        lock (_fileLock)
        {
            try { _fileWriter?.WriteLine(line); }
            catch { /* 文件写入失败不影响引擎 */ }
        }
    }

    // ── 工具 ──

    /// <summary>获取指定级别以上的最近日志。</summary>
    public static LogEntry[] GetRecentEntries(LogLevel minLevel = LogLevel.Trace)
    {
        return _ringBuffer.Where(e => e.Level >= minLevel).ToArray();
    }

    /// <summary>获取日志统计。</summary>
    public static (int total, int errors, int warnings) GetStats()
    {
        var all = _ringBuffer.ToArray();
        return (
            all.Length,
            all.Count(e => e.Level == LogLevel.Error),
            all.Count(e => e.Level == LogLevel.Warning)
        );
    }
}
