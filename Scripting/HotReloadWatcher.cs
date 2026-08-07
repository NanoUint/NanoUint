using System.IO;

namespace NanoUint;

/// <summary>热重载监视器。监听脚本目录的文件变动，自动重新加载 .vns 脚本。</summary>
public sealed class HotReloadWatcher : IDisposable
{
    private readonly string _watchDirectory;
    private FileSystemWatcher? _watcher;
    private readonly Dictionary<string, DateTime> _lastReload = new();
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(300);

    public event Action<string>? OnScriptChanged;

    public HotReloadWatcher(string watchDirectory)
    {
        _watchDirectory = watchDirectory;
    }

    public void Start()
    {
        if (!Directory.Exists(_watchDirectory))
        {
            Debug.LogWarning($"HotReloadWatcher: Directory not found: {_watchDirectory}");
            return;
        }

        _watcher = new FileSystemWatcher(_watchDirectory, "*.vns")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;

        Debug.Log($"HotReloadWatcher: Watching '{_watchDirectory}' for .vns changes");
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        HandleChange(e.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        HandleChange(e.FullPath);
    }

    private void HandleChange(string path)
    {
        // 防抖：忽略短时间内重复事件
        if (_lastReload.TryGetValue(path, out var last) &&
            DateTime.Now - last < _debounceInterval)
            return;

        _lastReload[path] = DateTime.Now;
        Debug.Log($"HotReloadWatcher: Detected change in '{Path.GetFileName(path)}'");
        OnScriptChanged?.Invoke(path);
    }

    public void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    public void Dispose()
    {
        Stop();
    }
}
