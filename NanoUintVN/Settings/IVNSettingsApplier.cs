namespace NanoUintVN.Settings;

/// <summary>
/// Pushes VN settings onto engine services. Implemented by the host (or by the game) so the
/// settings model itself stays free of engine and platform dependencies.
/// </summary>
public interface IVNSettingsApplier
{
    void Apply(VNSettings settings);
}
