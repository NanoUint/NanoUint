namespace NanoUintVN.Settings;

/// <summary>
/// Visual-novel facing settings. This is the source of truth for the VN layer; engine-level
/// services (audio, screen) are driven from here by an applier, never the other way round.
/// </summary>
public sealed class VNSettings
{
    /// <summary>Schema version, bumped when a migration is required.</summary>
    public int Version { get; set; } = 1;

    public BasicSettings Basic { get; set; } = new();
    public TextSettings Text { get; set; } = new();
    public AudioSettings Audio { get; set; } = new();

    /// <summary>Returns a deep copy, so callers can edit without touching the live instance.</summary>
    public VNSettings Clone() => new()
    {
        Version = Version,
        Basic = Basic.Clone(),
        Text = Text.Clone(),
        Audio = Audio.Clone(),
    };

    /// <summary>Clamps every field into its legal range. Applied after load and after updates.</summary>
    public void Clamp()
    {
        Basic.Clamp();
        Text.Clamp();
        Audio.Clamp();
    }
}

/// <summary>General group: auto quick save, display mode, resolution.</summary>
public sealed class BasicSettings
{
    public AutoQuickSaveMode AutoQuickSave { get; set; } = AutoQuickSaveMode.Off;
    public bool Fullscreen { get; set; }
    public int ResolutionWidth { get; set; } = 1920;
    public int ResolutionHeight { get; set; } = 1080;

    public BasicSettings Clone() => (BasicSettings)MemberwiseClone();

    public void Clamp()
    {
        if (ResolutionWidth < 320) ResolutionWidth = 320;
        if (ResolutionHeight < 240) ResolutionHeight = 240;
        if (!Enum.IsDefined(AutoQuickSave)) AutoQuickSave = AutoQuickSaveMode.Off;
    }
}

/// <summary>Text group: message speed and skip policy.</summary>
public sealed class TextSettings
{
    /// <summary>Characters per second. 0 means instant.</summary>
    public float MessageSpeed { get; set; } = 40f;

    public SkipMode SkipMode { get; set; } = SkipMode.ReadOnly;

    public TextSettings Clone() => (TextSettings)MemberwiseClone();

    public void Clamp()
    {
        if (float.IsNaN(MessageSpeed) || MessageSpeed < 0f) MessageSpeed = 0f;
        if (MessageSpeed > 200f) MessageSpeed = 200f;
        if (!Enum.IsDefined(SkipMode)) SkipMode = SkipMode.ReadOnly;
    }
}

/// <summary>Audio group: per-channel volumes and voice synchronisation.</summary>
public sealed class AudioSettings
{
    public float VoiceVolume { get; set; } = 1f;
    public float BgmVolume { get; set; } = 0.8f;
    public float VideoVolume { get; set; } = 1f;

    /// <summary>When true, advancing waits for the current voice clip to finish.</summary>
    public bool VoiceSync { get; set; } = true;

    public AudioSettings Clone() => (AudioSettings)MemberwiseClone();

    public void Clamp()
    {
        VoiceVolume = Clamp01(VoiceVolume);
        BgmVolume = Clamp01(BgmVolume);
        VideoVolume = Clamp01(VideoVolume);
    }

    private static float Clamp01(float v)
    {
        if (float.IsNaN(v)) return 0f;
        return v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
