using System.IO;
using Newtonsoft.Json;

namespace NanoUint;

/// <summary>系统存档管理器。追踪跨周目进度：已解锁 CG/音乐/动画/TIPS/结局/章节。</summary>
public static class SystemSaveManager
{
    private static readonly string SavePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "NanoUint", "system_save.json");

    private static SystemSaveData _data = new();

    #region 访问器（返回只读视图，阻止外部代码绕过解锁方法直接修改内部集合）

    public static IReadOnlySet<string> UnlockedCGs => _data.UnlockedCGs;
    public static IReadOnlySet<string> UnlockedMusic => _data.UnlockedMusic;
    public static IReadOnlySet<string> UnlockedMovies => _data.UnlockedMovies;
    public static IReadOnlySet<string> ReadTips => _data.ReadTips;
    public static IReadOnlySet<string> ReachedEndings => _data.ReachedEndings;
    public static IReadOnlySet<string> ClearedChapters => _data.ClearedChapters;
    public static IReadOnlySet<string> CollectedTrueEndFlags => _data.CollectedTrueEndFlags;
    public static IReadOnlySet<string> UnlockedAchievements => _data.UnlockedAchievements;
    public static float TotalPlayTime
    {
        get => _data.TotalPlayTime;
        set => _data.TotalPlayTime = value;
    }

    /// <summary>是否已通关至少一个结局（控制 EXTRA 解锁）。</summary>
    public static bool HasAnyEnding => _data.ReachedEndings.Count > 0;

    /// <summary>是否已通关全部结局（控制 True End 解锁）。</summary>
    public static bool HasAllEndings(string[] allEndingIds) =>
        allEndingIds.All(id => _data.ReachedEndings.Contains(id));

    #endregion

    #region 解锁方法

    public static void UnlockCG(string cgId) => _data.UnlockedCGs.Add(cgId);
    public static void UnlockMusic(string musicId) => _data.UnlockedMusic.Add(musicId);
    public static void UnlockMovie(string movieId) => _data.UnlockedMovies.Add(movieId);
    public static void MarkTipRead(string tipId) => _data.ReadTips.Add(tipId);
    public static void ReachEnding(string endingId) => _data.ReachedEndings.Add(endingId);
    public static void ClearChapter(string chapterId) => _data.ClearedChapters.Add(chapterId);
    public static bool UnlockAchievement(string achievementId) => _data.UnlockedAchievements.Add(achievementId);
    public static bool CollectTrueEndFlag(string flagId) => _data.CollectedTrueEndFlags.Add(flagId);

    #endregion

    #region 持久化

    public static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SavePath);
            if (dir != null) Directory.CreateDirectory(dir);

            var tmp = SavePath + ".tmp";
            var json = JsonConvert.SerializeObject(_data, Formatting.Indented);
            File.WriteAllText(tmp, json);

            var bak = SavePath + ".bak";
            if (File.Exists(bak)) File.Delete(bak);
            if (File.Exists(SavePath)) File.Move(SavePath, bak);
            File.Move(tmp, SavePath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"SystemSave: Failed to save: {ex.Message}");
        }
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                var json = File.ReadAllText(SavePath);
                _data = JsonConvert.DeserializeObject<SystemSaveData>(json) ?? new SystemSaveData();
                Debug.Log($"SystemSave: Loaded. Endings={_data.ReachedEndings.Count}, " +
                    $"CGs={_data.UnlockedCGs.Count}, Tips={_data.ReadTips.Count}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SystemSave: Load failed: {ex.Message}. Using defaults.");
            _data = new SystemSaveData();
        }
    }

    public static void Reset()
    {
        _data = new SystemSaveData();
    }
    #endregion
}

/// <summary>系统存档数据 DTO。</summary>
public sealed class SystemSaveData
{
    public HashSet<string> UnlockedCGs { get; set; } = new();
    public HashSet<string> UnlockedMusic { get; set; } = new();
    public HashSet<string> UnlockedMovies { get; set; } = new();
    public HashSet<string> ReadTips { get; set; } = new();
    public HashSet<string> ReachedEndings { get; set; } = new();
    public HashSet<string> ClearedChapters { get; set; } = new();
    public HashSet<string> CollectedTrueEndFlags { get; set; } = new();
    public HashSet<string> UnlockedAchievements { get; set; } = new();
    public float TotalPlayTime { get; set; }
}
