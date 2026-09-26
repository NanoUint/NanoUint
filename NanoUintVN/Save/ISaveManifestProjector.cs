namespace NanoUintVN.Save;

/// <summary>Projects full save state into a lightweight manifest for display.</summary>
public interface ISaveManifestProjector
{
    SaveSlotManifest Project(VNSaveState state);
}

/// <summary>Default implementation: maps fields directly.</summary>
public sealed class DefaultManifestProjector : ISaveManifestProjector
{
    public SaveSlotManifest Project(VNSaveState state) => new()
    {
        SlotId = state.SlotId,
        DocumentId = state.DocumentId,
        PageIndex = state.PageIndex,
        BeatIndex = state.BeatIndex,
        PageId = state.PageId,
        SaveTime = state.SaveTime,
        Description = state.Description,
        ThumbnailBase64 = state.ThumbnailBase64,
        PlayTime = state.PlayTime
    };
}
