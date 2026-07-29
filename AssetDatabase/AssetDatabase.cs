using System.IO;
using System.Threading.Tasks;

namespace NanoUint;

/// <summary>
/// 资源数据库。类似 Unity 的 Resources.Load。
/// 从文件系统加载资源（比嵌入资源快：WPF/NAudio 原生支持文件路径，OS 磁盘缓存）。
/// 资源文件通过 csproj 的 Content+CopyToOutputDirectory 部署到输出目录。
/// </summary>
public static class AssetDatabase
{
    private static readonly List<string> _searchDirs = new();
    private static bool _initialized;

    // 资源缓存：避免重复文件 I/O 和 PNG 解码
    private static readonly Dictionary<string, Asset> _cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] SearchFolders =
    {
        "", "Backgrounds", "Characters", "Audio", "Scripts", "Sprites",
        "BGM", "SFX", "Voice", "Movies"
    };

    /// <summary>注册搜索目录。通常在 IGameBootstrapper.OnStart 中调用。</summary>
    public static void RegisterAssembly(System.Reflection.Assembly _)
    {
        // 保持 API 兼容性，实际使用文件系统搜索
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
        catch { /* 权限问题等 */ }
    }

    /// <summary>清空资源缓存。</summary>
    public static void ClearCache()
    {
        _cache.Clear();
    }

    /// <summary>同步加载资源。找不到返回 null。</summary>
    public static T? Load<T>(string path) where T : Asset
    {
        if (!_initialized) AutoDetect();

        // 查缓存
        if (_cache.TryGetValue(path, out var cached) && cached is T t)
            return t;

        var fullPath = ResolvePath(path);
        if (fullPath == null || !File.Exists(fullPath)) return null;

        try
        {
            Asset asset;
            if (typeof(T) == typeof(Sprite))
                asset = LoadSprite(fullPath);
            else if (typeof(T) == typeof(AudioClip))
                asset = LoadAudio(fullPath);
            else if (typeof(T) == typeof(ScriptAsset))
                asset = LoadScript(fullPath);
            else
                throw new NotSupportedException($"Asset type '{typeof(T).Name}' not supported.");

            _cache[path] = asset;
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

    /// <summary>获取资源的完整文件路径。</summary>
    public static string? GetFullPath(string path)
    {
        if (!_initialized) AutoDetect();
        return ResolvePath(path);
    }

    // ── 路径解析 ──

    private static string? ResolvePath(string path)
    {
        // 绝对路径或已存在的文件：直接返回
        if (Path.IsPathRooted(path) && File.Exists(path))
            return path;

        // 提取纯文件名（处理 "Assembly.Resources.Folder.file.ext" 格式）
        var fileName = Path.GetFileName(path);
        // 如果 Path.GetFileName 返回整个路径（无目录分隔符的情况），尝试提取最后一段
        if (fileName == path && path.Contains('.'))
        {
            // 可能是 "SteinsGateX.Resources.MainMenu.png" 这种嵌入资源路径
            // 提取最后的 "name.ext" 部分
            var parts = path.Split('.');
            if (parts.Length >= 2)
                fileName = $"{parts[^2]}.{parts[^1]}";
        }

        foreach (var baseDir in _searchDirs)
        {
            // 1. 精确匹配（完整路径拼接）
            var exact = Path.Combine(baseDir, path);
            if (File.Exists(exact)) return Path.GetFullPath(exact);

            // 2. 搜索目录根（filename 直接放在 baseDir 下）
            var rootCandidate = Path.Combine(baseDir, fileName);
            if (File.Exists(rootCandidate))
                return Path.GetFullPath(rootCandidate);

            // 3. 短名搜索：在各子文件夹中查找
            foreach (var folder in SearchFolders)
            {
                if (string.IsNullOrEmpty(folder)) continue;
                var candidate = Path.Combine(baseDir, folder, fileName);
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    // ── 资源构造 ──

    private static Sprite LoadSprite(string fullPath)
    {
        var name = Path.GetFileNameWithoutExtension(fullPath);
        var sprite = new Sprite { Path = fullPath, Name = name };
        // 延迟加载：只在需要时读字节数组。WpfRenderer 优先用文件路径加载
        sprite.ImageData = Array.Empty<byte>();
        // 快速读取 PNG 尺寸（只读前 24 字节，不加载整张图）
        try
        {
            using var fs = File.OpenRead(fullPath);
            var header = new byte[24];
            if (fs.Read(header, 0, 24) == 24)
            {
                TryReadPngSize(header, out int w, out int h);
                sprite.Width = w;
                sprite.Height = h;
            }
        }
        catch { }
        return sprite;
    }

    private static AudioClip LoadAudio(string fullPath)
    {
        var name = Path.GetFileNameWithoutExtension(fullPath);
        // 音频不缓存到内存——NAudio 直接读文件
        return new AudioClip { Path = fullPath, Name = name,
            AudioData = Array.Empty<byte>() /* NAudio 用文件路径 */ };
    }

    private static ScriptAsset LoadScript(string fullPath)
    {
        var name = Path.GetFileNameWithoutExtension(fullPath);
        return new ScriptAsset { Path = fullPath, Name = name, Source = File.ReadAllText(fullPath) };
    }

    private static void TryReadPngSize(byte[] data, out int w, out int h)
    {
        w = 0; h = 0;
        try
        {
            if (data.Length >= 24 && data[0] == 0x89 && data[1] == 'P' && data[2] == 'N' && data[3] == 'G')
            {
                w = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
                h = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
            }
        }
        catch { }
    }

    // ── 预加载 ──

    /// <summary>
    /// 后台并行预加载所有图片的 BitmapImage。在 Splash/加载页面调用。
    /// 将 PNG 解码移到后台线程，完成后缓存到 WpfRenderer 的静态缓存中。
    /// 通过 IProgress 报告进度（0-100）。
    /// </summary>
    public static Task PreloadBitmapsAsync(IEnumerable<string> paths, IProgress<int>? progress = null)
    {
        return Rendering.WpfRenderer.WarmupBitmapsAsync(paths, progress);
    }
}
