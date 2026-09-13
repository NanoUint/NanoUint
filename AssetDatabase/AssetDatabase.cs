using System.IO;
using System.Threading.Tasks;
using NanoUint.Diagnostics;

namespace NanoUint;

/// <summary>Loads sprites, audio and script assets by logical path.</summary>
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

    /// <summary>Registers the assembly.</summary>
    public static void RegisterAssembly(System.Reflection.Assembly _)
    {
        // Filesystem search is used in practice, so the assembly argument is ignored.
    }

    /// <summary>Adds a search root directory.</summary>
    public static void AddSearchDirectory(string dir)
    {
        if (!_searchDirs.Contains(dir))
        {
            _searchDirs.Add(dir);
            Debug.Log($"AssetDatabase: added search dir '{dir}'");
        }
    }

    /// <summary>Auto-detects and registers the Resources directory.</summary>
    public static void AutoDetect()
    {
        if (_initialized) return;
        _initialized = true;

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var resourcesDir = Path.Combine(baseDir, "Resources");

        if (Directory.Exists(resourcesDir))
        {
            _searchDirs.Add(resourcesDir);
            Debug.Log($"AssetDatabase: found Resources at '{resourcesDir}'");
        }

        // Search upward for the project directory (development environment).
        TryAddProjectResources(baseDir);
    }

    private static void TryAddProjectResources(string baseDir)
    {
        try
        {
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

    /// <summary>Clears the asset cache.</summary>
    public static void ClearCache()
    {
        _cache.Clear();
        ResourceManager.ClearCache();
    }

    /// <summary>Loads an asset synchronously; returns null when not found.</summary>
    public static T? Load<T>(string path) where T : Asset => Load<T>(path, 0f);

    /// <summary>Loads an asset synchronously with an optional pixelsPerUnit (Sprite only; values ≤0 use the default).</summary>
    public static T? Load<T>(string path, float pixelsPerUnit) where T : Asset
    {
        if (!_initialized) AutoDetect();

        var cacheKey = typeof(T) == typeof(Sprite) && pixelsPerUnit > 0
            ? $"{path}|ppu={pixelsPerUnit}" : path;

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

    /// <summary>Checks whether an asset exists.</summary>
    public static bool Exists(string path)
    {
        if (!_initialized) AutoDetect();
        return ResolvePath(path) is string fp && File.Exists(fp);
    }

    /// <summary>Opens a stream over the asset file.</summary>
    public static Stream? OpenStream(string path)
    {
        if (!_initialized) AutoDetect();
        var fullPath = ResolvePath(path);
        if (fullPath != null && File.Exists(fullPath))
            return File.OpenRead(fullPath);
        return null;
    }

    /// <summary>Creates a Sprite from Base64-encoded image data.</summary>
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

    /// <summary>Gets the full file path of an asset.</summary>
    public static string? GetFullPath(string path)
    {
        if (!_initialized) AutoDetect();
        return ResolvePath(path);
    }

    #region Path Resolution

    /// <summary>Resolves an asset path to a real filesystem path.</summary>
    public static string? ResolvePath(string path)
    {
        if (Path.IsPathRooted(path) && File.Exists(path))
            return path;

        var fileName = Path.GetFileName(path);
        if (fileName == path && path.Contains('.'))
        {
            var parts = path.Split('.');
            if (parts.Length >= 2)
                fileName = $"{parts[^2]}.{parts[^1]}";
        }

        foreach (var baseDir in _searchDirs)
        {
            // 1. Exact match (full concatenated path).
            var exact = Path.Combine(baseDir, path);
            if (File.Exists(exact))
            {
                var resolved = Path.GetFullPath(exact);
                if (IsPathSafe(resolved)) return resolved;
            }

            // 2. File name directly under the root.
            var rootCandidate = Path.Combine(baseDir, fileName);
            if (File.Exists(rootCandidate))
            {
                var resolved = Path.GetFullPath(rootCandidate);
                if (IsPathSafe(resolved)) return resolved;
            }

            // 3. Search each subfolder.
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

    #region Asset Construction

    private static Sprite LoadSprite(string logicalPath, float pixelsPerUnit = 0f)
    {
        if (!ResourceManager.Exists(logicalPath))
            throw new FileNotFoundException($"Image resource not found: '{logicalPath}'");

        var name = Path.GetFileNameWithoutExtension(logicalPath);
        var ppu = pixelsPerUnit > 0 ? pixelsPerUnit : 100f;
        return new Sprite { Path = logicalPath, Name = name, ImageData = Array.Empty<byte>(), PixelsPerUnit = ppu };
    }

    private static AudioClip LoadAudio(string logicalPath)
    {
        var fullPath = ResolvePath(logicalPath);
        if (fullPath == null || !File.Exists(fullPath))
            throw new FileNotFoundException($"Audio asset not found: '{logicalPath}'");

        var name = Path.GetFileNameWithoutExtension(fullPath);
        return new AudioClip { Path = fullPath, Name = name, AudioData = Array.Empty<byte>() };
    }

    private static ScriptAsset LoadScript(string logicalPath)
    {
        var fullPath = ResolvePath(logicalPath);
        if (fullPath == null || !File.Exists(fullPath))
            throw new FileNotFoundException($"Script asset not found: '{logicalPath}'");

        var name = Path.GetFileNameWithoutExtension(fullPath);
        return new ScriptAsset { Path = fullPath, Name = name, Source = File.ReadAllText(fullPath) };
    }

    #endregion

    #region Preloading

    /// <summary>Preloads all embedded image resources in parallel in the background.</summary>
    public static Task PreloadBitmapsAsync(IEnumerable<string> paths, IProgress<int>? progress = null)
    {
        return ResourceManager.PreloadAllAsync(progress);
    }

    /// <summary>Warms up a single image synchronously.</summary>
    public static void PreloadImageSync(string logicalPath)
    {
        ResourceManager.WarmupSync(logicalPath);
    }
    #endregion
}
