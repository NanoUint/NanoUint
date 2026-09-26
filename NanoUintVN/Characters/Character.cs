namespace NanoUintVN.Characters;

/// <summary>
/// Base type for a cast member. The engine defines the contract only; concrete characters are
/// authored on the game side, so no story content leaks into the VN layer.
/// </summary>
public abstract class Character
{
    /// <summary>Stable identifier used by scripts and save data.</summary>
    public abstract string Id { get; }

    /// <summary>Name shown in the dialogue box.</summary>
    public virtual string DisplayName => Id;

    /// <summary>Optional "#RRGGBB" tint for the speaker name.</summary>
    public virtual string? NameColorHex => null;

    /// <summary>Portrait and expression lookup. Defaults to an empty set.</summary>
    public virtual ICharImages Images => EmptyCharImages.Instance;

    /// <summary>Voice clip lookup. Null when the character is unvoiced.</summary>
    public virtual IVoices? Voices => null;

    /// <summary>Phone contract. Null unless the game wires a phone system for this character.</summary>
    public virtual IPhoneInfo? Phone => null;
}
