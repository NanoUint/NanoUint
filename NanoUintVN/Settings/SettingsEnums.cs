namespace NanoUintVN.Settings;

/// <summary>When the runtime writes an automatic quick save.</summary>
public enum AutoQuickSaveMode
{
    /// <summary>Automatic quick save is disabled.</summary>
    Off,

    /// <summary>Write once per scene change.</summary>
    OnSceneChange,

    /// <summary>Write once per advance (one Enter).</summary>
    OnAdvance,

    /// <summary>Write on both scene change and advance.</summary>
    All,
}

/// <summary>Which text the fast-forward key is allowed to skip.</summary>
public enum SkipMode
{
    /// <summary>Only skip lines already read in any previous session.</summary>
    ReadOnly,

    /// <summary>Skip every line, read or not.</summary>
    All,
}
