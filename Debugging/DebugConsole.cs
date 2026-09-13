using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace NanoUint.Debugging;

/// <summary>Development-time live debug console that allocates a Windows console for log output.</summary>
public static class DebugConsole
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    private static bool _initialized;
    private static readonly object _lock = new();

    /// <summary>Shows the debug console window.</summary>
    public static void Show()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;
            AllocConsole();
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "FallenAltair Debug Console";
            Console.WriteLine("=== FallenAltair Debug Console ===");
            Console.WriteLine($"Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
            _initialized = true;
        }
    }

    /// <summary>Hides the debug console.</summary>
    public static void Hide()
    {
        if (!_initialized) return;
        lock (_lock)
        {
            if (!_initialized) return;
            FreeConsole();
            _initialized = false;
        }
    }

    /// <summary>Writes a timestamped log line.</summary>
    public static void Log(string tag, string message)
    {
        if (!_initialized) return;
        lock (_lock)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{tag}] {message}");
        }
    }

    /// <summary>Writes an unformatted line.</summary>
    public static void WriteLine(string message)
    {
        if (!_initialized) Console.WriteLine(message);
    }

    /// <summary>Writes exception details to the console.</summary>
    public static void LogError(string tag, Exception ex)
    {
        if (!_initialized) return;
        lock (_lock)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{tag}] ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
        }
    }
}
