namespace NanoUint;

/// <summary>Asset type enumeration.</summary>
public enum AssetType
{
    Sprite,
    Audio,
    Script,
    Font,
    RawData
}

/// <summary>Abstract base class for all engine assets.</summary>
public abstract class Asset
{
    /// <summary>Asset load path (e.g. "SteinsGateX.Resources.Sprites.Okabe.png").</summary>
    public string Path { get; internal set; } = "";

    /// <summary>Asset file name without extension.</summary>
    public string Name { get; internal set; } = "";

    /// <summary>Asset type.</summary>
    public AssetType Type { get; protected set; }

    internal Asset() { }

    public override string ToString() => $"{Type}:{Name} ({Path})";
}

/// <summary>Sprite (image) asset.</summary>
public sealed class Sprite : Asset
{
    internal byte[] ImageData = Array.Empty<byte>();
    public int Width { get; internal set; }
    public int Height { get; internal set; }

    /// <summary>Pixels per world unit (default 100).</summary>
    public float PixelsPerUnit { get; internal set; } = 100f;

    public Sprite()
    {
        Type = AssetType.Sprite;
    }
}

/// <summary>Audio asset.</summary>
public sealed class AudioClip : Asset
{
    internal byte[] AudioData = Array.Empty<byte>();
    public float Duration { get; internal set; }

    public AudioClip()
    {
        Type = AssetType.Audio;
    }
}

/// <summary>Script text asset.</summary>
public sealed class ScriptAsset : Asset
{
    public string Source { get; internal set; } = "";

    public ScriptAsset()
    {
        Type = AssetType.Script;
    }
}
