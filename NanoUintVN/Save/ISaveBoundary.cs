namespace NanoUintVN.Save;

/// <summary>Validates save/load boundaries (allow-save flags, slot limits, etc.).</summary>
public interface ISaveBoundary
{
    bool CanSave(string slotId);
    bool CanLoad(string slotId);
    int MaxSlots { get; }
    int QuickSaveSlot { get; }
}

/// <summary>Default boundary: 30 slots + quick-save, allow-save flag.</summary>
public sealed class DefaultSaveBoundary : ISaveBoundary
{
    private bool _allowSave = true;

    public bool AllowSave
    {
        get => _allowSave;
        set => _allowSave = value;
    }

    public int MaxSlots => 30;
    public int QuickSaveSlot => 99;

    public bool CanSave(string slotId)
    {
        if (!_allowSave) return false;
        if (!int.TryParse(slotId, out var idx)) return false;
        return idx >= 0 && idx < MaxSlots || idx == QuickSaveSlot;
    }

    public bool CanLoad(string slotId)
    {
        if (!int.TryParse(slotId, out var idx)) return false;
        return idx >= 0 && idx < MaxSlots || idx == QuickSaveSlot;
    }
}
