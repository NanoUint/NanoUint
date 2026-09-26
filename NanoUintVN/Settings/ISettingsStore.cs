namespace NanoUintVN.Settings;

/// <summary>Storage backend for VN settings. Implementations: FileSettingsStore, MemorySettingsStore.</summary>
public interface ISettingsStore
{
    /// <summary>Backend name, for logs and debug panels.</summary>
    string Name { get; }

    /// <summary>Human-readable description of where settings live.</summary>
    string DescribeLocation();

    /// <summary>Reads settings. Returns null when nothing has been stored yet.</summary>
    VNSettings? Load();

    bool Save(VNSettings settings);

    bool Delete();
}
