using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using NanoUint.Diagnostics;

namespace NanoUint;

/// <summary>Loads and caches images from embedded and filesystem resources.</summary>
public static class ResourceManager
{
    private static readonly Dictionary<string, (Assembly Assembly, string ResourceName)> _embeddedMap
        = new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, BitmapImage> _bitmapCache
        = new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, byte> _pendingLoads
        = new(StringComparer.OrdinalIgnoreCase);

    private static string? _resourcesRoot;

    private static bool _initialized;

    #region Initialization

    /// <summary>Scans embedded resources and detects the filesystem Resources directory.</summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        #region 1. Scan NanoUint embedded image resources (engine images such as Logo.jpg)
        var nanoAsm = Assembly.GetExecutingAssembly();
        ScanEmbeddedResources(nanoAsm);

        #endregion

        #region 2. Detect the filesystem Resources directory
        _resourcesRoot = FindResourcesRoot();
        if (_resourcesRoot != null)
            Logger.Info("Resource", $"Filesystem Resources root: '{_resourcesRoot}'");
        else
            Logger.Info("Resource", "No filesystem Resources directory found.");

        Logger.Info("Resource", $"ResourceManager initialized: {_embeddedMap.Count} embedded + " +
            $"filesystem resources");
        #endregion
    }

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

    private static string? FindResourcesRoot()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var resourcesDir = Path.Combine(baseDir, "Resources");
        if (Directory.Exists(resourcesDir))
            return resourcesDir;

        // Development environment: search upward for the project Resources directory.
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

    private static string DottedToSlashPath(string dottedPath)
    {
        var lastDot = dottedPath.LastIndexOf('.');
        if (lastDot < 0) return dottedPath;
        var withoutExt = dottedPath.Substring(0, lastDot);
        var ext = dottedPath.Substring(lastDot + 1);
        return withoutExt.Replace('.', '/') + "." + ext;
    }

    #endregion

    #region Query

    /// <summary>Checks whether an image resource exists for a logical path.</summary>
    public static bool Exists(string logicalPath)
    {
        if (!_initialized) Initialize();

        if (_embeddedMap.ContainsKey(logicalPath))
            return true;

        return ResolveFileSystemPath(logicalPath) is string fp && File.Exists(fp);
    }

    /// <summary>Lists all available image logical paths.</summary>
    public static IReadOnlyCollection<string> GetAllImagePaths()
    {
        if (!_initialized) Initialize();

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in _embeddedMap.Keys)
        {
            if (key.Contains('/') || !key.Contains('.'))
                paths.Add(key);
        }

        // Filesystem: scan every PNG/JPG under the Resources directory.
        if (_resourcesRoot != null && Directory.Exists(_resourcesRoot))
        {
            foreach (var file in Directory.EnumerateFiles(_resourcesRoot, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;

                // Convert to a logical path: {ResourcesRoot}\System\Phone\Phone.png → System/Phone/Phone.png
                var relative = file.Substring(_resourcesRoot.Length + 1);
                var logicalPath = relative.Replace('\\', '/');
                paths.Add(logicalPath);
            }
        }

        return paths.ToList().AsReadOnly();
    }

    #endregion

    #region Path Resolution

    private static string? ResolveFileSystemPath(string logicalPath)
    {
        if (_resourcesRoot == null) return null;
        var full = Path.GetFullPath(Path.Combine(_resourcesRoot, logicalPath));
        return File.Exists(full) ? full : null;
    }

    #endregion

    #region Synchronous Loading

    /// <summary>Loads and caches a BitmapImage synchronously.</summary>
    public static BitmapImage? GetBitmap(string logicalPath)
    {
        if (!_initialized) Initialize();

        if (_bitmapCache.TryGetValue(logicalPath, out var cached))
            return cached;

        if (TryLoadFromEmbedded(logicalPath) is { } embBmp)
        {
            _bitmapCache[logicalPath] = embBmp;
            return embBmp;
        }

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

    /// <summary>Warms up a single image synchronously.</summary>
    public static void WarmupSync(string logicalPath)
    {
        GetBitmap(logicalPath);
    }

    #endregion

    #region Background Preloading

    /// <summary>Preloads all image resources in parallel in the background.</summary>
    public static Task PreloadAllAsync(IProgress<int>? progress = null)
    {
        if (!_initialized) Initialize();

        // Fetch all filesystem images; O(n) scan, so it runs once at Splash only.
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
                    if (TryLoadFromEmbedded(path) is { } embBmp)
                    {
                        _bitmapCache[path] = embBmp;
                    }
                    else
                    {
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

    /// <summary>Loads a single image asynchronously in the background.</summary>
    public static Task<BitmapImage?> GetBitmapAsync(string logicalPath)
    {
        if (_bitmapCache.TryGetValue(logicalPath, out var cached))
            return Task.FromResult<BitmapImage?>(cached);

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

    /// <summary>Gets the absolute filesystem path of a resource.</summary>
    public static string? GetFullPath(string logicalPath)
    {
        if (!_initialized) Initialize();
        return ResolveFileSystemPath(logicalPath);
    }

    #endregion

    #region Decoding

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

    #region Helpers

    private static string NormalizePath(string path)
    {
        if (path.Contains('/')) return path;
        var slashForm = DottedToSlashPath(path);
        return slashForm != path ? slashForm : path.Replace('\\', '/');
    }

    /// <summary>Clears all caches.</summary>
    public static void ClearCache()
    {
        _bitmapCache.Clear();
        _pendingLoads.Clear();
        Logger.Info("Resource", "Bitmap cache cleared");
    }

    /// <summary>Gets cache statistics.</summary>
    public static (int Cached, int EmbeddedCount) GetStats()
        => (_bitmapCache.Count, _embeddedMap.Count);
    #endregion
}
