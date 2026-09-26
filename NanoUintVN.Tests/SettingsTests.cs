using System.IO;
using NanoUintVN.Settings;

namespace NanoUintVN.Tests;

public class SettingsTests : IDisposable
{
    private readonly string _dir;

    public SettingsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "NanoUintVN_Settings_" + Guid.NewGuid().ToString("N")[..8]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Defaults_MatchRequestedGroups()
    {
        var s = new VNSettings();

        Assert.Equal(AutoQuickSaveMode.Off, s.Basic.AutoQuickSave);
        Assert.False(s.Basic.Fullscreen);
        Assert.Equal(1920, s.Basic.ResolutionWidth);
        Assert.Equal(1080, s.Basic.ResolutionHeight);

        Assert.Equal(40f, s.Text.MessageSpeed);
        Assert.Equal(SkipMode.ReadOnly, s.Text.SkipMode);

        Assert.Equal(1f, s.Audio.VoiceVolume);
        Assert.Equal(0.8f, s.Audio.BgmVolume);
        Assert.Equal(1f, s.Audio.VideoVolume);
        Assert.True(s.Audio.VoiceSync);
    }

    [Fact]
    public void Clone_IsDeep()
    {
        var a = new VNSettings();
        var b = a.Clone();
        b.Text.MessageSpeed = 5f;
        b.Audio.BgmVolume = 0.1f;

        Assert.Equal(40f, a.Text.MessageSpeed);
        Assert.Equal(0.8f, a.Audio.BgmVolume);
    }

    [Fact]
    public void Clamp_RejectsOutOfRangeVolumes()
    {
        var s = new VNSettings();
        s.Audio.VoiceVolume = 3f;
        s.Audio.BgmVolume = -2f;
        s.Audio.VideoVolume = float.NaN;

        s.Clamp();

        Assert.Equal(1f, s.Audio.VoiceVolume);
        Assert.Equal(0f, s.Audio.BgmVolume);
        Assert.Equal(0f, s.Audio.VideoVolume);
    }

    [Fact]
    public void Clamp_GuardsResolutionAndSpeed()
    {
        var s = new VNSettings();
        s.Basic.ResolutionWidth = 10;
        s.Basic.ResolutionHeight = 10;
        s.Text.MessageSpeed = -5f;

        s.Clamp();

        Assert.Equal(320, s.Basic.ResolutionWidth);
        Assert.Equal(240, s.Basic.ResolutionHeight);
        Assert.Equal(0f, s.Text.MessageSpeed);
    }

    [Fact]
    public void Manager_UpdateClampsAndNotifies()
    {
        var mgr = new SettingsManagerV2(new MemorySettingsStore());
        var seen = new List<float>();
        mgr.Changed += s => seen.Add(s.Audio.BgmVolume);

        mgr.Update(s => s.Audio.BgmVolume = 5f);

        Assert.Equal(1f, mgr.Current.Audio.BgmVolume);
        Assert.Single(seen);
        Assert.Equal(1f, seen[0]);
    }

    [Fact]
    public void Manager_RoundTripsThroughFileStore()
    {
        var path = Path.Combine(_dir, "settings.json");
        var mgr = new SettingsManagerV2(new FileSettingsStore(path));
        mgr.Update(s =>
        {
            s.Text.MessageSpeed = 12.5f;
            s.Text.SkipMode = SkipMode.All;
            s.Basic.AutoQuickSave = AutoQuickSaveMode.All;
            s.Basic.Fullscreen = true;
            s.Basic.ResolutionWidth = 1280;
            s.Basic.ResolutionHeight = 720;
            s.Audio.VoiceVolume = 0.25f;
            s.Audio.VideoVolume = 0.5f;
            s.Audio.VoiceSync = false;
        });
        Assert.True(mgr.Save());

        var reloaded = new SettingsManagerV2(new FileSettingsStore(path));
        Assert.True(reloaded.Load());

        Assert.Equal(12.5f, reloaded.Current.Text.MessageSpeed);
        Assert.Equal(SkipMode.All, reloaded.Current.Text.SkipMode);
        Assert.Equal(AutoQuickSaveMode.All, reloaded.Current.Basic.AutoQuickSave);
        Assert.True(reloaded.Current.Basic.Fullscreen);
        Assert.Equal(1280, reloaded.Current.Basic.ResolutionWidth);
        Assert.Equal(720, reloaded.Current.Basic.ResolutionHeight);
        Assert.Equal(0.25f, reloaded.Current.Audio.VoiceVolume);
        Assert.Equal(0.5f, reloaded.Current.Audio.VideoVolume);
        Assert.False(reloaded.Current.Audio.VoiceSync);
    }

    [Fact]
    public void Manager_LoadOnMissingFileKeepsCurrent()
    {
        var mgr = new SettingsManagerV2(new FileSettingsStore(Path.Combine(_dir, "absent.json")));
        mgr.Update(s => s.Text.MessageSpeed = 33f);

        Assert.False(mgr.Load());
        Assert.Equal(33f, mgr.Current.Text.MessageSpeed);
    }

    [Fact]
    public void Manager_ApplierReceivesEveryApply()
    {
        var applier = new RecordingApplier();
        var mgr = new SettingsManagerV2(new MemorySettingsStore(), applier);

        mgr.Update(s => s.Basic.Fullscreen = true);

        Assert.Equal(1, applier.Count);
        Assert.True(applier.Last!.Basic.Fullscreen);
    }

    [Fact]
    public void Manager_SuspendBatchesNotifications()
    {
        var mgr = new SettingsManagerV2(new MemorySettingsStore());
        var count = 0;
        mgr.Changed += _ => count++;

        using (mgr.SuspendNotifications())
        {
            mgr.Update(s => s.Text.MessageSpeed = 10f);
            mgr.Update(s => s.Text.MessageSpeed = 20f);
            Assert.Equal(0, count);
        }

        Assert.Equal(1, count);
        Assert.Equal(20f, mgr.Current.Text.MessageSpeed);
    }

    [Fact]
    public void Manager_ResetRestoresDefaults()
    {
        var mgr = new SettingsManagerV2(new MemorySettingsStore());
        mgr.Update(s => s.Text.SkipMode = SkipMode.All);

        mgr.Reset();

        Assert.Equal(SkipMode.ReadOnly, mgr.Current.Text.SkipMode);
    }

    private sealed class RecordingApplier : IVNSettingsApplier
    {
        public int Count { get; private set; }
        public VNSettings? Last { get; private set; }

        public void Apply(VNSettings settings)
        {
            Count++;
            Last = settings;
        }
    }
}
