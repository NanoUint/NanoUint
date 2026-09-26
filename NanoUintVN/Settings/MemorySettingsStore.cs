namespace NanoUintVN.Settings;

/// <summary>In-memory settings store. Useful for tests and for running without a writable disk.</summary>
public sealed class MemorySettingsStore : ISettingsStore
{
    private string? _json;

    public string Name => "memory";

    public string DescribeLocation() => "(memory)";

    public VNSettings? Load()
        => string.IsNullOrWhiteSpace(_json)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<VNSettings>(_json);

    public bool Save(VNSettings settings)
    {
        _json = System.Text.Json.JsonSerializer.Serialize(settings);
        return true;
    }

    public bool Delete()
    {
        _json = null;
        return true;
    }
}
