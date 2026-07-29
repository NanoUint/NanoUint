using System.Collections;

namespace NanoUint;

/// <summary>
/// 手机触发系统 UI 组件。管理手机状态：待机/来电/邮件列表/邮件阅读/回复选择。
/// 独立于背景和立绘的浮层，有滑入/滑出动画。
/// 核心设计：来电有超时倒计时（不接=选择忽略），邮件关键词高亮回复。
/// </summary>
public sealed class PhoneScreen : Behaviour
{
    public enum State { Closed, IncomingCall, InCall, MailList, MailView, ReplySelect }

    private State _state = State.Closed;
    private string _callerName = "";
    private float _callTimer;       // 来电倒计时
    private float _callTimeout;
    private Action? _onAnswer, _onIgnore;

    private string _mailSender = "", _mailSubject = "", _mailBody = "";
    private string[] _mailKeywords = Array.Empty<string>();
    private int _selectedKeyword = -1;
    private Action<int>? _onReplySelect;

    /// <summary>当前手机状态。</summary>
    public State CurrentState => _state;

    /// <summary>来电者名称（来电时）。</summary>
    public string CallerName => _callerName;

    /// <summary>来电剩余时间（秒）。</summary>
    public float CallTimer => _callTimer;

    /// <summary>邮件发件人。</summary>
    public string MailSender => _mailSender;

    /// <summary>邮件主题。</summary>
    public string MailSubject => _mailSubject;

    /// <summary>邮件正文。</summary>
    public string MailBody => _mailBody;

    /// <summary>邮件回复关键词。</summary>
    public string[] MailKeywords => _mailKeywords;

    /// <summary>选中关键词索引（-1=未选）。</summary>
    public int SelectedKeyword => _selectedKeyword;

    /// <summary>手机是否可见（非 Closed）。</summary>
    public bool IsVisible => _state != State.Closed;

    /// <summary>触发来电。</summary>
    public void TriggerCall(string caller, float timeoutSec, Action onAnswer, Action onIgnore)
    {
        _state = State.IncomingCall;
        _callerName = caller;
        _callTimer = timeoutSec;
        _callTimeout = timeoutSec;
        _onAnswer = onAnswer;
        _onIgnore = onIgnore;
        MarkDirty();
    }

    /// <summary>接听来电。</summary>
    public void AnswerCall()
    {
        if (_state != State.IncomingCall) return;
        _state = State.InCall;
        _callTimer = 0;
        MarkDirty();
        _onAnswer?.Invoke();
    }

    /// <summary>拒接/忽略来电。</summary>
    public void DeclineCall()
    {
        if (_state != State.IncomingCall && _state != State.InCall) return;
        _state = State.Closed;
        MarkDirty();
        _onIgnore?.Invoke();
    }

    /// <summary>显示邮件列表（收件箱）。</summary>
    public void OpenMailbox()
    {
        _state = State.MailList;
        MarkDirty();
    }

    /// <summary>打开一封邮件。</summary>
    public void OpenMail(string sender, string subject, string body, string[] keywords)
    {
        _state = State.MailView;
        _mailSender = sender;
        _mailSubject = subject;
        _mailBody = body;
        _mailKeywords = keywords;
        _selectedKeyword = -1;
        MarkDirty();
    }

    /// <summary>进入回复选择界面。</summary>
    public void ShowReplyOptions(string[] keywords, Action<int> onSelect)
    {
        _state = State.ReplySelect;
        _mailKeywords = keywords;
        _selectedKeyword = -1;
        _onReplySelect = onSelect;
        MarkDirty();
    }

    /// <summary>选择关键词回复。</summary>
    public void SelectKeyword(int index)
    {
        if (_state != State.ReplySelect || index < 0 || index >= _mailKeywords.Length) return;
        _selectedKeyword = index;
        _state = State.Closed;
        MarkDirty();
        _onReplySelect?.Invoke(index);
    }

    /// <summary>关闭手机。</summary>
    public void Close()
    {
        _state = State.Closed;
        MarkDirty();
    }

    protected internal override void Update(float deltaTime)
    {
        // 来电倒计时：超时 = 忽略
        if (_state == State.IncomingCall && _callTimer > 0)
        {
            _callTimer -= deltaTime;
            if (_callTimer <= 0)
            {
                _callTimer = 0;
                _state = State.Closed;
                MarkDirty();
                _onIgnore?.Invoke(); // 不接 = 忽略
            }
        }
    }

    protected internal override void OnDestroy()
    {
        _onAnswer = null; _onIgnore = null; _onReplySelect = null;
        base.OnDestroy();
    }

    public override string ToString() =>
        $"PhoneScreen (state={_state}, caller={_callerName}, timer={_callTimer:F1})";
}
