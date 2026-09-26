namespace NanoUintVN.Save;

/// <summary>High-level save manager using ISaveStore + ISaveBoundary + ISaveManifestProjector.</summary>
public sealed class SaveManagerV2
{
    private readonly ISaveStore _store;
    private readonly ISaveBoundary _boundary;
    private readonly ISaveManifestProjector _projector;
    private readonly List<ISaveStateContributor> _contributors = new();

    public SaveManagerV2(ISaveStore store, ISaveBoundary boundary, ISaveManifestProjector projector)
    {
        _store = store;
        _boundary = boundary;
        _projector = projector;
    }

    public void RegisterContributor(ISaveStateContributor contributor) => _contributors.Add(contributor);
    public void UnregisterContributor(ISaveStateContributor contributor) => _contributors.Remove(contributor);

    public bool Save(string slotId, VNSaveState state)
    {
        if (!_boundary.CanSave(slotId)) return false;

        state.SlotId = slotId;
        state.SaveTime = DateTime.UtcNow;
        state.SchemaVersion = 1;

        foreach (var c in _contributors)
        {
            var captured = c.CaptureState();
            state.DomainStates[c.Domain] = System.Text.Json.JsonSerializer.Serialize(captured, captured.GetType());
        }

        var json = System.Text.Json.JsonSerializer.Serialize(state);
        return _store.Save(slotId, json);
    }

    public VNSaveState? Load(string slotId)
    {
        if (!_boundary.CanLoad(slotId)) return null;

        var json = _store.Load(slotId);
        if (json == null) return null;

        var state = System.Text.Json.JsonSerializer.Deserialize<VNSaveState>(json);
        if (state == null) return null;

        foreach (var c in _contributors)
        {
            if (state.DomainStates.TryGetValue(c.Domain, out var domainJson) && domainJson != null)
            {
                var obj = System.Text.Json.JsonSerializer.Deserialize(domainJson, c.CapturedType);
                if (obj != null)
                    c.RestoreState(obj);
            }
        }

        return state;
    }

    public bool QuickSave(VNSaveState state) => Save(_boundary.QuickSaveSlot.ToString(), state);
    public VNSaveState? QuickLoad() => Load(_boundary.QuickSaveSlot.ToString());
    public bool Delete(string slotId) => _store.Delete(slotId);
    public bool SlotExists(string slotId) => _store.Exists(slotId);

    public SaveSlotManifest? GetManifest(string slotId)
    {
        var json = _store.Load(slotId);
        if (json == null) return null;
        var state = System.Text.Json.JsonSerializer.Deserialize<VNSaveState>(json);
        return state != null ? _projector.Project(state) : null;
    }

    public IReadOnlyList<SaveSlotManifest> GetAllManifests() => _store.GetAllManifests();
}
