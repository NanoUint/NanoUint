using System.Collections.Concurrent;

namespace NanoUint;

/// <summary>
/// 多语言管理器。引擎内置，支持通过字符串 key 获取翻译文本。
/// 语言文件为内嵌的 XAML 资源字典，引擎在启动时自动加载。
/// </summary>
public static class LocalizationManager
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _locales = new();
    private static string _currentLanguage = "zh-CN";

    /// <summary>当前使用的语言代码（如 "zh-CN", "en-US", "ja-JP"）。</summary>
    public static string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value && _locales.ContainsKey(value))
            {
                _currentLanguage = value;
                SettingsManager.Language = value;
                OnLanguageChanged?.Invoke(value);
                Debug.Log($"Language changed to: {value}");
            }
        }
    }

    /// <summary>支持的语言列表。</summary>
    public static string[] SupportedLanguages => _locales.Keys.ToArray();

    /// <summary>语言切换时触发。</summary>
    public static event Action<string>? OnLanguageChanged;

    /// <summary>注册一个语言的数据字典。</summary>
    public static void RegisterLocale(string langCode, Dictionary<string, string> strings)
    {
        _locales[langCode] = new ConcurrentDictionary<string, string>(strings);
        Debug.Log($"Locale registered: {langCode} ({strings.Count} strings)");
    }

    /// <summary>获取翻译文本。</summary>
    public static string Get(string key)
    {
        if (_locales.TryGetValue(_currentLanguage, out var dict) &&
            dict.TryGetValue(key, out var value))
            return value;

        Debug.LogWarning($"Localization key not found: '{key}' for language '{_currentLanguage}'");
        return key; // 退回原始 key
    }

    /// <summary>获取格式化翻译文本。</summary>
    public static string Get(string key, params object[] args)
    {
        var template = Get(key);
        try { return string.Format(template, args); }
        catch { return template; }
    }

    /// <summary>加载引擎内嵌的默认多语言数据。</summary>
    public static void LoadEmbeddedLocales()
    {
        // 简体中文
        RegisterLocale("zh-CN", new Dictionary<string, string>
        {
            ["Menu.NewGame"] = "新游戏",
            ["Menu.Continue"] = "继续",
            ["Menu.LoadGame"] = "读取存档",
            ["Menu.Settings"] = "设置",
            ["Menu.Exit"] = "退出",
            ["Menu.Return"] = "返回标题",
            ["Common.Save"] = "保存",
            ["Common.Cancel"] = "取消",
            ["Common.OK"] = "确定",
            ["Common.Apply"] = "应用",
            ["Common.Close"] = "关闭",
            ["Common.Back"] = "返回",
            ["Common.Yes"] = "是",
            ["Common.No"] = "否",
            ["Settings.Title"] = "设置",
            ["Settings.Audio"] = "音频",
            ["Settings.MasterVolume"] = "主音量",
            ["Settings.BGMVolume"] = "背景音乐",
            ["Settings.SFXVolume"] = "音效",
            ["Settings.VoiceVolume"] = "语音",
            ["Settings.Display"] = "画面",
            ["Settings.Fullscreen"] = "全屏",
            ["Settings.VSync"] = "垂直同步",
            ["Settings.ShowFPS"] = "显示帧率",
            ["Settings.Gameplay"] = "游戏体验",
            ["Settings.TextSpeed"] = "文字速度",
            ["Settings.AutoAdvance"] = "自动推进",
            ["Settings.AutoAdvanceDelay"] = "自动推进延迟（秒）",
            ["Settings.Language"] = "语言",
            ["Save.Title"] = "存档",
            ["Save.LoadTitle"] = "读档",
            ["Save.EmptySlot"] = "空",
            ["Save.ConfirmOverwrite"] = "覆盖此存档？",
            ["Save.ConfirmDelete"] = "删除此存档？",
            ["Phone.Title"] = "手机",
            ["Phone.Messages"] = "消息",
            ["Phone.Calls"] = "通话",
            ["Phone.Contacts"] = "通讯录",
            ["Phone.Settings"] = "设置",
            ["Game.AutoMode"] = "自动",
            ["Game.SkipMode"] = "跳过",
            ["Game.Backlog"] = "记录",
            ["Game.Save"] = "存档",
            ["Game.Load"] = "读档",
        });

        // English
        RegisterLocale("en-US", new Dictionary<string, string>
        {
            ["Menu.NewGame"] = "New Game",
            ["Menu.Continue"] = "Continue",
            ["Menu.LoadGame"] = "Load Game",
            ["Menu.Settings"] = "Settings",
            ["Menu.Exit"] = "Exit",
            ["Menu.Return"] = "Return to Title",
            ["Common.Save"] = "Save",
            ["Common.Cancel"] = "Cancel",
            ["Common.OK"] = "OK",
            ["Common.Apply"] = "Apply",
            ["Common.Close"] = "Close",
            ["Common.Back"] = "Back",
            ["Common.Yes"] = "Yes",
            ["Common.No"] = "No",
            ["Settings.Title"] = "Settings",
            ["Settings.Audio"] = "Audio",
            ["Settings.MasterVolume"] = "Master Volume",
            ["Settings.BGMVolume"] = "BGM Volume",
            ["Settings.SFXVolume"] = "SFX Volume",
            ["Settings.VoiceVolume"] = "Voice Volume",
            ["Settings.Display"] = "Display",
            ["Settings.Fullscreen"] = "Fullscreen",
            ["Settings.VSync"] = "VSync",
            ["Settings.ShowFPS"] = "Show FPS",
            ["Settings.Gameplay"] = "Gameplay",
            ["Settings.TextSpeed"] = "Text Speed",
            ["Settings.AutoAdvance"] = "Auto Advance",
            ["Settings.AutoAdvanceDelay"] = "Auto Advance Delay (s)",
            ["Settings.Language"] = "Language",
            ["Save.Title"] = "Save",
            ["Save.LoadTitle"] = "Load",
            ["Save.EmptySlot"] = "Empty",
            ["Save.ConfirmOverwrite"] = "Overwrite this save?",
            ["Save.ConfirmDelete"] = "Delete this save?",
            ["Phone.Title"] = "Phone",
            ["Phone.Messages"] = "Messages",
            ["Phone.Calls"] = "Calls",
            ["Phone.Contacts"] = "Contacts",
            ["Phone.Settings"] = "Settings",
            ["Game.AutoMode"] = "Auto",
            ["Game.SkipMode"] = "Skip",
            ["Game.Backlog"] = "Backlog",
            ["Game.Save"] = "Save",
            ["Game.Load"] = "Load",
        });

        // 日本語
        RegisterLocale("ja-JP", new Dictionary<string, string>
        {
            ["Menu.NewGame"] = "はじめから",
            ["Menu.Continue"] = "つづきから",
            ["Menu.LoadGame"] = "ロード",
            ["Menu.Settings"] = "設定",
            ["Menu.Exit"] = "終了",
            ["Menu.Return"] = "タイトルに戻る",
            ["Common.Save"] = "保存",
            ["Common.Cancel"] = "キャンセル",
            ["Common.OK"] = "OK",
            ["Common.Apply"] = "適用",
            ["Common.Close"] = "閉じる",
            ["Common.Back"] = "戻る",
            ["Common.Yes"] = "はい",
            ["Common.No"] = "いいえ",
            ["Settings.Title"] = "設定",
            ["Settings.Audio"] = "オーディオ",
            ["Settings.MasterVolume"] = "マスター音量",
            ["Settings.BGMVolume"] = "BGM 音量",
            ["Settings.SFXVolume"] = "効果音",
            ["Settings.VoiceVolume"] = "ボイス",
            ["Settings.Display"] = "画面",
            ["Settings.Fullscreen"] = "フルスクリーン",
            ["Settings.VSync"] = "垂直同期",
            ["Settings.ShowFPS"] = "FPS 表示",
            ["Settings.Gameplay"] = "ゲームプレイ",
            ["Settings.TextSpeed"] = "文字速度",
            ["Settings.AutoAdvance"] = "自動送り",
            ["Settings.AutoAdvanceDelay"] = "自動送り遅延 (秒)",
            ["Settings.Language"] = "言語",
            ["Save.Title"] = "セーブ",
            ["Save.LoadTitle"] = "ロード",
            ["Save.EmptySlot"] = "空",
            ["Phone.Title"] = "携帯",
            ["Phone.Messages"] = "メッセージ",
            ["Phone.Calls"] = "通話",
            ["Phone.Contacts"] = "連絡先",
            ["Phone.Settings"] = "設定",
            ["Game.AutoMode"] = "オート",
            ["Game.SkipMode"] = "スキップ",
            ["Game.Backlog"] = "バックログ",
            ["Game.Save"] = "セーブ",
            ["Game.Load"] = "ロード",
        });
    }
}
