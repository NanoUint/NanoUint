using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json;

namespace NanoUint;

#region 手机数据模型（纯 C#）

public enum MessageDirection { Incoming, Outgoing }
public enum CallType { Incoming, Outgoing, Missed }

public class PhoneMessage
{
    public string SenderName { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsRead { get; set; }
    public bool IsOutgoing { get; set; }
    public MessageDirection Direction => IsOutgoing ? MessageDirection.Outgoing : MessageDirection.Incoming;
}

public class PhoneConversation
{
    public string ContactName { get; set; } = "";
    public ObservableCollection<PhoneMessage> Messages { get; set; } = new();
    public DateTime LastActivity { get; set; }
    public int UnreadCount => Messages.Count(m => !m.IsRead && !m.IsOutgoing);
    public bool HasUnread => UnreadCount > 0;
    public string LastMessagePreview => Messages.LastOrDefault()?.Text ?? "";
}

public class CallEntry
{
    public string CallerName { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public CallType Type { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsActive { get; set; }
    public string DurationDisplay => Duration.TotalSeconds > 0
        ? $"{(int)Duration.TotalMinutes}:{Duration.Seconds:D2}" : "";
    public string TypeIcon => Type switch
    {
        CallType.Incoming => "↓",
        CallType.Outgoing => "↑",
        CallType.Missed => "✗",
        _ => ""
    };
}

public class PhoneNotification
{
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string IconText { get; set; } = "";
    public Action? OnDismiss { get; set; }
    public Action? OnTap { get; set; }
}

public class PhoneSettings
{
    public string RingtonePath { get; set; } = "";
    public string WallpaperPath { get; set; } = "";
    public string DeviceName { get; set; } = "NanoPhone";
    public bool VibrationEnabled { get; set; } = true;
    public float RingtoneVolume { get; set; } = 0.7f;
}

/// <summary>手机服务。纯逻辑、无 UI 依赖，游戏端用它搭建手机 UI。</summary>
public static class PhoneService
{
    private static readonly ObservableCollection<PhoneConversation> _conversations = new();
    private static readonly ObservableCollection<CallEntry> _callHistory = new();
    private static readonly PhoneSettings _settings = new();
    private static CallEntry? _activeCall;
    private static CallEntry? _ringingCall;

    public static PhoneSettings Settings => _settings;
    public static bool HasRingingCall => _ringingCall != null;
    public static bool IsInCall => _activeCall != null;
    public static string? ActiveCallerName => _activeCall?.CallerName;

    public static event Action<PhoneNotification>? NotificationReceived;

    #region 消息

    public static void ReceiveMessage(string sender, string text)
    {
        var conv = GetOrCreateConversation(sender);
        var msg = new PhoneMessage
        {
            SenderName = sender, Text = text, Timestamp = DateTime.Now,
            IsRead = false, IsOutgoing = false,
        };
        conv.Messages.Add(msg);
        conv.LastActivity = DateTime.Now;

        NotificationReceived?.Invoke(new PhoneNotification
        {
            Type = "Message", Title = sender,
            Body = text.Length > 40 ? text[..40] + "..." : text, IconText = "✉",
        });
    }

    public static void SendMessage(string contact, string text)
    {
        var conv = GetOrCreateConversation(contact);
        var msg = new PhoneMessage
        {
            SenderName = "You", Text = text, Timestamp = DateTime.Now,
            IsRead = true, IsOutgoing = true,
        };
        conv.Messages.Add(msg);
        conv.LastActivity = DateTime.Now;
    }

    #endregion

    #region 通话

    public static CallEntry ReceiveCall(string caller)
    {
        var entry = new CallEntry
        {
            CallerName = caller, Timestamp = DateTime.Now,
            Type = CallType.Incoming, IsActive = true,
        };
        _ringingCall = entry;
        NotificationReceived?.Invoke(new PhoneNotification
        {
            Type = "Call", Title = "Incoming Call", Body = caller, IconText = "📞",
        });
        return entry;
    }

    public static CallEntry DialCall(string number)
    {
        var entry = new CallEntry
        {
            CallerName = number, Timestamp = DateTime.Now,
            Type = CallType.Outgoing, IsActive = true,
        };
        _activeCall = entry;
        _callHistory.Add(entry);
        return entry;
    }

    public static void AcceptCall()
    {
        if (_ringingCall == null) return;
        _activeCall = _ringingCall;
        _ringingCall = null;
        _activeCall.IsActive = true;
        _callHistory.Add(_activeCall);
    }

    public static void DeclineCall()
    {
        if (_ringingCall != null)
        {
            _ringingCall.Type = CallType.Missed;
            _ringingCall.IsActive = false;
            _callHistory.Add(_ringingCall);
            _ringingCall = null;
        }
    }

    public static void EndCall()
    {
        if (_activeCall != null)
        {
            _activeCall.Duration = DateTime.Now - _activeCall.Timestamp;
            _activeCall.IsActive = false;
            _activeCall = null;
        }
    }

    #endregion

    #region 查询

    public static ObservableCollection<PhoneConversation> GetConversations() => _conversations;
    public static List<PhoneMessage> GetMessages(string contactName)
        => _conversations.FirstOrDefault(c => c.ContactName == contactName)?.Messages.ToList() ?? new();
    public static ObservableCollection<CallEntry> GetCallHistory() => _callHistory;
    public static void MarkConversationRead(string contactName)
    {
        var conv = _conversations.FirstOrDefault(c => c.ContactName == contactName);
        if (conv != null)
            foreach (var msg in conv.Messages.Where(m => !m.IsOutgoing))
                msg.IsRead = true;
    }

    #endregion

    #region 内部

    private static PhoneConversation GetOrCreateConversation(string contactName)
    {
        var conv = _conversations.FirstOrDefault(c => c.ContactName == contactName);
        if (conv == null)
        {
            conv = new PhoneConversation { ContactName = contactName };
            _conversations.Add(conv);
        }
        return conv;
    }

    #endregion

    #region 持久化

    private static readonly string _savePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "NanoUint", "phone_data.json");

    public static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_savePath);
            if (dir != null) Directory.CreateDirectory(dir);

            var data = new PhoneSaveData
            {
                Conversations = _conversations.Select(c => new ConversationData
                {
                    ContactName = c.ContactName,
                    Messages = c.Messages.Select(m => new MessageData
                    {
                        SenderName = m.SenderName, Text = m.Text,
                        Timestamp = m.Timestamp, IsRead = m.IsRead, IsOutgoing = m.IsOutgoing,
                    }).ToList(),
                }).ToList(),
                CallHistory = _callHistory.Select(c => new CallData
                {
                    CallerName = c.CallerName, Timestamp = c.Timestamp,
                    Type = c.Type, Duration = c.Duration,
                }).ToList(),
            };

            var tmp = _savePath + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(data, Formatting.Indented));
            var bak = _savePath + ".bak";
            if (File.Exists(bak)) File.Delete(bak);
            if (File.Exists(_savePath)) File.Move(_savePath, bak);
            File.Move(tmp, _savePath);
            Debug.Log("PhoneService: Data saved.");
        }
        catch (Exception ex) { Debug.LogError($"PhoneService.Save failed: {ex.Message}"); }
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(_savePath)) return;
            var data = JsonConvert.DeserializeObject<PhoneSaveData>(File.ReadAllText(_savePath));
            if (data == null) return;

            _conversations.Clear();
            foreach (var cd in data.Conversations)
            {
                var conv = new PhoneConversation { ContactName = cd.ContactName };
                foreach (var md in cd.Messages)
                    conv.Messages.Add(new PhoneMessage
                    {
                        SenderName = md.SenderName, Text = md.Text,
                        Timestamp = md.Timestamp, IsRead = md.IsRead, IsOutgoing = md.IsOutgoing,
                    });
                conv.LastActivity = conv.Messages.LastOrDefault()?.Timestamp ?? DateTime.MinValue;
                _conversations.Add(conv);
            }

            _callHistory.Clear();
            foreach (var cd in data.CallHistory)
                _callHistory.Add(new CallEntry
                {
                    CallerName = cd.CallerName, Timestamp = cd.Timestamp,
                    Type = cd.Type, Duration = cd.Duration,
                });
            Debug.Log("PhoneService: Data loaded.");
        }
        catch (Exception ex) { Debug.LogError($"PhoneService.Load failed: {ex.Message}"); }
    }

    private class PhoneSaveData
    {
        public List<ConversationData> Conversations { get; set; } = new();
        public List<CallData> CallHistory { get; set; } = new();
    }
    private class ConversationData { public string ContactName { get; set; } = ""; public List<MessageData> Messages { get; set; } = new(); }
    private class MessageData { public string SenderName { get; set; } = ""; public string Text { get; set; } = ""; public DateTime Timestamp { get; set; } public bool IsRead { get; set; } public bool IsOutgoing { get; set; } }
    private class CallData { public string CallerName { get; set; } = ""; public DateTime Timestamp { get; set; } public CallType Type { get; set; } public TimeSpan Duration { get; set; } }
    #endregion
}

#endregion