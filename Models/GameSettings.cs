using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace NanoUint.Models;

/// <summary>
/// 应用程序设置，具有属性变更通知，支持实时绑定。
/// </summary>
public class GameSettings : INotifyPropertyChanged
{
    private double _masterVolume = 0.8;
    private double _bgmVolume = 0.8;
    private double _sfxVolume = 0.8;
    private double _voiceVolume = 1.0;
    private int _resolutionWidth = 1280;
    private int _resolutionHeight = 720;
    private bool _isFullscreen;
    private bool _vSyncEnabled = true;
    private bool _showFps;
    private double _textSpeed = 0.05;
    private bool _autoAdvance;
    private double _autoAdvanceDelay = 2.0;

    public double MasterVolume
    {
        get => _masterVolume;
        set { _masterVolume = Math.Clamp(value, 0, 1); OnPropertyChanged(); }
    }

    public double BGMVolume
    {
        get => _bgmVolume;
        set { _bgmVolume = Math.Clamp(value, 0, 1); OnPropertyChanged(); }
    }

    public double SFXVolume
    {
        get => _sfxVolume;
        set { _sfxVolume = Math.Clamp(value, 0, 1); OnPropertyChanged(); }
    }

    public double VoiceVolume
    {
        get => _voiceVolume;
        set { _voiceVolume = Math.Clamp(value, 0, 1); OnPropertyChanged(); }
    }

    public int ResolutionWidth
    {
        get => _resolutionWidth;
        set { _resolutionWidth = value; OnPropertyChanged(); }
    }

    public int ResolutionHeight
    {
        get => _resolutionHeight;
        set { _resolutionHeight = value; OnPropertyChanged(); }
    }

    public bool IsFullscreen
    {
        get => _isFullscreen;
        set { _isFullscreen = value; OnPropertyChanged(); }
    }

    public bool VSyncEnabled
    {
        get => _vSyncEnabled;
        set { _vSyncEnabled = value; OnPropertyChanged(); }
    }

    public bool ShowFPS
    {
        get => _showFps;
        set { _showFps = value; OnPropertyChanged(); }
    }

    public double TextSpeed
    {
        get => _textSpeed;
        set { _textSpeed = Math.Clamp(value, 0.01, 0.5); OnPropertyChanged(); }
    }

    public bool AutoAdvance
    {
        get => _autoAdvance;
        set { _autoAdvance = value; OnPropertyChanged(); }
    }

    public double AutoAdvanceDelay
    {
        get => _autoAdvanceDelay;
        set { _autoAdvanceDelay = Math.Clamp(value, 0.5, 10); OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>创建设置的深拷贝</summary>
    public GameSettings Clone()
    {
        return new GameSettings
        {
            MasterVolume = _masterVolume,
            BGMVolume = _bgmVolume,
            SFXVolume = _sfxVolume,
            VoiceVolume = _voiceVolume,
            ResolutionWidth = _resolutionWidth,
            ResolutionHeight = _resolutionHeight,
            IsFullscreen = _isFullscreen,
            VSyncEnabled = _vSyncEnabled,
            ShowFPS = _showFps,
            TextSpeed = _textSpeed,
            AutoAdvance = _autoAdvance,
            AutoAdvanceDelay = _autoAdvanceDelay
        };
    }

    #region 持久化

    private static string ConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                     "FallenAltair", "settings.json");

    /// <summary>将设置保存到磁盘</summary>
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // 静默失败 —— 设置持久化不是关键功能
        }
    }

    /// <summary>从磁盘加载设置，如果不可用则返回默认值</summary>
    public static GameSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonConvert.DeserializeObject<GameSettings>(json);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // 文件损坏或丢失 —— 返回默认值
        }
        return new GameSettings();
    }

    #endregion
}
