using System.IO;
using Newtonsoft.Json;

namespace NanoUint;

/// <summary>
/// 存档管理器。引擎内置，20 槽 JSON 持久化，原子文件写入。
/// 借鉴 VoidNovelEngine-dev 的 save_manager：Schema 版本化 + 保存边界检查。
/// </summary>
public static class SaveManager
{
    public const int MaxSlots = 30;
    public const int QuickSaveSlot = 99; // 特殊槽位
    private static readonly string SaveDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "NanoUint", "Saves");

    private static bool _allowSave = true; // 由保存边界检查控制

    /// <summary>当前是否允许存档（保存边界检查）。</summary>
    public static bool AllowSave
    {
        get => _allowSave;
        internal set => _allowSave = value;
    }

    // ── 快速存档 ──

    public static void QuickSave(SaveData data)
    {
        Save(QuickSaveSlot, data);
    }

    public static SaveData? QuickLoad()
    {
        return Load(QuickSaveSlot);
    }

    public static bool QuickSaveExists => SlotExists(QuickSaveSlot);

    public static SaveSlotInfo? GetQuickSaveInfo()
    {
        var path = GetSlotPath(QuickSaveSlot);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            var data = JsonConvert.DeserializeObject<SaveData>(json);
            return new SaveSlotInfo
            {
                SlotIndex = QuickSaveSlot,
                SaveTime = data?.SaveTime ?? DateTime.MinValue,
                ChapterName = data?.ChapterName ?? "Unknown",
                PlayTime = data?.PlayTime ?? TimeSpan.Zero,
            };
        }
        catch { return null; }
    }

    // ── 存档操作 ──

    public static void Save(int slotIndex, SaveData data)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));

        if (!_allowSave)
        {
            Debug.LogWarning("SaveManager: Save not allowed at this point (save boundary check).");
            return;
        }

        try
        {
            Directory.CreateDirectory(SaveDirectory);
            data.SlotIndex = slotIndex;
            data.SaveTime = DateTime.Now;
            data.SchemaVersion = 1;

            var path = GetSlotPath(slotIndex);
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);

            // 原子写入
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            var bak = path + ".bak";
            if (File.Exists(bak)) File.Delete(bak);
            if (File.Exists(path)) File.Move(path, bak);
            File.Move(tmp, path);

            Debug.Log($"SaveManager: Saved slot {slotIndex} ({data.ChapterName})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"SaveManager: Failed to save slot {slotIndex}: {ex.Message}");
        }
    }

    public static SaveData? Load(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));

        try
        {
            var path = GetSlotPath(slotIndex);
            if (!File.Exists(path)) return null;

            var json = File.ReadAllText(path);
            var data = JsonConvert.DeserializeObject<SaveData>(json);
            Debug.Log($"SaveManager: Loaded slot {slotIndex} ({data?.ChapterName})");
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogError($"SaveManager: Failed to load slot {slotIndex}: {ex.Message}");
            return null;
        }
    }

    public static void Delete(int slotIndex)
    {
        try
        {
            var path = GetSlotPath(slotIndex);
            if (File.Exists(path)) File.Delete(path);
            var bak = path + ".bak";
            if (File.Exists(bak)) File.Delete(bak);
            Debug.Log($"SaveManager: Deleted slot {slotIndex}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"SaveManager: Failed to delete slot {slotIndex}: {ex.Message}");
        }
    }

    public static bool SlotExists(int slotIndex)
    {
        return File.Exists(GetSlotPath(slotIndex));
    }

    public static List<SaveSlotInfo> GetAllSlots()
    {
        var slots = new List<SaveSlotInfo>();
        for (int i = 0; i < MaxSlots; i++)
        {
            var path = GetSlotPath(i);
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var data = JsonConvert.DeserializeObject<SaveData>(json);
                    slots.Add(new SaveSlotInfo
                    {
                        SlotIndex = i,
                        SaveTime = data?.SaveTime ?? DateTime.MinValue,
                        ChapterName = data?.ChapterName ?? "Unknown",
                        PlayTime = data?.PlayTime ?? TimeSpan.Zero,
                    });
                }
                catch { /* 跳过损坏的存档 */ }
            }
        }
        return slots.OrderByDescending(s => s.SaveTime).ToList();
    }

    public static int GetSlotCount()
    {
        int count = 0;
        for (int i = 0; i < MaxSlots; i++)
            if (File.Exists(GetSlotPath(i))) count++;
        return count;
    }

    private static string GetSlotPath(int slotIndex)
        => Path.Combine(SaveDirectory, $"save_{slotIndex:D2}.json");
}

/// <summary>
/// 存档数据 DTO。
/// </summary>
public class SaveData
{
    public int SchemaVersion { get; set; } = 1;
    public int SlotIndex { get; set; }
    public DateTime SaveTime { get; set; }
    public string ChapterName { get; set; } = "";
    public int DialogueIndex { get; set; }
    public string? CurrentBackground { get; set; }
    public string? CurrentBGM { get; set; }
    public TimeSpan PlayTime { get; set; }
    public Dictionary<string, bool> Flags { get; set; } = new();
    public List<string> ChoiceHistory { get; set; } = new();
    public string? ScriptStateJson { get; set; }
    public string? SceneStateJson { get; set; }
    public string? ThumbnailBase64 { get; set; }

    public string GetDescription()
        => $"{SaveTime:yyyy/MM/dd HH:mm} - {ChapterName}";
}

/// <summary>
/// 存档槽摘要信息（用于列表显示）。
/// </summary>
public class SaveSlotInfo
{
    public int SlotIndex { get; set; }
    public DateTime SaveTime { get; set; }
    public string ChapterName { get; set; } = "";
    public TimeSpan PlayTime { get; set; }
    public string Description => $"{SaveTime:yyyy/MM/dd HH:mm} - {ChapterName}";
}
