using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using NanoUint.Models;
using Newtonsoft.Json;

namespace NanoUint.Services;

/// <summary>
/// 管理中心服务，管理所有手机状态：消息、通话、设置和通知。
/// 位于 NanoUint 中，以便 Liminal 和脚本引擎都能访问它。
/// </summary>
public class PhoneService
{
    private readonly ObservableCollection<PhoneConversation> _conversations = new();
    private readonly ObservableCollection<CallEntry> _callHistory = new();
    private PhoneSettings _settings;
    private PhoneConversation? _activeCallConversation;

    /// <summary>当需要在游戏窗口显示通知时触发</summary>
    public event Action<PhoneNotification>? NotificationReceived;

    /// <summary>当需要清除所有通知时触发</summary>
    public event Action? NotificationsCleared;

    public PhoneSettings Settings => _settings;

    public PhoneService()
    {
        _settings = PhoneSettings.Load();
        LoadPhoneData();
    }

    #region 消息

    /// <summary>接收来自联系人的消息</summary>
    public void ReceiveMessage(string senderName, string messageText)
    {
        var conv = GetOrCreateConversation(senderName);
        conv.Messages.Add(new PhoneMessage
        {
            SenderName = senderName,
            Text = messageText,
            Timestamp = DateTime.Now,
            IsRead = false,
            IsOutgoing = false
        });
        conv.UnreadCount++;
        conv.LastActivity = DateTime.Now;

        // 重新排序到列表顶部（最新的在最前面）
        RepositionConversation(conv);

        var preview = messageText.Length > 40 ? messageText[..40] + "..." : messageText;
        NotificationReceived?.Invoke(new PhoneNotification
        {
            Type = NotificationType.Message,
            Title = senderName,
            Body = preview
        });
    }

    /// <summary>从玩家发送一条消息</summary>
    public void SendMessage(string contactName, string messageText)
    {
        var conv = GetOrCreateConversation(contactName);
        conv.Messages.Add(new PhoneMessage
        {
            SenderName = "玩家",
            Text = messageText,
            Timestamp = DateTime.Now,
            IsRead = true,
            IsOutgoing = true
        });
        conv.LastActivity = DateTime.Now;
        RepositionConversation(conv);
    }

    #endregion

    #region 通话

    /// <summary>接听来电</summary>
    public CallEntry ReceiveCall(string callerName)
    {
        _activeCallConversation = GetOrCreateConversation(callerName);

        var entry = new CallEntry
        {
            CallerName = callerName,
            Timestamp = DateTime.Now,
            Type = CallType.Incoming,
            IsActive = true
        };
        _callHistory.Insert(0, entry);

        // 如果可用则播放铃声 SFX
        if (_settings.RingtoneVolume > 0)
        {
            // 音频服务会在此处播放铃声
        }

        NotificationReceived?.Invoke(new PhoneNotification
        {
            Type = NotificationType.Call,
            Title = "来电",
            Body = callerName
        });

        return entry;
    }

    /// <summary>发起拨出电话</summary>
    public CallEntry DialCall(string numberOrName)
    {
        var entry = new CallEntry
        {
            CallerName = numberOrName,
            Timestamp = DateTime.Now,
            Type = CallType.Outgoing,
            IsActive = true
        };
        _callHistory.Insert(0, entry);
        _activeCallConversation = GetOrCreateConversation(numberOrName);
        return entry;
    }

    /// <summary>接听当前来电</summary>
    public void AcceptCall()
    {
        var call = _callHistory.FirstOrDefault(c => c.IsActive && c.Type == CallType.Incoming);
        if (call == null) return;

        call.IsActive = true;
        NotificationsCleared?.Invoke();
    }

    /// <summary>拒接当前来电</summary>
    public void DeclineCall()
    {
        var call = _callHistory.FirstOrDefault(c => c.IsActive && c.Type == CallType.Incoming);
        if (call == null) return;

        call.Type = CallType.Missed;
        call.IsActive = false;
        _activeCallConversation = null;
        NotificationsCleared?.Invoke();
    }

    /// <summary>结束当前通话</summary>
    public void EndCall()
    {
        var call = _callHistory.FirstOrDefault(c => c.IsActive);
        if (call == null) return;

        call.Duration = DateTime.Now - call.Timestamp;
        call.IsActive = false;
        _activeCallConversation = null;
    }

    /// <summary>检查是否有等待接听的来电</summary>
    public bool HasRingingCall =>
        _callHistory.Any(c => c.IsActive && c.Type == CallType.Incoming);

    /// <summary>检查当前是否正在通话中</summary>
    public bool IsInCall =>
        _callHistory.Any(c => c.IsActive && c.Type != CallType.Missed);

    /// <summary>获取当前通话的来电者名称</summary>
    public string? ActiveCallerName =>
        _callHistory.FirstOrDefault(c => c.IsActive)?.CallerName;

    #endregion

    #region 访问器

    public ObservableCollection<PhoneConversation> GetConversations()
    {
        // 返回按最新排序的列表
        return _conversations;
    }

    public ObservableCollection<PhoneMessage>? GetMessagesForContact(string contactName)
    {
        return _conversations.FirstOrDefault(c =>
            string.Equals(c.ContactName, contactName, StringComparison.OrdinalIgnoreCase))?.Messages;
    }

    public void MarkConversationRead(string contactName)
    {
        var conv = _conversations.FirstOrDefault(c =>
            string.Equals(c.ContactName, contactName, StringComparison.OrdinalIgnoreCase));
        if (conv != null)
        {
            conv.UnreadCount = 0;
            foreach (var msg in conv.Messages)
                msg.IsRead = true;
        }
    }

    public ObservableCollection<CallEntry> GetCallHistory() => _callHistory;

    #endregion

    #region 持久化

    private static string PhoneDataPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                     "FallenAltair", "phone_data.json");

    private void SavePhoneData()
    {
        try
        {
            var dir = Path.GetDirectoryName(PhoneDataPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var data = new PhoneSaveData
            {
                Conversations = _conversations.Select(c => new ConversationData
                {
                    ContactName = c.ContactName,
                    Messages = c.Messages.Select(m => new MessageData
                    {
                        SenderName = m.SenderName,
                        Text = m.Text,
                        Timestamp = m.Timestamp,
                        IsRead = m.IsRead,
                        IsOutgoing = m.IsOutgoing
                    }).ToList(),
                    LastActivity = c.LastActivity
                }).ToList(),
                CallHistory = _callHistory.Select(c => new CallData
                {
                    CallerName = c.CallerName,
                    Timestamp = c.Timestamp,
                    Type = (int)c.Type,
                    Duration = c.Duration,
                    IsActive = c.IsActive
                }).ToList()
            };

            File.WriteAllText(PhoneDataPath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }
        catch { }
    }

    private void LoadPhoneData()
    {
        try
        {
            if (!File.Exists(PhoneDataPath)) return;
            var data = JsonConvert.DeserializeObject<PhoneSaveData>(File.ReadAllText(PhoneDataPath));
            if (data == null) return;

            _conversations.Clear();
            foreach (var cd in data.Conversations)
            {
                var conv = new PhoneConversation
                {
                    ContactName = cd.ContactName,
                    LastActivity = cd.LastActivity
                };
                foreach (var md in cd.Messages)
                {
                    conv.Messages.Add(new PhoneMessage
                    {
                        SenderName = md.SenderName,
                        Text = md.Text,
                        Timestamp = md.Timestamp,
                        IsRead = md.IsRead,
                        IsOutgoing = md.IsOutgoing
                    });
                }
                conv.UnreadCount = conv.Messages.Count(m => !m.IsRead);
                _conversations.Add(conv);
            }

            _callHistory.Clear();
            foreach (var cd in data.CallHistory)
            {
                _callHistory.Add(new CallEntry
                {
                    CallerName = cd.CallerName,
                    Timestamp = cd.Timestamp,
                    Type = (CallType)cd.Type,
                    Duration = cd.Duration,
                    IsActive = cd.IsActive
                });
            }
        }
        catch { }
    }

    /// <summary>在应用退出/设置更改时保存手机数据</summary>
    public void Save()
    {
        _settings.Save();
        SavePhoneData();
    }

    #endregion

    #region 辅助方法

    private PhoneConversation GetOrCreateConversation(string contactName)
    {
        var conv = _conversations.FirstOrDefault(c =>
            string.Equals(c.ContactName, contactName, StringComparison.OrdinalIgnoreCase));
        if (conv == null)
        {
            conv = new PhoneConversation { ContactName = contactName };
            _conversations.Add(conv);
        }
        return conv;
    }

    private void RepositionConversation(PhoneConversation conv)
    {
        _conversations.Remove(conv);
        // 插入到位置 0（最新的在最前面）
        _conversations.Insert(0, conv);
    }

    // 内部序列化类型
    private class PhoneSaveData
    {
        public List<ConversationData> Conversations { get; set; } = new();
        public List<CallData> CallHistory { get; set; } = new();
    }

    private class ConversationData
    {
        public string ContactName { get; set; } = "";
        public List<MessageData> Messages { get; set; } = new();
        public DateTime LastActivity { get; set; }
    }

    private class MessageData
    {
        public string SenderName { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public bool IsOutgoing { get; set; }
    }

    private class CallData
    {
        public string CallerName { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public int Type { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsActive { get; set; }
    }

    #endregion
}
