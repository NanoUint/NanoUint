namespace NanoUintVN.Settings;

/// <summary>
/// Pushes VN-only policy onto a playback controller: auto-advance timing, skip policy and the
/// automatic quick save policy.
/// </summary>
public sealed class VNPlaybackSettingsApplier : IVNSettingsApplier
{
    private readonly VNPlaybackController _controller;

    public VNPlaybackSettingsApplier(VNPlaybackController controller) => _controller = controller;

    public void Apply(VNSettings settings)
    {
        _controller.AutoAdvanceDelay = AutoAdvanceDelayFor(settings.Text.MessageSpeed);
        _controller.SkipPolicy = settings.Text.SkipMode;
        _controller.AutoQuickSavePolicy = settings.Basic.AutoQuickSave;
        _controller.AutoSave.Enabled = settings.Basic.AutoQuickSave != AutoQuickSaveMode.Off;
    }

    /// <summary>Instant text needs no wait; otherwise hold long enough to read a line.</summary>
    private static float AutoAdvanceDelayFor(float charactersPerSecond)
        => charactersPerSecond <= 0f ? 1.5f : 3.5f;
}
