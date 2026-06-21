using System.IO;
using System.Windows.Media;
using System.Windows.Threading;

namespace NanoUint.Services;

/// <summary>
/// Manages audio playback for BGM, SFX, and voice lines.
/// Uses WPF MediaPlayer for audio.
/// </summary>
public class AudioService : IDisposable
{
    private MediaPlayer? _bgmPlayer;
    private MediaPlayer? _sfxPlayer;
    private MediaPlayer? _voicePlayer;

    private double _masterVolume = 0.8;
    private double _bgmVolume = 0.8;
    private double _sfxVolume = 0.8;
    private double _voiceVolume = 1.0;

    private string? _currentBgmPath;
    private readonly string _assetBasePath;

    public AudioService(string assetBasePath = "Assets/Audio")
    {
        _assetBasePath = assetBasePath;
    }

    /// <summary>Set volume levels</summary>
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
        if (_bgmPlayer != null)
            _bgmPlayer.Volume = _masterVolume * _bgmVolume;
        if (_sfxPlayer != null)
            _sfxPlayer.Volume = _masterVolume * _sfxVolume;
        if (_voicePlayer != null)
            _voicePlayer.Volume = _masterVolume * _voiceVolume;
    }

    /// <summary>Play BGM (looping background music)</summary>
    public void PlayBGM(string audioPath)
    {
        if (_currentBgmPath == audioPath && _bgmPlayer != null)
            return;

        StopBGM();

        _bgmPlayer = new MediaPlayer();
        var fullPath = Path.Combine(_assetBasePath, audioPath);
        if (!File.Exists(fullPath)) return;

        _bgmPlayer.Open(new Uri(fullPath));
        _bgmPlayer.Volume = _masterVolume * _bgmVolume;
        _bgmPlayer.MediaEnded += (_, _) =>
        {
            // Loop BGM
            _bgmPlayer.Position = TimeSpan.Zero;
            _bgmPlayer.Play();
        };
        _bgmPlayer.Play();
        _currentBgmPath = audioPath;
    }

    /// <summary>Stop BGM playback</summary>
    public void StopBGM()
    {
        if (_bgmPlayer != null)
        {
            _bgmPlayer.Stop();
            _bgmPlayer.Close();
            _bgmPlayer = null;
        }
        _currentBgmPath = null;
    }

    /// <summary>Pause BGM (e.g., during menus)</summary>
    public void PauseBGM()
    {
        _bgmPlayer?.Pause();
    }

    /// <summary>Resume paused BGM</summary>
    public void ResumeBGM()
    {
        _bgmPlayer?.Play();
    }

    /// <summary>Play a one-shot sound effect</summary>
    public void PlaySFX(string audioPath)
    {
        _sfxPlayer?.Stop();
        _sfxPlayer?.Close();

        _sfxPlayer = new MediaPlayer();
        var fullPath = Path.Combine(_assetBasePath, audioPath);
        if (!File.Exists(fullPath)) return;

        _sfxPlayer.Open(new Uri(fullPath));
        _sfxPlayer.Volume = _masterVolume * _sfxVolume;
        _sfxPlayer.Play();
    }

    /// <summary>Play a voice line</summary>
    public void PlayVoice(string audioPath)
    {
        _voicePlayer?.Stop();
        _voicePlayer?.Close();

        _voicePlayer = new MediaPlayer();
        var fullPath = Path.Combine(_assetBasePath, audioPath);
        if (!File.Exists(fullPath)) return;

        _voicePlayer.Open(new Uri(fullPath));
        _voicePlayer.Volume = _masterVolume * _voiceVolume;
        _voicePlayer.Play();
    }

    /// <summary>Stop all audio</summary>
    public void StopAll()
    {
        StopBGM();
        _sfxPlayer?.Stop();
        _sfxPlayer?.Close();
        _sfxPlayer = null;
        _voicePlayer?.Stop();
        _voicePlayer?.Close();
        _voicePlayer = null;
    }

    public void Dispose()
    {
        StopAll();
    }
}
