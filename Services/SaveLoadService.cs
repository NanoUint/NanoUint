using System.IO;
using NanoUint.Models;
using Newtonsoft.Json;

namespace NanoUint.Services;

/// <summary>
/// 管理游戏状态的保存和加载（通过 JSON 文件）。
/// 支持多个带元数据的存档槽位。
/// </summary>
public static class SaveLoadService
{
    private static readonly string SaveDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Liminal", "Saves");

    private const int MaxSlots = 20;
    private const string SaveFilePattern = "save_{0:D2}.json";

    static SaveLoadService()
    {
        Directory.CreateDirectory(SaveDirectory);
    }

    /// <summary>获取指定存档槽位的文件路径</summary>
    public static string GetSavePath(int slotIndex)
    {
        return Path.Combine(SaveDirectory, string.Format(SaveFilePattern, slotIndex));
    }

    /// <summary>检查存档槽位是否有数据</summary>
    public static bool SlotExists(int slotIndex)
    {
        return File.Exists(GetSavePath(slotIndex));
    }

    /// <summary>将游戏状态保存到槽位</summary>
    public static void Save(int slotIndex, SaveData data)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Slot must be 0-{MaxSlots - 1}");

        data.SlotIndex = slotIndex;
        data.SaveTime = DateTime.Now;

        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(GetSavePath(slotIndex), json);
    }

    /// <summary>从槽位加载游戏状态</summary>
    public static SaveData? Load(int slotIndex)
    {
        var path = GetSavePath(slotIndex);
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<SaveData>(json);
    }

    /// <summary>删除存档槽位</summary>
    public static void Delete(int slotIndex)
    {
        var path = GetSavePath(slotIndex);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>获取所有存档槽位的元数据</summary>
    public static List<SaveData?> GetAllSlots()
    {
        var slots = new List<SaveData?>();
        for (int i = 0; i < MaxSlots; i++)
        {
            slots.Add(Load(i));
        }
        return slots;
    }

    /// <summary>获取已使用的存档槽位数量</summary>
    public static int GetSlotCount()
    {
        int count = 0;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (SlotExists(i)) count++;
        }
        return count;
    }

    /// <summary>删除所有存档</summary>
    public static void DeleteAll()
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            Delete(i);
        }
    }
}
