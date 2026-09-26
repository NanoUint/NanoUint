namespace NanoUintVN.Dialogue;

/// <summary>A single dialogue beat: speaker + text + optional voice/bgm.</summary>
public sealed class DialogueBeat
{
    public string Speaker { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string? VoicePath { get; init; }
    public string? BgmPath { get; init; }
    public bool StopBgmAfter { get; init; }
    public float TextSpeed { get; init; } = 0.05f;
}
