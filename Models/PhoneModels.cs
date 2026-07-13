using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace NanoUint.Models;

/// <summary>手机消息的方向</summary>
public enum MessageDirection
{
    Incoming,
    Outgoing
}

/// <summary>通话记录的类型</summary>
public enum CallType
{
    Incoming,
    Outgoing,
    Missed
}

/// <summary>手机通知的类型</summary>
public enum NotificationType
{
    Message,
    Call
}

/// <summary>一条文本消息</summary>
public class PhoneMessage
{
    public string SenderName { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsRead { get; set; }
    public bool IsOutgoing { get; set; }
    public MessageDirection Direction => IsOutgoing ? MessageDirection.Outgoing : MessageDirection.Incoming;
}

/// <summary>按联系人分组的对话线程</summary>
public class PhoneConversation : INotifyPropertyChanged
{
    private string _contactName = "";
    private int _unreadCount;

    public string ContactName
    {
        get => _contactName;
        set { _contactName = value; OnPropertyChanged(); }
    }

    public ObservableCollection<PhoneMessage> Messages { get; set; } = new();
    public DateTime LastActivity { get; set; } = DateTime.Now;

    public int UnreadCount
    {
        get => _unreadCount;
        set { _unreadCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnread)); }
    }

    public bool HasUnread => UnreadCount > 0;

    public string LastMessagePreview =>
        Messages.Count > 0 ? Messages[^1].Text : "";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>历史记录中的一条通话记录</summary>
public class CallEntry
{
    public string CallerName { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public CallType Type { get; set; } = CallType.Incoming;
    public TimeSpan Duration { get; set; }
    public bool IsActive { get; set; }

    public string DurationDisplay => Duration.TotalSeconds > 0
        ? $"{Duration.Minutes:D2}:{Duration.Seconds:D2}"
        : "--:--";

    public string TypeIcon => Type switch
    {
        CallType.Incoming => "⬇",  // 来电
        CallType.Outgoing => "⬆",  // 拨出
        CallType.Missed => "✖",    // 未接
        _ => ""
    };
}

/// <summary>手机相关设置，支持持久化</summary>
public class PhoneSettings : INotifyPropertyChanged
{
    private string _ringtonePath = "Default";
    private string _wallpaperPath = "Dark";
    private string _deviceName = "Liminal Phone";
    private bool _vibrationEnabled = true;
    private double _ringtoneVolume = 0.8;

    public string RingtonePath
    {
        get => _ringtonePath;
        set { _ringtonePath = value; OnPropertyChanged(); }
    }

    public string WallpaperPath
    {
        get => _wallpaperPath;
        set { _wallpaperPath = value; OnPropertyChanged(); }
    }

    public string DeviceName
    {
        get => _deviceName;
        set { _deviceName = value; OnPropertyChanged(); }
    }

    public bool VibrationEnabled
    {
        get => _vibrationEnabled;
        set { _vibrationEnabled = value; OnPropertyChanged(); }
    }

    public double RingtoneVolume
    {
        get => _ringtoneVolume;
        set { _ringtoneVolume = Math.Clamp(value, 0, 1); OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static string ConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                     "Liminal", "phone_settings.json");

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        catch { }
    }

    public static PhoneSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return JsonConvert.DeserializeObject<PhoneSettings>(File.ReadAllText(ConfigPath)) ?? new();
        }
        catch { }
        return new PhoneSettings();
    }
}

/// <summary>在主游戏窗口中显示的 Toast 通知</summary>
public class PhoneNotification
{
    public NotificationType Type { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string IconText => Type == NotificationType.Message ? "✉" : "☎";  // 消息 / 电话
    public Action? OnDismiss { get; set; }
    public Action? OnTap { get; set; }
}
