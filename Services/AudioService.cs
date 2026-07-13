using System.IO;
using NAudio.Wave;
using NAudio.Vorbis;

namespace NanoUint.Services;

/// <summary>
/// 管理 BGM、SFX 和语音的音频播放。
/// 使用 NAudio 支持 MP3/WAV/OGG 等多种格式。
/// </summary>
public class AudioService : IDisposable
{
    private WaveOutEvent? _bgmOut;
    private WaveOutEvent? _sfxOut;
    private WaveOutEvent? _voiceOut;

    private WaveStream? _bgmStream;
    private WaveStream? _sfxStream;
    private WaveStream? _voiceStream;

    private double _masterVolume = 0.8;
    private double _bgmVolume = 0.8;
    private double _sfxVolume = 0.8;
    private double _voiceVolume = 1.0;

    private string? _currentBgmPath;
    private readonly string _assetBasePath;
    private bool _disposed;

    /// <summary>语音播放完毕时触发</summary>
    public event Action? VoiceFinished;

    /// <summary>当前是否正在播放语音</summary>
    public bool IsVoicePlaying => _voiceOut?.PlaybackState == PlaybackState.Playing;

    public AudioService(string assetBasePath = "Assets/Audio")
    {
        _assetBasePath = assetBasePath;
    }

    /// <summary>创建适合文件格式的 WaveStream</summary>
    private static WaveStream? CreateReader(string fullPath)
    {
        if (!File.Exists(fullPath)) return null;

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        try
        {
            return ext switch
            {
                ".ogg" => new VorbisWaveReader(fullPath),
                ".mp3" or ".wav" or ".aiff" or ".aif" => new AudioFileReader(fullPath),
                _ => new AudioFileReader(fullPath) // 尝试 MediaFoundation
            };
        }
        catch (Exception ex)
        {
            DebugConsole.LogError("Audio-Reader", ex);
            return null;
        }
    }

    #region 音量

    public void SetVolumes(double master, double bgm, double sfx, double voice)
    {
        _masterVolume = master;
        _bgmVolume = bgm;
        _sfxVolume = sfx;
        _voiceVolume = voice;
        ApplyVolumes();
    }

    private void ApplyVolumes()
    {
        if (_bgmOut != null) _bgmOut.Volume = (float)(_masterVolume * _bgmVolume);
        if (_sfxOut != null) _sfxOut.Volume = (float)(_masterVolume * _sfxVolume);
        if (_voiceOut != null) _voiceOut.Volume = (float)(_masterVolume * _voiceVolume);
    }

    #endregion

    #region BGM

    public void PlayBGM(string audioPath)
    {
        if (_currentBgmPath == audioPath && _bgmOut?.PlaybackState == PlaybackState.Playing)
            return;

        StopBGM();

        var fullPath = Path.Combine(_assetBasePath, audioPath);
        var stream = CreateReader(fullPath);
        if (stream == null) return;

        try
        {
            _bgmStream = stream;
            _bgmOut = new WaveOutEvent();
            _bgmOut.Volume = (float)(_masterVolume * _bgmVolume);
            _bgmOut.PlaybackStopped += (_, _) =>
            {
                // 循环播放
                if (_bgmStream != null && _bgmOut != null && !_disposed)
                {
                    try
                    {
                        _bgmStream.Position = 0;
                        _bgmOut.Play();
                    }
                    catch { /* 设备已释放 */ }
                }
            };
            _bgmOut.Init(stream);
            _bgmOut.Play();
            _currentBgmPath = audioPath;
        }
        catch (Exception ex)
        {
            DebugConsole.LogError("Audio-BGM", ex);
            stream.Dispose();
            _bgmOut?.Dispose();
            _bgmOut = null;
            _bgmStream = null;
        }
    }

    public void StopBGM()
    {
        _bgmOut?.Stop();
        _bgmOut?.Dispose();
        _bgmOut = null;
        _bgmStream?.Dispose();
        _bgmStream = null;
        _currentBgmPath = null;
    }

    public void PauseBGM() => _bgmOut?.Pause();
    public void ResumeBGM() => _bgmOut?.Play();

    #endregion

    #region SFX

    public void PlaySFX(string audioPath)
    {
        _sfxOut?.Stop();
        _sfxOut?.Dispose();
        _sfxOut = null;
        _sfxStream?.Dispose();
        _sfxStream = null;

        var fullPath = Path.Combine(_assetBasePath, audioPath);
        var stream = CreateReader(fullPath);
        if (stream == null) return;

        try
        {
            _sfxStream = stream;
            _sfxOut = new WaveOutEvent();
            _sfxOut.Volume = (float)(_masterVolume * _sfxVolume);
            _sfxOut.Init(stream);
            _sfxOut.Play();
        }
        catch (Exception ex)
        {
            DebugConsole.LogError("Audio-SFX", ex);
            stream.Dispose();
            _sfxOut?.Dispose();
            _sfxOut = null;
            _sfxStream = null;
        }
    }

    #endregion

    #region Voice

    public void PlayVoice(string audioPath)
    {
        StopVoice();

        var fullPath = Path.Combine(_assetBasePath, audioPath);
        var stream = CreateReader(fullPath);
        if (stream == null) return;

        try
        {
            _voiceStream = stream;
            _voiceOut = new WaveOutEvent();
            _voiceOut.Volume = (float)(_masterVolume * _voiceVolume);
            _voiceOut.PlaybackStopped += (_, _) =>
            {
                if (!_disposed && _voiceOut != null)
                    VoiceFinished?.Invoke();
            };
            _voiceOut.Init(stream);
            _voiceOut.Play();
        }
        catch (Exception ex)
        {
            DebugConsole.LogError("Audio-Voice", ex);
            stream.Dispose();
            _voiceOut?.Dispose();
            _voiceOut = null;
            _voiceStream = null;
        }
    }

    /// <summary>停止语音播放</summary>
    public void StopVoice()
    {
        _voiceOut?.Stop();
        _voiceOut?.Dispose();
        _voiceOut = null;
        _voiceStream?.Dispose();
        _voiceStream = null;
    }

    #endregion

    #region 停止全部

    public void StopAll()
    {
        StopBGM();

        _sfxOut?.Stop();
        _sfxOut?.Dispose();
        _sfxOut = null;
        _sfxStream?.Dispose();
        _sfxStream = null;

        _voiceOut?.Stop();
        _voiceOut?.Dispose();
        _voiceOut = null;
        _voiceStream?.Dispose();
        _voiceStream = null;
    }

    public void Dispose()
    {
        _disposed = true;
        StopAll();
    }

    #endregion
}
