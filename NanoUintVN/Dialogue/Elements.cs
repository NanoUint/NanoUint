namespace NanoUintVN.Dialogue;

/// <summary>Background image element.</summary>
public sealed class BackgroundElement : VNElement
{
    public string SpritePath { get; init; } = string.Empty;
    public PageTransition Transition { get; init; } = PageTransition.None;
}

/// <summary>Sprite/character element.</summary>
public sealed class SpriteElement : VNElement
{
    public string SpritePath { get; init; } = string.Empty;
    public float X { get; init; }
    public float Y { get; init; }
    public bool FlipX { get; init; }
    public PageTransition Transition { get; init; } = PageTransition.None;
}

/// <summary>BGM element.</summary>
public sealed class BgmElement : VNElement
{
    public string MusicPath { get; init; } = string.Empty;
    public bool StopAfter { get; init; }
}

/// <summary>SFX element.</summary>
public sealed class SfxElement : VNElement
{
    public string SoundPath { get; init; } = string.Empty;
}

/// <summary>Choice element — multiple options on a page.</summary>
public sealed class ChoiceElement : VNElement
{
    public string[] Labels { get; init; } = Array.Empty<string>();
    public string[] Targets { get; init; } = Array.Empty<string>();
}
