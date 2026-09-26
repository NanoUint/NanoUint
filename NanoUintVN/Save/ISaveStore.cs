namespace NanoUintVN.Save;

/// <summary>Storage backend for save data. Implementations: FileSaveStore, MemorySaveStore.</summary>
public interface ISaveStore
{
    bool Save(string slotId, string json);
    string? Load(string slotId);
    bool Delete(string slotId);
    bool Exists(string slotId);
    IReadOnlyList<SaveSlotManifest> GetAllManifests();
}

/// <summary>Manifest summary for a save slot (lightweight metadata).</summary>
public sealed class SaveSlotManifest
{
    public string SlotId { get; init; } = string.Empty;
    public string DocumentId { get; init; } = string.Empty;
    public int PageIndex { get; init; }
    public int BeatIndex { get; init; }
    public string? PageId { get; init; }
    public DateTime SaveTime { get; init; }
    public string? Description { get; init; }
    public string? ThumbnailBase64 { get; init; }
    public TimeSpan PlayTime { get; init; }
}
