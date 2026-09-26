namespace NanoUintVN.Save;

/// <summary>Complete VN save state with anchor-based positioning.</summary>
public sealed class VNSaveState
{
    public string SlotId { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public int PageIndex { get; set; }
    public int BeatIndex { get; set; }
    public string? PageId { get; set; }
    public DateTime SaveTime { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
    public string? ThumbnailBase64 { get; set; }
    public TimeSpan PlayTime { get; set; }
    public int SchemaVersion { get; set; } = 1;

    public Dictionary<string, string?> DomainStates { get; set; } = new();
    public Dictionary<string, bool> Flags { get; set; } = new();
    public Dictionary<string, string> Variables { get; set; } = new();
    public List<string> ChoiceHistory { get; set; } = new();
}
