using System.Collections;

namespace NanoUint;

/// <summary>手机触发系统 UI 组件。管理智能手机状态机（Closed → Opening → Home → Denha/Rine/Settings）。</summary>
public sealed class PhoneScreen : Behaviour
{
    public enum State
    {
        Closed,
        Opening,           // 滑入动画播放中
        BlackScreen,       // 黑屏待机（等 Enter/点击解锁）
        Home,              // 主屏幕（壁纸 + App 图标）
        DenhaIncoming,     // 来电
        DenhaInCall,       // 通话中
        RineList,          // RINE 对话列表
        RineChat,          // RINE 聊天界面
        RineStamp,         // RINE 贴纸选择
        Settings,          // 设置
        SettingsWallpaper, // 壁纸选择
    }

    private State _state = State.Closed;

    #region 渲染配置（由游戏层初始化）

    public PhoneRenderConfig? Config { get; set; }

    #endregion

    #region 来电

    private string _callerName = "";
    private float _callTimer;
    private float _callTimeout;
    private Action? _onAnswer, _onIgnore;

    #endregion

    #region 邮件/回复（兼容旧 API）

    private string _mailSender = "", _mailSubject = "", _mailBody = "";
    private string[] _mailKeywords = Array.Empty<string>();
    private int _selectedKeyword = -1;
    private Action<int>? _onReplySelect;

    #endregion

    #region 导航状态

    private int _wallpaperIndex;
    private int _highlightedApp = -1;  // -1=无高亮, 0=Denha, 1=Rine, 2=Settings
    private string _activeRineContact = "";
    private string[] _availableStamps = Array.Empty<string>();
    private Action<int>? _onStampSelected;

    #endregion

    #region 属性

    public State CurrentState => _state;
    public string CallerName => _callerName;
    public float CallTimer => _callTimer;
    public string MailSender => _mailSender;
    public string MailSubject => _mailSubject;
    public string MailBody => _mailBody;
    public string[] MailKeywords => _mailKeywords;
    public int SelectedKeyword => _selectedKeyword;
    public bool IsVisible => _state != State.Closed;

    public int WallpaperIndex => _wallpaperIndex;
    public int HighlightedApp => _highlightedApp;
    public string ActiveRineContact => _activeRineContact;
    public string[] AvailableStamps => _availableStamps;

    #endregion

    #region 导航方法

    /// <summary>显示手机（滑入动画 → 黑屏待机）。</summary>
    public void ShowHome()
    {
        if (_state != State.Closed) return;
        _state = State.Opening;
        _highlightedApp = -1;
        _slideTimer = 0f;
        MarkDirty();
    }

    /// <summary>从黑屏唤醒到主屏幕。</summary>
    public void WakePhone()
    {
        if (_state != State.BlackScreen) return;
        _state = State.Home;
        MarkDirty();
    }

    /// <summary>切换手机显示（Closed → Opening, 否则 → Closed）。</summary>
    public void Toggle()
    {
        if (_state == State.Closed) ShowHome();
        else Close();
    }

    /// <summary>内部使用：让渲染器推进动画状态。</summary>
    public void SetState(State s)
    {
        if (_state == s) return;
        _state = s;
        MarkDirty();
    }

    /// <summary>滑入动画计时器（秒），渲染器读写。</summary>
    public float SlideTimer
    {
        get => _slideTimer;
        set { _slideTimer = value; MarkDirty(); }
    }
    private float _slideTimer;

    /// <summary>打开电话 App。</summary>
    public void OpenDenha()
    {
        _state = State.DenhaIncoming;
        MarkDirty();
    }

    /// <summary>打开 RINE 对话列表。</summary>
    public void OpenRine()
    {
        _state = State.RineList;
        MarkDirty();
    }

    /// <summary>打开与某人的 RINE 聊天。</summary>
    public void OpenRineChat(string contact)
    {
        _state = State.RineChat;
        _activeRineContact = contact;
        MarkDirty();
    }

    /// <summary>打开 RINE 贴纸选择器。</summary>
    public void OpenRineStampPicker(string[] stamps, Action<int> onSelect)
    {
        _state = State.RineStamp;
        _availableStamps = stamps;
        _onStampSelected = onSelect;
        MarkDirty();
    }

    /// <summary>选择贴纸。</summary>
    public void SelectStamp(int index)
    {
        if (_state != State.RineStamp || index < 0 || index >= _availableStamps.Length) return;
        _state = State.RineChat;
        MarkDirty();
        _onStampSelected?.Invoke(index);
    }

    /// <summary>打开设置。</summary>
    public void OpenSettings()
    {
        _state = State.Settings;
        MarkDirty();
    }

    /// <summary>打开壁纸选择。</summary>
    public void OpenWallpaperPicker()
    {
        _state = State.SettingsWallpaper;
        MarkDirty();
    }

    /// <summary>选择壁纸。</summary>
    public void SelectWallpaper(int index)
    {
        _wallpaperIndex = Math.Clamp(index, 0, 11);
        MarkDirty();
    }

    /// <summary>循环壁纸（左右切换）。</summary>
    public void CycleWallpaper(int direction)
    {
        _wallpaperIndex = (_wallpaperIndex + direction + 12) % 12;
        MarkDirty();
    }

    /// <summary>设置 App 高亮。</summary>
    public void SetAppHighlight(int appIndex)
    {
        _highlightedApp = appIndex;
        MarkDirty();
    }

    #endregion

    #region 来电（保持兼容旧 API）

    public void TriggerCall(string caller, float timeoutSec, Action onAnswer, Action onIgnore)
    {
        _state = State.DenhaIncoming;
        _callerName = caller;
        _callTimer = timeoutSec;
        _callTimeout = timeoutSec;
        _onAnswer = onAnswer;
        _onIgnore = onIgnore;
        MarkDirty();
    }

    public void AnswerCall()
    {
        if (_state != State.DenhaIncoming) return;
        _state = State.DenhaInCall;
        _callTimer = 0;
        MarkDirty();
        _onAnswer?.Invoke();
    }

    public void DeclineCall()
    {
        if (_state != State.DenhaIncoming && _state != State.DenhaInCall) return;
        _state = State.Home;
        MarkDirty();
        _onIgnore?.Invoke();
    }

    /// <summary>结束通话。</summary>
    public void EndCall()
    {
        if (_state != State.DenhaInCall) return;
        _state = State.Home;
        MarkDirty();
    }

    #endregion

    #region 邮件（兼容旧 API → 映射到 RINE）

    public void OpenMailbox()
    {
        _state = State.RineList;
        MarkDirty();
    }

    public void OpenMail(string sender, string subject, string body, string[] keywords)
    {
        _state = State.RineChat;
        _activeRineContact = sender;
        _mailSender = sender;
        _mailSubject = subject;
        _mailBody = body;
        _mailKeywords = keywords;
        _selectedKeyword = -1;
        MarkDirty();
    }

    public void ShowReplyOptions(string[] keywords, Action<int> onSelect)
    {
        _mailKeywords = keywords;
        _selectedKeyword = -1;
        _onReplySelect = onSelect;
        MarkDirty();
    }

    public void SelectKeyword(int index)
    {
        if (index < 0 || index >= _mailKeywords.Length) return;
        _selectedKeyword = index;
        _state = State.Home;
        MarkDirty();
        _onReplySelect?.Invoke(index);
    }

    #endregion

    #region 关闭

    public void Close()
    {
        _state = State.Closed;
        _slideTimer = 0f;
        MarkDirty();
    }

    /// <summary>返回主屏幕（从任意子界面）。</summary>
    public void BackToHome()
    {
        _state = State.Home;
        _highlightedApp = -1;
        MarkDirty();
    }

    #endregion

    #region 生命周期

    protected internal override void Update(float deltaTime)
    {
        if (_state == State.DenhaIncoming && _callTimer > 0)
        {
            _callTimer -= deltaTime;
            if (_callTimer <= 0)
            {
                _callTimer = 0;
                _state = State.Home;
                MarkDirty();
                _onIgnore?.Invoke();
            }
        }
        else if (_state == State.Opening)
        {
            _slideTimer += deltaTime;
            MarkDirty(); // 每帧触发渲染更新，推进动画帧
            var duration = (float)(Config?.SlideDurationSec ?? 0.6);
            if (_slideTimer >= duration)
            {
                _slideTimer = duration;
                _state = State.BlackScreen;
                MarkDirty();
            }
        }
    }

    protected internal override void OnDestroy()
    {
        _onAnswer = null; _onIgnore = null; _onReplySelect = null; _onStampSelected = null;
        base.OnDestroy();
    }

    public override string ToString() =>
        $"PhoneScreen (state={_state}, caller={_callerName}, timer={_callTimer:F1})";
    #endregion
}
