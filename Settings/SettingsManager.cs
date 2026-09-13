using System.IO;
using Newtonsoft.Json;

namespace NanoUint;

/// <summary>Persists and exposes all game settings.</summary>
public static class SettingsManager
{
    private static SettingsData _data = new();
    private static readonly string SavePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "NanoUint", "settings.json");

    #region Audio
    public static float MasterVolume
    {
        get => _data.MasterVolume;
        set => SetProperty(nameof(MasterVolume), value, v => _data.MasterVolume = Math.Clamp(v, 0f, 1f));
    }
    public static float BGMVolume
    {
        get => _data.BGMVolume;
        set => SetProperty(nameof(BGMVolume), value, v => _data.BGMVolume = Math.Clamp(v, 0f, 1f));
    }
    public static float SFXVolume
    {
        get => _data.SFXVolume;
        set => SetProperty(nameof(SFXVolume), value, v => _data.SFXVolume = Math.Clamp(v, 0f, 1f));
    }
    public static float VoiceVolume
    {
        get => _data.VoiceVolume;
        set => SetProperty(nameof(VoiceVolume), value, v => _data.VoiceVolume = Math.Clamp(v, 0f, 1f));
    }

    #endregion

    #region Display
    public static int ResolutionWidth
    {
        get => _data.ResolutionWidth;
        set => SetProperty(nameof(ResolutionWidth), value, v => _data.ResolutionWidth = Math.Max(800, v));
    }
    public static int ResolutionHeight
    {
        get => _data.ResolutionHeight;
        set => SetProperty(nameof(ResolutionHeight), value, v => _data.ResolutionHeight = Math.Max(600, v));
    }
    public static bool IsFullscreen
    {
        get => _data.IsFullscreen;
        set => SetProperty(nameof(IsFullscreen), value, v => _data.IsFullscreen = v);
    }
    public static bool VSyncEnabled
    {
        get => _data.VSyncEnabled;
        set => SetProperty(nameof(VSyncEnabled), value, v => _data.VSyncEnabled = v);
    }
    public static bool ShowFPS
    {
        get => _data.ShowFPS;
        set => SetProperty(nameof(ShowFPS), value, v => _data.ShowFPS = v);
    }

    #endregion

    #region Gameplay
    public static float TextSpeed
    {
        get => _data.TextSpeed;
        set => SetProperty(nameof(TextSpeed), value, v => _data.TextSpeed = Math.Clamp(v, 0.01f, 0.5f));
    }
    public static bool AutoAdvance
    {
        get => _data.AutoAdvance;
        set => SetProperty(nameof(AutoAdvance), value, v => _data.AutoAdvance = v);
    }
    public static float AutoAdvanceDelay
    {
        get => _data.AutoAdvanceDelay;
        set => SetProperty(nameof(AutoAdvanceDelay), value, v => _data.AutoAdvanceDelay = Math.Clamp(v, 0.5f, 10f));
    }

    #endregion

    #region Language
    public static string Language
    {
        get => _data.Language;
        set => SetProperty(nameof(Language), value, v => _data.Language = v);
    }

    #endregion

    #region Skip Mode
    /// <summary>Skip mode: false = skip read text only, true = skip everything.</summary>
    public static bool SkipAll
    {
        get => _data.SkipAll;
        set => SetProperty(nameof(SkipAll), value, v => _data.SkipAll = v);
    }

    #endregion

    #region Events
    public static event Action<string>? OnChanged;

    private static void SetProperty<T>(string name, T value, Action<T> setter)
    {
        setter(value);
        OnChanged?.Invoke(name);
    }

    #endregion

    #region Persistence

    public static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SavePath);
            if (dir != null) Directory.CreateDirectory(dir);

            // Atomic write: write .tmp first, then replace.
            var tmp = SavePath + ".tmp";
            var json = JsonConvert.SerializeObject(_data, Formatting.Indented);
            File.WriteAllText(tmp, json);

            var bak = SavePath + ".bak";
            if (File.Exists(bak)) File.Delete(bak);
            if (File.Exists(SavePath)) File.Move(SavePath, bak);
            File.Move(tmp, SavePath);

            Debug.Log("Settings saved.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save settings: {ex.Message}");
        }
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                var json = File.ReadAllText(SavePath);
                _data = JsonConvert.DeserializeObject<SettingsData>(json) ?? new SettingsData();
                Debug.Log("Settings loaded.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load settings: {ex.Message}. Using defaults.");
            _data = new SettingsData();
        }
    }

    public static void Reset()
    {
        _data = new SettingsData();
    }

    private sealed class SettingsData
    {
        public float MasterVolume { get; set; } = 1f;
        public float BGMVolume { get; set; } = 1f;
        public float SFXVolume { get; set; } = 1f;
        public float VoiceVolume { get; set; } = 1f;
        public int ResolutionWidth { get; set; } = 1280;
        public int ResolutionHeight { get; set; } = 720;
        public bool IsFullscreen { get; set; }
        public bool VSyncEnabled { get; set; } = true;
        public bool ShowFPS { get; set; }
        public float TextSpeed { get; set; } = 0.05f;
        public bool AutoAdvance { get; set; }
        public float AutoAdvanceDelay { get; set; } = 3f;
        public string Language { get; set; } = "zh-CN";
        public bool SkipAll { get; set; } // false = skip read only (default)
    }
    #endregion
}
