using System.IO;
using NAudio.Wave;
using NAudio.Vorbis;

namespace NanoUint;

/// <summary>音频管理器。引擎内置的静态 API，管理 BGM/SFX/Voice 播放和音量。</summary>
public static class AudioManager
{
    private static WaveOutEvent? _bgmDevice;
    private static WaveOutEvent? _sfxDevice;
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

    #region 音量

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

    #endregion

    #region 事件

    public static event Action? VoiceFinished;
    public static bool IsVoicePlaying { get; private set; }

    /// <summary>当前正在播放的语音总时长（秒）。语音未播放时返回 0。</summary>
    public static double VoiceDuration =>
        _voiceStream?.TotalTime.TotalSeconds ?? 0.0;

    /// <summary>预先探测语音文件的时长（秒）。独立打开文件读取 TotalTime，不受播放状态影响。</summary>
    public static double ProbeVoiceDuration(string resourcePath)
    {
        try
        {
            using var stream = CreateReader(resourcePath);
            return stream.TotalTime.TotalSeconds;
        }
        catch
        {
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
        // 如果是故意停止（切换 BGM / StopBGM），不循环
        if (_isStoppingBgm) return;

        // 循环播放
        if (_bgmStream != null && _bgmDevice != null)
        {
            try
            {
                _bgmStream.Position = 0;
                _bgmDevice.Play();
            }
            catch (ObjectDisposedException)
            {
                // 设备已被释放，忽略
            }
        }
    }

    #endregion

    #region SFX

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

    #endregion

    #region Voice

    public static void PlayVoice(string resourcePath)
    {
        StopVoice();
        try
        {
            _voiceStream = CreateReader(resourcePath);
            _voiceDevice = new WaveOutEvent();
            _voiceDevice.Volume = EffectiveVoiceVolume;
            _voiceDevice.Init(_voiceStream);
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
        // 如果是故意停止（切换 Voice / StopVoice），不处理
        if (_isStoppingVoice) return;

        IsVoicePlaying = false;
        _voiceDevice?.Dispose();
        _voiceStream?.Dispose();
        _voiceDevice = null;
        _voiceStream = null;
        VoiceFinished?.Invoke();
    }

    #endregion

    #region 全局

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
        // 直接从文件系统加载
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
