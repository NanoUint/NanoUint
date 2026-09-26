using NanoUint;

namespace NanoUintVN.Settings;

/// <summary>
/// Pushes the VN settings that map onto engine services: display mode, resolution and the
/// audio channels. VN-only policy (message speed, skip, auto quick save) is handled elsewhere.
/// </summary>
public sealed class EngineSettingsApplier : IVNSettingsApplier
{
    public void Apply(VNSettings settings)
    {
        ApplyDisplay(settings.Basic);
        ApplyAudio(settings.Audio);
    }

    private static void ApplyDisplay(BasicSettings basic)
    {
        if (ScreenManager.Width != basic.ResolutionWidth || ScreenManager.Height != basic.ResolutionHeight)
        {
            ScreenManager.SetResolution(basic.ResolutionWidth, basic.ResolutionHeight);
        }

        if (ScreenManager.IsFullscreen != basic.Fullscreen)
        {
            ScreenManager.IsFullscreen = basic.Fullscreen;
        }
    }

    private static void ApplyAudio(AudioSettings audio)
    {
        AudioManager.BGMVolume = audio.BgmVolume;
        AudioManager.VoiceVolume = audio.VoiceVolume;
    }
}
