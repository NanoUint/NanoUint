namespace NanoUintVN.Settings;

/// <summary>
/// High-level settings manager built on ISettingsStore. Owns the live VNSettings instance,
/// validates it on every mutation, and notifies an optional applier so engine services follow.
/// </summary>
public sealed class SettingsManagerV2
{
    private readonly ISettingsStore _store;
    private readonly IVNSettingsApplier? _applier;
    private VNSettings _settings;
    private bool _suspended;

    public SettingsManagerV2(ISettingsStore store, IVNSettingsApplier? applier = null, VNSettings? defaults = null)
    {
        _store = store;
        _applier = applier;
        _settings = defaults?.Clone() ?? new VNSettings();
        _settings.Clamp();
    }

    /// <summary>The live settings instance. Treat as read-only; mutate through <see cref="Update"/>.</summary>
    public VNSettings Current => _settings;

    public string StoreName => _store.Name;

    public string DescribeLocation() => _store.DescribeLocation();

    /// <summary>Raised after every applied change.</summary>
    public event Action<VNSettings>? Changed;

    /// <summary>Loads from the store. Missing or corrupt data falls back to the current values.</summary>
    public bool Load()
    {
        var loaded = _store.Load();
        if (loaded == null) return false;

        loaded.Clamp();
        _settings = loaded;
        Apply();
        return true;
    }

    public bool Save() => _store.Save(_settings);

    /// <summary>Restores defaults and pushes them through the applier.</summary>
    public void Reset()
    {
        _settings = new VNSettings();
        Apply();
    }

    /// <summary>Mutates settings in place, clamps the result, then applies and notifies.</summary>
    public void Update(Action<VNSettings> mutate)
    {
        mutate(_settings);
        _settings.Clamp();
        Apply();
    }

    /// <summary>Applies the current values to engine services without raising <see cref="Changed"/>.</summary>
    public void Apply()
    {
        _applier?.Apply(_settings);
        if (_suspended) return;
        Changed?.Invoke(_settings);
    }

    /// <summary>Batches several updates into a single notification.</summary>
    public IDisposable SuspendNotifications()
    {
        _suspended = true;
        return new Scope(this);
    }

    private sealed class Scope : IDisposable
    {
        private readonly SettingsManagerV2 _owner;
        public Scope(SettingsManagerV2 owner) => _owner = owner;
        public void Dispose()
        {
            _owner._suspended = false;
            _owner.Changed?.Invoke(_owner._settings);
        }
    }
}
