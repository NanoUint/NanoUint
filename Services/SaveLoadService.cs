using System.IO;
using NanoUint.Models;
using Newtonsoft.Json;

namespace NanoUint.Services;

/// <summary>
/// Manages saving and loading game state to/from JSON files.
/// Supports multiple save slots with metadata.
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

    /// <summary>Get the file path for a given save slot</summary>
    public static string GetSavePath(int slotIndex)
    {
        return Path.Combine(SaveDirectory, string.Format(SaveFilePattern, slotIndex));
    }

    /// <summary>Check if a save slot has data</summary>
    public static bool SlotExists(int slotIndex)
    {
        return File.Exists(GetSavePath(slotIndex));
    }

    /// <summary>Save game state to a slot</summary>
    public static void Save(int slotIndex, SaveData data)
    {
        if (slotIndex < 0 || slotIndex >= MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Slot must be 0-{MaxSlots - 1}");

        data.SlotIndex = slotIndex;
        data.SaveTime = DateTime.Now;

        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(GetSavePath(slotIndex), json);
    }

    /// <summary>Load game state from a slot</summary>
    public static SaveData? Load(int slotIndex)
    {
        var path = GetSavePath(slotIndex);
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<SaveData>(json);
    }

    /// <summary>Delete a save slot</summary>
    public static void Delete(int slotIndex)
    {
        var path = GetSavePath(slotIndex);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>Get metadata for all save slots</summary>
    public static List<SaveData?> GetAllSlots()
    {
        var slots = new List<SaveData?>();
        for (int i = 0; i < MaxSlots; i++)
        {
            slots.Add(Load(i));
        }
        return slots;
    }

    /// <summary>Get the number of filled save slots</summary>
    public static int GetSlotCount()
    {
        int count = 0;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (SlotExists(i)) count++;
        }
        return count;
    }

    /// <summary>Delete all saves</summary>
    public static void DeleteAll()
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            Delete(i);
        }
    }
}
