using System.Globalization;
using System.Windows;
using NanoUint.Models;

namespace NanoUint.Services;

/// <summary>
/// Provides localization utilities for the visual novel engine.
/// The host application registers a language-switch callback via Initialize().
/// </summary>
public static class LocalizationService
{
    private static string _currentLanguage = "en-US";
    private static Action<string>? _switchCallback;

    /// <summary>Supported languages</summary>
    public static readonly Dictionary<string, string> SupportedLanguages = new()
    {
        { "zh-CN", "中文 (简体)" },
        { "en-US", "English" },
        { "ja-JP", "日本語" }
    };

    /// <summary>
    /// Called by the host application on startup to register the language
    /// and a callback for runtime language switching.
    /// </summary>
    public static void Initialize(string lang, Action<string>? switchCallback = null)
    {
        _currentLanguage = lang;
        _switchCallback = switchCallback;
    }

    /// <summary>Detect the system language and return the matching game language code</summary>
    public static string DetectSystemLanguage()
    {
        var culture = CultureInfo.CurrentCulture;
        return culture.TwoLetterISOLanguageName switch
        {
            "zh" => "zh-CN",
            "ja" => "ja-JP",
            _ => "en-US"
        };
    }

    /// <summary>Get the GameLanguage enum from a language code</summary>
    public static GameLanguage GetGameLanguage(string langCode) => langCode switch
    {
        "zh-CN" => GameLanguage.Chinese,
        "ja-JP" => GameLanguage.Japanese,
        _ => GameLanguage.English
    };

    /// <summary>Get a localized string from the application resources</summary>
    public static string GetString(string key)
    {
        var found = Application.Current.TryFindResource(key);
        return found as string ?? $"[{key}]";
    }

    /// <summary>Switch the application language at runtime</summary>
    public static void SwitchLanguage(string langCode)
    {
        _currentLanguage = langCode;
        _switchCallback?.Invoke(langCode);
    }

    /// <summary>Get the currently active language code</summary>
    public static string GetCurrentLanguage() => _currentLanguage;

    /// <summary>Format a string with localized parameters</summary>
    public static string Format(string key, params object[] args)
    {
        var template = GetString(key);
        return string.Format(template, args);
    }
}
