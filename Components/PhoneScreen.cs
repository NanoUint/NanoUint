using System.Collections;

namespace NanoUint;

/// <summary>Phone trigger system UI component.</summary>
public sealed class PhoneScreen : Behaviour
{
    public enum State
    {
        Closed,
        Opening,
        BlackScreen,       // Blank screen; waits for Enter/tap to unlock
        Home,              // Home screen with wallpaper and app icons
        DenhaIncoming,
        DenhaInCall,
        RineList,
        RineChat,
        RineStamp,         // RINE sticker picker
        Settings,
        SettingsWallpaper,
    }

    private State _state = State.Closed;

    #region Render config (initialized by game layer)

    public PhoneRenderConfig? Config { get; set; }

    #endregion

    #region Incoming call

    private string _callerName = "";
    private float _callTimer;
    private float _callTimeout;
    private Action? _onAnswer, _onIgnore;

    #endregion

    #region Mail/Reply (legacy API compat)

    private string _mailSender = "", _mailSubject = "", _mailBody = "";
    private string[] _mailKeywords = Array.Empty<string>();
    private int _selectedKeyword = -1;
    private Action<int>? _onReplySelect;

    #endregion

    #region Navigation state

    private int _wallpaperIndex;
    private int _highlightedApp = -1;  // -1 = none, 0 = Denha, 1 = Rine, 2 = Settings
    private string _activeRineContact = "";
    private string[] _availableStamps = Array.Empty<string>();
    private Action<int>? _onStampSelected;

    #endregion

    #region Properties

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

    #region Navigation methods

    /// <summary>Shows the phone.</summary>
    public void ShowHome()
    {
        if (_state != State.Closed) return;
        _state = State.Opening;
        _highlightedApp = -1;
        _slideTimer = 0f;
        MarkDirty();
    }

    /// <summary>Wakes from the black screen into the home screen.</summary>
    public void WakePhone()
    {
        if (_state != State.BlackScreen) return;
        _state = State.Home;
        MarkDirty();
    }

    /// <summary>Toggles phone visibility.</summary>
    public void Toggle()
    {
        if (_state == State.Closed) ShowHome();
        else Close();
    }

    /// <summary>Sets the phone state.</summary>
    public void SetState(State s)
    {
        if (_state == s) return;
        _state = s;
        MarkDirty();
    }

    /// <summary>Slide-in animation timer in seconds.</summary>
    public float SlideTimer
    {
        get => _slideTimer;
        set { _slideTimer = value; MarkDirty(); }
    }
    private float _slideTimer;

    /// <summary>Opens the phone app.</summary>
    public void OpenDenha()
    {
        _state = State.DenhaIncoming;
        MarkDirty();
    }

    /// <summary>Opens the RINE conversation list.</summary>
    public void OpenRine()
    {
        _state = State.RineList;
        MarkDirty();
    }

    /// <summary>Opens the RINE chat with a contact.</summary>
    public void OpenRineChat(string contact)
    {
        _state = State.RineChat;
        _activeRineContact = contact;
        MarkDirty();
    }

    /// <summary>Opens the RINE sticker picker.</summary>
    public void OpenRineStampPicker(string[] stamps, Action<int> onSelect)
    {
        _state = State.RineStamp;
        _availableStamps = stamps;
        _onStampSelected = onSelect;
        MarkDirty();
    }

    /// <summary>Selects a sticker.</summary>
    public void SelectStamp(int index)
    {
        if (_state != State.RineStamp || index < 0 || index >= _availableStamps.Length) return;
        _state = State.RineChat;
        MarkDirty();
        _onStampSelected?.Invoke(index);
    }

    /// <summary>Opens settings.</summary>
    public void OpenSettings()
    {
        _state = State.Settings;
        MarkDirty();
    }

    /// <summary>Opens the wallpaper picker.</summary>
    public void OpenWallpaperPicker()
    {
        _state = State.SettingsWallpaper;
        MarkDirty();
    }

    /// <summary>Selects a wallpaper.</summary>
    public void SelectWallpaper(int index)
    {
        _wallpaperIndex = Math.Clamp(index, 0, 11);
        MarkDirty();
    }

    /// <summary>Cycles the wallpaper (left/right).</summary>
    public void CycleWallpaper(int direction)
    {
        _wallpaperIndex = (_wallpaperIndex + direction + 12) % 12;
        MarkDirty();
    }

    /// <summary>Sets the highlighted app.</summary>
    public void SetAppHighlight(int appIndex)
    {
        _highlightedApp = appIndex;
        MarkDirty();
    }

    #endregion

    #region Incoming call (legacy API compat)

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

    /// <summary>Ends the call.</summary>
    public void EndCall()
    {
        if (_state != State.DenhaInCall) return;
        _state = State.Home;
        MarkDirty();
    }

    #endregion

    #region Mail (legacy API -> maps to RINE)

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

    #region Close

    public void Close()
    {
        _state = State.Closed;
        _slideTimer = 0f;
        MarkDirty();
    }

    /// <summary>Returns to the home screen from any sub-screen.</summary>
    public void BackToHome()
    {
        _state = State.Home;
        _highlightedApp = -1;
        MarkDirty();
    }

    #endregion

    #region Lifecycle

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
            MarkDirty(); // Mark dirty each frame so the renderer advances the animation
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
