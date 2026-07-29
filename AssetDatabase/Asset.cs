namespace NanoUint;

/// <summary>
/// 资源类型枚举。
/// </summary>
public enum AssetType
{
    Sprite,
    Audio,
    Script,
    Font,
    RawData
}

/// <summary>
/// 所有引擎资源的抽象基类。类似 UnityEngine.Object（资源维度）。
/// </summary>
public abstract class Asset
{
    /// <summary>资源加载路径（如 "SteinsGateX.Resources.Sprites.Okabe.png"）。</summary>
    public string Path { get; internal set; } = "";

    /// <summary>资源文件名（不含扩展名）。</summary>
    public string Name { get; internal set; } = "";

    /// <summary>资源类型。</summary>
    public AssetType Type { get; protected set; }

    internal Asset() { }

    public override string ToString() => $"{Type}:{Name} ({Path})";
}

/// <summary>
/// 精灵（图片）资源。
/// </summary>
public sealed class Sprite : Asset
{
    internal byte[] ImageData = Array.Empty<byte>();
    public int Width { get; internal set; }
    public int Height { get; internal set; }

    public Sprite()
    {
        Type = AssetType.Sprite;
    }
}

/// <summary>
/// 音频资源。
/// </summary>
public sealed class AudioClip : Asset
{
    internal byte[] AudioData = Array.Empty<byte>();
    public float Duration { get; internal set; }

    public AudioClip()
    {
        Type = AssetType.Audio;
    }
}

/// <summary>
/// 脚本文本资源。
/// </summary>
public sealed class ScriptAsset : Asset
{
    public string Source { get; internal set; } = "";

    public ScriptAsset()
    {
        Type = AssetType.Script;
    }
}
