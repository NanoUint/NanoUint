using System.IO;
using System.Threading.Tasks;
using NanoUint.Diagnostics;

namespace NanoUint;

/// <summary>资源数据库。图片走 ResourceManager（内嵌程序集），音频/脚本走文件系统。</summary>
public static class AssetDatabase
{
    private static readonly List<string> _searchDirs = new();
    private static bool _initialized;

    private static readonly Dictionary<string, Asset> _cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] SearchFolders =
    {
        "", "Backgrounds", "Characters", "Audio", "Scripts", "Sprites",
        "BGM", "SFX", "Voice", "Movies"
    };

    /// <summary>注册搜索目录。通常在 IGameBootstrapper.OnStart 中调用。</summary>
    public static void RegisterAssembly(System.Reflection.Assembly _)
    {
        // 实际使用文件系统搜索
    }

    /// <summary>添加搜索根目录。</summary>
    public static void AddSearchDirectory(string dir)
    {
        if (!_searchDirs.Contains(dir))
        {
            _searchDirs.Add(dir);
            Debug.Log($"AssetDatabase: added search dir '{dir}'");
        }
    }

    /// <summary>自动探测并注册 Resources 目录。</summary>
    public static void AutoDetect()
    {
        if (_initialized) return;
        _initialized = true;

        // 搜索可执行文件附近的 Resources 目录
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var resourcesDir = Path.Combine(baseDir, "Resources");

        if (Directory.Exists(resourcesDir))
        {
            _searchDirs.Add(resourcesDir);
            Debug.Log($"AssetDatabase: found Resources at '{resourcesDir}'");
        }

        // 向上搜索（开发环境：项目目录）
        TryAddProjectResources(baseDir);
    }

    private static void TryAddProjectResources(string baseDir)
    {
        try
        {
            // 向上搜索最多 5 层，找 Resources 目录
            var dir = baseDir;
            for (int i = 0; i < 5; i++)
            {
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
                var resourcesDir = Path.Combine(dir, "Resources");
                if (Directory.Exists(resourcesDir) && !_searchDirs.Contains(resourcesDir))
                {
                    _searchDirs.Add(resourcesDir);
                    Debug.Log($"AssetDatabase: found project Resources at '{resourcesDir}'");
                    break;
                }
            }
        }
        catch (Exception ex) { Logger.Trace("AssetDatabase", $"Project dir scan skipped: {ex.Message}"); }
    }

    /// <summary>清空资源缓存。</summary>
    public static void ClearCache()
    {
        _cache.Clear();
        ResourceManager.ClearCache();
    }

    /// <summary>同步加载资源。找不到返回 null。</summary>
    public static T? Load<T>(string path) where T : Asset => Load<T>(path, 0f);

    /// <summary>同步加载资源，可指定 pixelsPerUnit（仅对 Sprite 有效，≤0 则用默认值 1920）。</summary>
    public static T? Load<T>(string path, float pixelsPerUnit) where T : Asset
    {
        if (!_initialized) AutoDetect();

        var cacheKey = typeof(T) == typeof(Sprite) && pixelsPerUnit > 0
            ? $"{path}|ppu={pixelsPerUnit}" : path;

        // 查缓存
        if (_cache.TryGetValue(cacheKey, out var cached) && cached is T t)
            return t;

        try
        {
            Asset asset;
            if (typeof(T) == typeof(Sprite))
                asset = LoadSprite(path, pixelsPerUnit);
            else if (typeof(T) == typeof(AudioClip))
                asset = LoadAudio(path);
            else if (typeof(T) == typeof(ScriptAsset))
                asset = LoadScript(path);
            else
                throw new NotSupportedException($"Asset type '{typeof(T).Name}' not supported.");

            _cache[cacheKey] = asset;
            return (T)asset;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"AssetDatabase.Load failed for '{path}': {ex.Message}");
            return null;
        }
    }

    /// <summary>检查资源是否存在。</summary>
    public static bool Exists(string path)
    {
        if (!_initialized) AutoDetect();
        return ResolvePath(path) is string fp && File.Exists(fp);
    }

    /// <summary>打开资源文件流。</summary>
    public static Stream? OpenStream(string path)
    {
        if (!_initialized) AutoDetect();
        var fullPath = ResolvePath(path);
        if (fullPath != null && File.Exists(fullPath))
            return File.OpenRead(fullPath);
        return null;
    }

    /// <summary>从 Base64 编码的图片数据创建 Sprite（用于存档缩略图等）。</summary>
    public static Sprite? CreateSpriteFromBase64(string base64, string syntheticKey)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            var sprite = new Sprite
            {
                Path = $"__thumb__/{syntheticKey}",
                Name = syntheticKey,
                ImageData = bytes,
                Width = 320,
                Height = 180,
            };
            return sprite;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"AssetDatabase: Failed to create sprite from base64 ({syntheticKey}): {ex.Message}");
            return null;
        }
    }

    /// <summary>获取资源的完整文件路径。</summary>
    public static string? GetFullPath(string path)
    {
        if (!_initialized) AutoDetect();
        return ResolvePath(path);
    }

    #region 路径解析

    /// <summary>解析资源路径到实际文件系统路径（精确路径 → 根目录 → 各子文件夹）。</summary>
    public static string? ResolvePath(string path)
    {
        // 绝对路径或已存在的文件：直接返回
        if (Path.IsPathRooted(path) && File.Exists(path))
            return path;

        // 提取纯文件名
        var fileName = Path.GetFileName(path);
        if (fileName == path && path.Contains('.'))
        {
            var parts = path.Split('.');
            if (parts.Length >= 2)
                fileName = $"{parts[^2]}.{parts[^1]}";
        }

        foreach (var baseDir in _searchDirs)
        {
            // 1. 精确匹配（完整路径拼接）
            var exact = Path.Combine(baseDir, path);
            if (File.Exists(exact))
            {
                var resolved = Path.GetFullPath(exact);
                if (IsPathSafe(resolved)) return resolved;
            }

            // 2. 根目录文件名
            var rootCandidate = Path.Combine(baseDir, fileName);
            if (File.Exists(rootCandidate))
            {
                var resolved = Path.GetFullPath(rootCandidate);
                if (IsPathSafe(resolved)) return resolved;
            }

            // 3. 各子文件夹中查找
            foreach (var folder in SearchFolders)
            {
                if (string.IsNullOrEmpty(folder)) continue;
                var candidate = Path.Combine(baseDir, folder, fileName);
                if (File.Exists(candidate))
                {
                    var resolved = Path.GetFullPath(candidate);
                    if (IsPathSafe(resolved)) return resolved;
                }
            }
        }

        return null;
    }

    /// <summary>验证解析后的路径仍在允许的搜索根目录内。</summary>
    private static bool IsPathSafe(string resolvedPath)
    {
        foreach (var baseDir in _searchDirs)
        {
            var normalizedBase = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedPath = resolvedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (normalizedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        Debug.LogWarning($"AssetDatabase: Path traversal blocked — '{resolvedPath}' is outside allowed directories.");
        return false;
    }

    #endregion

    #region 资源构造

    /// <summary>从内嵌资源创建 Sprite。Path 存储逻辑路径（如 "Backgrounds/BG01A.png"）。</summary>
    private static Sprite LoadSprite(string logicalPath, float pixelsPerUnit = 0f)
    {
        if (!ResourceManager.Exists(logicalPath))
            throw new FileNotFoundException($"Image resource not found: '{logicalPath}'");

        var name = Path.GetFileNameWithoutExtension(logicalPath);
        var ppu = pixelsPerUnit > 0 ? pixelsPerUnit : 100f;
        return new Sprite { Path = logicalPath, Name = name, ImageData = Array.Empty<byte>(), PixelsPerUnit = ppu };
    }

    /// <summary>从文件系统加载音频。保持文件路径以便 NAudio 流式播放。</summary>
    private static AudioClip LoadAudio(string logicalPath)
    {
        var fullPath = ResolvePath(logicalPath);
        if (fullPath == null || !File.Exists(fullPath))
            throw new FileNotFoundException($"Audio asset not found: '{logicalPath}'");

        var name = Path.GetFileNameWithoutExtension(fullPath);
        return new AudioClip { Path = fullPath, Name = name, AudioData = Array.Empty<byte>() };
    }

    /// <summary>从文件系统加载脚本文本。</summary>
    private static ScriptAsset LoadScript(string logicalPath)
    {
        var fullPath = ResolvePath(logicalPath);
        if (fullPath == null || !File.Exists(fullPath))
            throw new FileNotFoundException($"Script asset not found: '{logicalPath}'");

        var name = Path.GetFileNameWithoutExtension(fullPath);
        return new ScriptAsset { Path = fullPath, Name = name, Source = File.ReadAllText(fullPath) };
    }

    #endregion

    #region 预加载

    /// <summary>后台并行预加载所有内嵌图片资源。在 Splash 阶段调用。</summary>
    public static Task PreloadBitmapsAsync(IEnumerable<string> paths, IProgress<int>? progress = null)
    {
        return ResourceManager.PreloadAllAsync(progress);
    }

    /// <summary>同步预热单张图片。</summary>
    public static void PreloadImageSync(string logicalPath)
    {
        ResourceManager.WarmupSync(logicalPath);
    }
    #endregion
}
