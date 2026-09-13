using System.IO;
using NAudio.Wave;
using NAudio.Vorbis;

namespace NanoUint;

/// <summary>Static API for BGM, SFX and voice playback and volume.</summary>
public static class AudioManager
{
    private static WaveOutEvent? _bgmDevice;
    private static WaveOutEvent? _voiceDevice;

    private static WaveStream? _bgmStream;
    private static WaveStream? _voiceStream;
    private static string? _currentBgmPath;
    private static bool _isStoppingBgm;
    private static bool _isStoppingVoice;

    private static float _masterVolume = 1f;
    private static float _bgmVolume = 1f;
    private static float _sfxVolume = 1f;
    private static float _voiceVolume = 1f;

    #region Volume

    /// <summary>Master volume (0-1). Prefer EngineContext.Default.Audio.MasterVolume.</summary>
    public static float MasterVolume
    {
        get => _masterVolume;
        set { _masterVolume = Math.Clamp(value, 0f, 1f); ApplyVolumes(); }
    }
    /// <summary>BGM channel volume (0-1). Prefer EngineContext.Default.Audio.BGMVolume.</summary>
    public static float BGMVolume
    {
        get => _bgmVolume;
        set { _bgmVolume = Math.Clamp(value, 0f, 1f); ApplyVolumes(); }
    }
    /// <summary>SFX channel volume (0-1). Prefer EngineContext.Default.Audio.SFXVolume.</summary>
    public static float SFXVolume
    {
        get => _sfxVolume;
        set { _sfxVolume = Math.Clamp(value, 0f, 1f); }
    }
    /// <summary>Voice channel volume (0-1). Prefer EngineContext.Default.Audio.VoiceVolume.</summary>
    public static float VoiceVolume
    {
        get => _voiceVolume;
        set { _voiceVolume = Math.Clamp(value, 0f, 1f); ApplyVolumes(); }
    }

    private static float EffectiveBGMVolume => _masterVolume * _bgmVolume;
    private static float EffectiveSFXVolume => _masterVolume * _sfxVolume;
    private static float EffectiveVoiceVolume => _masterVolume * _voiceVolume;

    #endregion

    #region Events

    /// <summary>Voice playback finished event. Prefer EngineContext.Default.Audio event subscription.</summary>
    public static event Action? VoiceFinished;
    public static bool IsVoicePlaying { get; private set; }

    /// <summary>Total duration in seconds of the currently playing voice; 0 when no voice is playing.</summary>
    public static double VoiceDuration =>
        _voiceStream?.TotalTime.TotalSeconds ?? 0.0;

    /// <summary>Probes a voice file's duration in seconds by opening it independently of playback state.</summary>
    public static double ProbeVoiceDuration(string resourcePath)
    {
        try
        {
            using var stream = CreateReader(resourcePath);
            return stream.TotalTime.TotalSeconds;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to probe voice duration '{resourcePath}': {ex.Message}");
            return 0.0;
        }
    }

    #endregion

    #region BGM

        public static void PlayBGM(string resourcePath)
    {
        if (resourcePath == _currentBgmPath && _bgmDevice?.PlaybackState == PlaybackState.Playing)
            return;

        StopBGM();

        WaveStream? stream = null;
        WaveOutEvent? device = null;
        try
        {
            stream = CreateReader(resourcePath);
            device = new WaveOutEvent();
            device.PlaybackStopped += OnBGMStopped;
            device.Init(stream);
            device.Volume = EffectiveBGMVolume;
            device.Play();

            // Commit state only after playback succeeds, so a failed start leaves no half-initialized device/stream.
            _bgmStream = stream;
            _bgmDevice = device;
            _currentBgmPath = resourcePath;
            Debug.Log($"BGM playing: {resourcePath}");
        }
        catch (Exception ex)
        {
            device?.Dispose();
            stream?.Dispose();
            Debug.LogError($"Failed to play BGM '{resourcePath}': {ex.Message}");
        }
    }

        public static void StopBGM()
    {
        _isStoppingBgm = true;
        _bgmDevice?.Stop();
        _bgmDevice?.Dispose();
        _bgmStream?.Dispose();
        _bgmDevice = null;
        _bgmStream = null;
        _currentBgmPath = null;
        _isStoppingBgm = false;
    }

        public static void PauseBGM() => _bgmDevice?.Pause();
        public static void ResumeBGM() => _bgmDevice?.Play();

    private static void OnBGMStopped(object? sender, StoppedEventArgs e)
    {
        // An intentional stop (BGM switch / StopBGM) must not loop.
        if (_isStoppingBgm) return;

        if (_bgmStream != null && _bgmDevice != null)
        {
            try
            {
                _bgmStream.Position = 0;
                _bgmDevice.Play();
            }
            catch (ObjectDisposedException)
            {
                // The device was already disposed, so nothing to recover here.
            }
        }
    }

    #endregion

    #region SFX

        public static void PlaySFX(string resourcePath)
    {
        WaveStream? stream = null;
        WaveOutEvent? device = null;
        try
        {
            stream = CreateReader(resourcePath);
            device = new WaveOutEvent();
            device.Volume = EffectiveSFXVolume;
            device.Init(stream);
            device.Play();
            device.PlaybackStopped += (s, e) => { device.Dispose(); stream.Dispose(); };
        }
        catch (Exception ex)
        {
            device?.Dispose();
            stream?.Dispose();
            Debug.LogError($"Failed to play SFX '{resourcePath}': {ex.Message}");
        }
    }

    #endregion

    #region Voice

        public static void PlayVoice(string resourcePath)
    {
        StopVoice();

        WaveStream? stream = null;
        WaveOutEvent? device = null;
        try
        {
            stream = CreateReader(resourcePath);
            device = new WaveOutEvent();
            device.Volume = EffectiveVoiceVolume;
            device.Init(stream);
            device.PlaybackStopped += OnVoiceStopped;
            device.Play();

            // Commit state only after playback succeeds, so a failed start leaves no half-initialized device/stream.
            _voiceStream = stream;
            _voiceDevice = device;
            IsVoicePlaying = true;
            Debug.Log($"Voice playing: {resourcePath}");
        }
        catch (Exception ex)
        {
            device?.Dispose();
            stream?.Dispose();
            Debug.LogError($"Failed to play voice '{resourcePath}': {ex.Message}");
        }
    }

        public static void StopVoice()
    {
        _isStoppingVoice = true;
        _voiceDevice?.Stop();
        _voiceDevice?.Dispose();
        _voiceStream?.Dispose();
        _voiceDevice = null;
        _voiceStream = null;
        IsVoicePlaying = false;
        _isStoppingVoice = false;
    }

    private static void OnVoiceStopped(object? sender, StoppedEventArgs e)
    {
        // An intentional stop (Voice switch / StopVoice) must be ignored here.
        if (_isStoppingVoice) return;

        IsVoicePlaying = false;
        _voiceDevice?.Dispose();
        _voiceStream?.Dispose();
        _voiceDevice = null;
        _voiceStream = null;
        VoiceFinished?.Invoke();
    }

    #endregion

    #region Global

        public static void StopAll()
    {
        StopBGM();
        StopVoice();
    }

        public static void Dispose()
    {
        StopAll();
    }

    private static void ApplyVolumes()
    {
        if (_bgmDevice != null)
            _bgmDevice.Volume = EffectiveBGMVolume;
        if (_voiceDevice != null)
            _voiceDevice.Volume = EffectiveVoiceVolume;
    }

    private static WaveStream CreateReader(string path)
    {
        var fullPath = AssetDatabase.GetFullPath(path);
        if (fullPath == null || !File.Exists(fullPath))
            throw new FileNotFoundException($"Audio asset not found: '{path}'");

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        return ext switch
        {
            ".ogg" => new VorbisWaveReader(fullPath),
            ".mp3" => new Mp3FileReader(fullPath),
            ".wav" => new WaveFileReader(fullPath),
            ".aiff" or ".aif" => new AiffFileReader(fullPath),
            _ => throw new NotSupportedException($"Audio format '{ext}' is not supported.")
        };
    }
    #endregion
}
