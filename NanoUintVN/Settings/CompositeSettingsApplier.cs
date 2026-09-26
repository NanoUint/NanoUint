namespace NanoUintVN.Settings;

/// <summary>Fans a single settings change out to several appliers, in order.</summary>
public sealed class CompositeSettingsApplier : IVNSettingsApplier
{
    private readonly List<IVNSettingsApplier> _appliers = new();

    public CompositeSettingsApplier(params IVNSettingsApplier[] appliers) => _appliers.AddRange(appliers);

    public CompositeSettingsApplier Add(IVNSettingsApplier applier)
    {
        if (applier != null && !_appliers.Contains(applier)) _appliers.Add(applier);
        return this;
    }

    public void Apply(VNSettings settings)
    {
        foreach (var applier in _appliers) applier.Apply(settings);
    }
}
