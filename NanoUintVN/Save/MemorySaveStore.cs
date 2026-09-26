namespace NanoUintVN.Save;

/// <summary>In-memory save store for testing.</summary>
public sealed class MemorySaveStore : ISaveStore
{
    private readonly Dictionary<string, string> _data = new();

    public bool Save(string slotId, string json)
    {
        _data[slotId] = json;
        return true;
    }

    public string? Load(string slotId)
    {
        return _data.TryGetValue(slotId, out var json) ? json : null;
    }

    public bool Delete(string slotId) => _data.Remove(slotId);

    public bool Exists(string slotId) => _data.ContainsKey(slotId);

    public IReadOnlyList<SaveSlotManifest> GetAllManifests()
    {
        var manifests = new List<SaveSlotManifest>();
        foreach (var kv in _data)
        {
            try
            {
                var state = System.Text.Json.JsonSerializer.Deserialize<VNSaveState>(kv.Value);
                if (state != null)
                {
                    manifests.Add(new SaveSlotManifest
                    {
                        SlotId = state.SlotId,
                        DocumentId = state.DocumentId,
                        PageIndex = state.PageIndex,
                        BeatIndex = state.BeatIndex,
                        PageId = state.PageId,
                        SaveTime = state.SaveTime,
                        Description = state.Description,
                        PlayTime = state.PlayTime
                    });
                }
            }
            catch { }
        }
        return manifests.OrderBy(m => m.SlotId).ToList();
    }
}
