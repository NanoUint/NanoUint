namespace NanoUint.Models;

/// <summary>
/// Represents a character in the visual novel with their sprites and metadata.
/// </summary>
public class CharacterData
{
    /// <summary>Unique identifier for this character</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Character name in Chinese</summary>
    public string NameZh { get; set; } = string.Empty;
    /// <summary>Character name in English</summary>
    public string NameEn { get; set; } = string.Empty;
    /// <summary>Character name in Japanese</summary>
    public string NameJa { get; set; } = string.Empty;

    /// <summary>Default sprite path</summary>
    public string? DefaultSprite { get; set; }

    /// <summary>Dictionary of expression name → sprite path</summary>
    public Dictionary<string, string> Expressions { get; set; } = new();

    /// <summary>Default position on screen</summary>
    public CharacterPosition DefaultPosition { get; set; } = CharacterPosition.Center;

    /// <summary>Get name based on language</summary>
    public string GetName(GameLanguage lang) => lang switch
    {
        GameLanguage.Chinese => NameZh,
        GameLanguage.Japanese => NameJa,
        _ => NameEn
    };

    /// <summary>Get a sprite path by expression name, falling back to default</summary>
    public string? GetSprite(string? expression = null)
    {
        if (expression != null && Expressions.TryGetValue(expression, out var sprite))
            return sprite;
        return DefaultSprite;
    }
}
