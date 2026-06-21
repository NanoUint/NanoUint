namespace NanoUint.Models;

/// <summary>
/// Represents a single save slot containing full game state.
/// </summary>
public class SaveData
{
    /// <summary>Slot index (0-based)</summary>
    public int SlotIndex { get; set; }

    /// <summary>Timestamp when the save was created</summary>
    public DateTime SaveTime { get; set; } = DateTime.Now;

    /// <summary>Name of the current chapter/scene</summary>
    public string ChapterName { get; set; } = string.Empty;

    /// <summary>Chapter name in save (localized)</summary>
    public string ChapterNameZh { get; set; } = string.Empty;
    public string ChapterNameEn { get; set; } = string.Empty;
    public string ChapterNameJa { get; set; } = string.Empty;

    /// <summary>Index in the current dialogue sequence</summary>
    public int DialogueIndex { get; set; }

    /// <summary>Current scene/background path</summary>
    public string? CurrentBackground { get; set; }

    /// <summary>Current BGM path</summary>
    public string? CurrentBGM { get; set; }

    /// <summary>Game flags for branching logic</summary>
    public Dictionary<string, bool> Flags { get; set; } = new();

    /// <summary>Player choices made (for branching paths)</summary>
    public List<string> ChoiceHistory { get; set; } = new();

    /// <summary>Total play time at save point</summary>
    public TimeSpan PlayTime { get; set; }

    /// <summary>Base64 thumbnail of save screenshot (placeholder)</summary>
    public string? ThumbnailBase64 { get; set; }

    /// <summary>Display-friendly save description</summary>
    public string GetDescription(GameLanguage lang)
    {
        var chapter = lang switch
        {
            GameLanguage.Chinese => string.IsNullOrEmpty(ChapterNameZh) ? ChapterName : ChapterNameZh,
            GameLanguage.Japanese => string.IsNullOrEmpty(ChapterNameJa) ? ChapterName : ChapterNameJa,
            _ => string.IsNullOrEmpty(ChapterNameEn) ? ChapterName : ChapterNameEn
        };
        return $"{SaveTime:yyyy/MM/dd HH:mm} - {chapter}";
    }
}
