using System.Windows.Media;

namespace NanoUint.Models;

/// <summary>
/// Supported languages for the game.
/// </summary>
public enum GameLanguage
{
    Chinese,
    English,
    Japanese
}

/// <summary>
/// Position of a character sprite on screen.
/// </summary>
public enum CharacterPosition
{
    Left,
    Center,
    Right,
    OffscreenLeft,
    OffscreenRight
}

/// <summary>
/// A single dialogue entry in the visual novel.
/// Supports Chinese, English, and Japanese text.
/// </summary>
public class DialogueEntry
{
    /// <summary>Speaker name in Chinese</summary>
    public string SpeakerZh { get; set; } = string.Empty;
    /// <summary>Speaker name in English</summary>
    public string SpeakerEn { get; set; } = string.Empty;
    /// <summary>Speaker name in Japanese</summary>
    public string SpeakerJa { get; set; } = string.Empty;

    /// <summary>Dialogue text in Chinese</summary>
    public string TextZh { get; set; } = string.Empty;
    /// <summary>Dialogue text in English</summary>
    public string TextEn { get; set; } = string.Empty;
    /// <summary>Dialogue text in Japanese</summary>
    public string TextJa { get; set; } = string.Empty;

    /// <summary>Character sprite image path (relative to game assets)</summary>
    public string? CharacterSprite { get; set; }

    /// <summary>Where the character appears on screen</summary>
    public CharacterPosition CharacterPosition { get; set; } = CharacterPosition.Center;

    /// <summary>Background image path (relative to game assets)</summary>
    public string? BackgroundImage { get; set; }

    /// <summary>Audio file path for BGM or voice</summary>
    public string? AudioFile { get; set; }

    /// <summary>Whether this audio is BGM (looping) or a voice line</summary>
    public bool IsBGM { get; set; } = false;

    /// <summary>Color of the dialogue box for this entry</summary>
    public Color DialogueBoxColor { get; set; } = Color.FromRgb(10, 10, 10);

    /// <summary>Whether to show the character name label</summary>
    public bool ShowSpeakerName { get; set; } = true;

    /// <summary>Fade transition duration in milliseconds</summary>
    public int TransitionMs { get; set; } = 500;

    /// <summary>Special effect to play (shake, flash, etc.)</summary>
    public string? Effect { get; set; }

    /// <summary>Get the speaker name based on the current language</summary>
    public string GetSpeaker(GameLanguage lang) => lang switch
    {
        GameLanguage.Chinese => SpeakerZh,
        GameLanguage.Japanese => SpeakerJa,
        _ => SpeakerEn
    };

    /// <summary>Get the dialogue text based on the current language</summary>
    public string GetText(GameLanguage lang) => lang switch
    {
        GameLanguage.Chinese => TextZh,
        GameLanguage.Japanese => TextJa,
        _ => TextEn
    };
}
