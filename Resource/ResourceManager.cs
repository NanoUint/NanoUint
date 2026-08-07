using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using NanoUint.Diagnostics;

namespace NanoUint;

/// <summary>统一资源管理器。所有图片加载的唯一入口。</summary>
public static class ResourceManager
{
    /// <summary>逻辑路径（如 "System/Phone/Phone.png"）→ (Assembly, 内嵌资源名)。仅存内嵌资源映射。</summary>
    private static readonly Dictionary<string, (Assembly Assembly, string ResourceName)> _embeddedMap
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>BitmapImage 统一缓存（跨场景复用，ConcurrentDictionary 支持后台预加载）。</summary>
    private static readonly ConcurrentDictionary<string, BitmapImage> _bitmapCache
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>正在后台加载的 key，防重复提交。</summary>
    private static readonly ConcurrentDictionary<string, byte> _pendingLoads
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>文件系统 Resources 根目录。</summary>
    private static string? _resourcesRoot;

    private static bool _initialized;

    #region 初始化

    /// <summary>扫描内嵌资源 + 探测文件系统 Resources 目录。</summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        #region 1. 扫描 NanoUint 内嵌图片资源（Logo.jpg 等引擎图片）
        var nanoAsm = Assembly.GetExecutingAssembly();
        ScanEmbeddedResources(nanoAsm);

        #endregion

        #region 2. 探测文件系统 Resources 目录
        _resourcesRoot = FindResourcesRoot();
        if (_resourcesRoot != null)
            Logger.Info("Resource", $"Filesystem Resources root: '{_resourcesRoot}'");
        else
            Logger.Info("Resource", "No filesystem Resources directory found.");

        Logger.Info("Resource", $"ResourceManager initialized: {_embeddedMap.Count} embedded + " +
            $"filesystem resources");
        #endregion
    }

    /// <summary>扫描程序集中的内嵌 .png/.jpg 资源。</summary>
    private static void ScanEmbeddedResources(Assembly asm)
    {
        var asmName = asm.GetName().Name;
        if (asmName == null) return;

        var prefix = asmName + ".Resources.";

        foreach (var resName in asm.GetManifestResourceNames())
        {
            if (!resName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var ext = Path.GetExtension(resName).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                continue;

            // "NanoUint.Resources.Logo.jpg" → "Logo.jpg"
            var afterPrefix = resName.Substring(prefix.Length);
            var slashPath = DottedToSlashPath(afterPrefix);

            _embeddedMap[slashPath] = (asm, resName);
            if (slashPath != afterPrefix)
                _embeddedMap[afterPrefix] = (asm, resName);

            Logger.Trace("Resource", $"Embedded: '{slashPath}' → {asmName}");
        }
    }

    /// <summary>探测文件系统 Resources 目录。</summary>
    private static string? FindResourcesRoot()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var resourcesDir = Path.Combine(baseDir, "Resources");
        if (Directory.Exists(resourcesDir))
            return resourcesDir;

        // 开发环境：向上搜索项目 Resources 目录
        try
        {
            var dir = baseDir;
            for (int i = 0; i < 5; i++)
            {
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
                var candidate = Path.Combine(dir, "Resources");
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }
        catch (Exception ex) { Logger.Trace("Resource", $"Project resources root scan skipped: {ex.Message}"); }

        return null;
    }

    /// <summary>将点分隔的内嵌资源路径转为斜杠分隔的逻辑路径。</summary>
    private static string DottedToSlashPath(string dottedPath)
    {
        var lastDot = dottedPath.LastIndexOf('.');
        if (lastDot < 0) return dottedPath;
        var withoutExt = dottedPath.Substring(0, lastDot);
        var ext = dottedPath.Substring(lastDot + 1);
        return withoutExt.Replace('.', '/') + "." + ext;
    }

    #endregion

    #region 查询

    /// <summary>检查逻辑路径对应的图片资源是否存在（内嵌 + 文件系统）。</summary>
    public static bool Exists(string logicalPath)
    {
        if (!_initialized) Initialize();

        // 1. 内嵌资源
        if (_embeddedMap.ContainsKey(logicalPath))
            return true;

        // 2. 文件系统
        return ResolveFileSystemPath(logicalPath) is string fp && File.Exists(fp);
    }

    /// <summary>列出所有可用的图片逻辑路径（内嵌 + 文件系统）。</summary>
    public static IReadOnlyCollection<string> GetAllImagePaths()
    {
        if (!_initialized) Initialize();

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 内嵌资源
        foreach (var key in _embeddedMap.Keys)
        {
            if (key.Contains('/') || !key.Contains('.'))
                paths.Add(key);
        }

        // 文件系统：扫描 Resources 目录下所有 PNG/JPG
        if (_resourcesRoot != null && Directory.Exists(_resourcesRoot))
        {
            foreach (var file in Directory.EnumerateFiles(_resourcesRoot, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;

                // 转为逻辑路径：{ResourcesRoot}\System\Phone\Phone.png → System/Phone/Phone.png
                var relative = file.Substring(_resourcesRoot.Length + 1); // +1 for separator
                var logicalPath = relative.Replace('\\', '/');
                paths.Add(logicalPath);
            }
        }

        return paths.ToList().AsReadOnly();
    }

    #endregion

    #region 路径解析

    /// <summary>将逻辑路径解析为文件系统绝对路径。返回 null 表示该路径没有对应的文件系统资源。</summary>
    private static string? ResolveFileSystemPath(string logicalPath)
    {
        if (_resourcesRoot == null) return null;
        var full = Path.GetFullPath(Path.Combine(_resourcesRoot, logicalPath));
        return File.Exists(full) ? full : null;
    }

    #endregion

    #region 同步加载

    /// <summary>同步加载并缓存 BitmapImage。查找顺序：缓存 → 内嵌资源 → 文件系统。</summary>
    public static BitmapImage? GetBitmap(string logicalPath)
    {
        if (!_initialized) Initialize();

        // 1. 缓存命中
        if (_bitmapCache.TryGetValue(logicalPath, out var cached))
            return cached;

        // 2. 内嵌资源
        if (TryLoadFromEmbedded(logicalPath) is { } embBmp)
        {
            _bitmapCache[logicalPath] = embBmp;
            return embBmp;
        }

        // 3. 文件系统
        var fsPath = ResolveFileSystemPath(logicalPath);
        if (fsPath != null)
        {
            var bmp = DecodeFromFile(fsPath);
            if (bmp != null)
            {
                _bitmapCache[logicalPath] = bmp;
                return bmp;
            }
        }

        Logger.Warning("Resource", $"Image not found: '{logicalPath}'");
        return null;
    }

    /// <summary>尝试从内嵌资源加载并解码。</summary>
    private static BitmapImage? TryLoadFromEmbedded(string logicalPath)
    {
        if (!_embeddedMap.TryGetValue(logicalPath, out var entry))
        {
            var alt = NormalizePath(logicalPath);
            if (!_embeddedMap.TryGetValue(alt, out entry))
                return null;
        }

        try
        {
            using var stream = entry.Assembly.GetManifestResourceStream(entry.ResourceName);
            return stream != null ? DecodeBitmap(stream) : null;
        }
        catch (Exception ex)
        {
            Logger.Warning("Resource", $"Embedded load failed for '{logicalPath}': {ex.Message}");
            return null;
        }
    }

    /// <summary>同步预热单个图片。确保图片在协程开始前已就绪。</summary>
    public static void WarmupSync(string logicalPath)
    {
        GetBitmap(logicalPath);
    }

    #endregion

    #region 后台预加载

    /// <summary>后台并行预加载所有图片资源（内嵌 + 文件系统）。</summary>
    public static Task PreloadAllAsync(IProgress<int>? progress = null)
    {
        if (!_initialized) Initialize();

        // 获取所有文件系统图片（网络搜索开销 O(n)，仅在 Splash 调用一次）
        var allPaths = GetAllImagePaths().Where(p => !_bitmapCache.ContainsKey(p)).ToList();
        if (allPaths.Count == 0)
        {
            progress?.Report(100);
            return Task.CompletedTask;
        }

        int loaded = 0;
        return Task.Run(() =>
        {
            Parallel.ForEach(allPaths, path =>
            {
                try
                {
                    if (_bitmapCache.ContainsKey(path)) return;
                    // 内嵌资源
                    if (TryLoadFromEmbedded(path) is { } embBmp)
                    {
                        _bitmapCache[path] = embBmp;
                    }
                    else
                    {
                        // 文件系统
                        var fsPath = ResolveFileSystemPath(path);
                        if (fsPath != null)
                        {
                            var bmp = DecodeFromFile(fsPath);
                            if (bmp != null)
                                _bitmapCache[path] = bmp;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Trace("Resource", $"Skipped corrupted resource during preload: {ex.Message}");
                }

                int done = Interlocked.Increment(ref loaded);
                progress?.Report(done * 100 / allPaths.Count);
            });
        });
    }

    /// <summary>后台异步加载单张图片。缓存命中则立即返回，否则启动后台解码。</summary>
    public static Task<BitmapImage?> GetBitmapAsync(string logicalPath)
    {
        if (_bitmapCache.TryGetValue(logicalPath, out var cached))
            return Task.FromResult<BitmapImage?>(cached);

        // 防重复提交
        if (!_pendingLoads.TryAdd(logicalPath, 0))
            return Task.FromResult<BitmapImage?>(null);

        var capturedPath = logicalPath;
        return Task.Run(() =>
        {
            try
            {
                return GetBitmap(capturedPath);
            }
            catch (Exception ex)
            {
                Logger.Trace("Resource", $"Async bitmap load failed for '{capturedPath}': {ex.Message}");
                return null;
            }
            finally
            {
                _pendingLoads.TryRemove(capturedPath, out _);
            }
        });
    }

    /// <summary>获取资源的文件系统绝对路径。仅文件系统资源有效；内嵌资源返回 null。</summary>
    public static string? GetFullPath(string logicalPath)
    {
        if (!_initialized) Initialize();
        return ResolveFileSystemPath(logicalPath);
    }

    #endregion

    #region 解码

    /// <summary>从 Stream 解码 BitmapImage 并 Freeze（跨线程安全）。</summary>
    private static BitmapImage? DecodeBitmap(Stream stream)
    {
        try
        {
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            return DecodeFromBytes(ms);
        }
        catch (Exception ex)
        {
            Logger.Warning("Resource", $"Bitmap decode failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>从文件系统路径同步解码。</summary>
    private static BitmapImage? DecodeFromFile(string fullPath)
    {
        try
        {
            var rawBytes = File.ReadAllBytes(fullPath);
            using var ms = new MemoryStream(rawBytes);
            return DecodeFromBytes(ms);
        }
        catch (Exception ex)
        {
            Logger.Warning("Resource", $"File decode failed for '{fullPath}': {ex.Message}");
            return null;
        }
    }

    /// <summary>从 MemoryStream 解码 BitmapImage。</summary>
    private static BitmapImage? DecodeFromBytes(MemoryStream ms)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.StreamSource = ms;
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    #endregion

    #region 辅助

    private static string NormalizePath(string path)
    {
        if (path.Contains('/')) return path;
        var slashForm = DottedToSlashPath(path);
        return slashForm != path ? slashForm : path.Replace('\\', '/');
    }

    /// <summary>清空所有缓存（调试用）。</summary>
    public static void ClearCache()
    {
        _bitmapCache.Clear();
        _pendingLoads.Clear();
        Logger.Info("Resource", "Bitmap cache cleared");
    }

    /// <summary>获取缓存统计信息。</summary>
    public static (int Cached, int EmbeddedCount) GetStats()
        => (_bitmapCache.Count, _embeddedMap.Count);
    #endregion
}
