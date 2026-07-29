using System.IO;
using NAudio.Wave;
using NAudio.Vorbis;

namespace NanoUint;

/// <summary>
/// 音频管理器。引擎内置的静态 API，管理 BGM/SFX/Voice 播放和音量。
/// 内部使用 NAudio。
/// </summary>
public static class AudioManager
{
    private static WaveOutEvent? _bgmDevice;
    private static WaveOutEvent? _sfxDevice;
    private static WaveOutEvent? _voiceDevice;

    private static WaveStream? _bgmStream;
    private static string? _currentBgmPath;

    private static float _masterVolume = 1f;
    private static float _bgmVolume = 1f;
    private static float _sfxVolume = 1f;
    private static float _voiceVolume = 1f;

    // ── 音量 ──

    public static float MasterVolume
    {
        get => _masterVolume;
        set { _masterVolume = Math.Clamp(value, 0f, 1f); ApplyVolumes(); }
    }
    public static float BGMVolume
    {
        get => _bgmVolume;
        set { _bgmVolume = Math.Clamp(value, 0f, 1f); ApplyVolumes(); }
    }
    public static float SFXVolume
    {
        get => _sfxVolume;
        set { _sfxVolume = Math.Clamp(value, 0f, 1f); }
    }
    public static float VoiceVolume
    {
        get => _voiceVolume;
        set { _voiceVolume = Math.Clamp(value, 0f, 1f); }
    }

    private static float EffectiveBGMVolume => _masterVolume * _bgmVolume;
    private static float EffectiveSFXVolume => _masterVolume * _sfxVolume;
    private static float EffectiveVoiceVolume => _masterVolume * _voiceVolume;

    // ── 事件 ──

    public static event Action? VoiceFinished;
    public static bool IsVoicePlaying { get; private set; }

    // ── BGM ──

    public static void PlayBGM(string resourcePath)
    {
        if (resourcePath == _currentBgmPath && _bgmDevice?.PlaybackState == PlaybackState.Playing)
            return;

        StopBGM();

        try
        {
            _bgmStream = CreateReader(resourcePath);
            _bgmDevice = new WaveOutEvent();
            _bgmDevice.PlaybackStopped += OnBGMStopped;
            _bgmDevice.Init(_bgmStream);
            _bgmDevice.Volume = EffectiveBGMVolume;
            _bgmDevice.Play();
            _currentBgmPath = resourcePath;
            Debug.Log($"BGM playing: {resourcePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to play BGM '{resourcePath}': {ex.Message}");
        }
    }

    public static void StopBGM()
    {
        _bgmDevice?.Stop();
        _bgmDevice?.Dispose();
        _bgmStream?.Dispose();
        _bgmDevice = null;
        _bgmStream = null;
        _currentBgmPath = null;
    }

    public static void PauseBGM() => _bgmDevice?.Pause();
    public static void ResumeBGM() => _bgmDevice?.Play();

    private static void OnBGMStopped(object? sender, StoppedEventArgs e)
    {
        // 循环播放
        if (_bgmStream != null && _bgmDevice != null)
        {
            _bgmStream.Position = 0;
            _bgmDevice.Play();
        }
    }

    // ── SFX ──

    public static void PlaySFX(string resourcePath)
    {
        try
        {
            var stream = CreateReader(resourcePath);
            var device = new WaveOutEvent();
            device.Volume = EffectiveSFXVolume;
            device.Init(stream);
            device.Play();
            device.PlaybackStopped += (s, e) => { device.Dispose(); stream.Dispose(); };
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to play SFX '{resourcePath}': {ex.Message}");
        }
    }

    // ── Voice ──

    public static void PlayVoice(string resourcePath)
    {
        StopVoice();
        try
        {
            var stream = CreateReader(resourcePath);
            _voiceDevice = new WaveOutEvent();
            _voiceDevice.Volume = EffectiveVoiceVolume;
            _voiceDevice.Init(stream);
            _voiceDevice.PlaybackStopped += OnVoiceStopped;
            _voiceDevice.Play();
            IsVoicePlaying = true;
            Debug.Log($"Voice playing: {resourcePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to play voice '{resourcePath}': {ex.Message}");
        }
    }

    public static void StopVoice()
    {
        _voiceDevice?.Stop();
        _voiceDevice?.Dispose();
        _voiceDevice = null;
        IsVoicePlaying = false;
    }

    private static void OnVoiceStopped(object? sender, StoppedEventArgs e)
    {
        IsVoicePlaying = false;
        _voiceDevice?.Dispose();
        _voiceDevice = null;
        VoiceFinished?.Invoke();
    }

    // ── 全局 ──

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
        if (_sfxDevice != null)
            _sfxDevice.Volume = EffectiveSFXVolume;
        if (_voiceDevice != null)
            _voiceDevice.Volume = EffectiveVoiceVolume;
    }

    private static WaveStream CreateReader(string path)
    {
        // 直接从文件系统加载（NAudio 原生支持文件路径，比嵌入资源+temp 快得多）
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
}
