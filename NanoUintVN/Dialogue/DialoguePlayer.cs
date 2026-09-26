namespace NanoUintVN.Dialogue;

/// <summary>Events raised by DialoguePlayer during playback.</summary>
public sealed class DialoguePlayerEvents
{
    public event Action<DialogueBeat>? OnBeatStart;
    public event Action<DialogueBeat>? OnBeatComplete;
    public event Action<VNPage>? onPageEnter;
    public event Action<VNPage>? onPageExit;
    public event Action? OnDocumentEnd;
    public event Action<ChoiceElement>? OnChoicePresented;

    internal void RaiseBeatStart(DialogueBeat beat) => OnBeatStart?.Invoke(beat);
    internal void RaiseBeatComplete(DialogueBeat beat) => OnBeatComplete?.Invoke(beat);
    internal void RaisePageEnter(VNPage page) => onPageEnter?.Invoke(page);
    internal void RaisePageExit(VNPage page) => onPageExit?.Invoke(page);
    internal void RaiseDocumentEnd() => OnDocumentEnd?.Invoke();
    internal void RaiseChoicePresented(ChoiceElement choice) => OnChoicePresented?.Invoke(choice);
}

/// <summary>State of the dialogue player.</summary>
public enum PlayerState
{
    Idle,
    Playing,
    WaitingForAdvance,
    WaitingForChoice,
    Paused,
    Complete
}

/// <summary>Runtime player that executes VNPage sequences.</summary>
public sealed class DialoguePlayer
{
    private VNDocument? _document;
    private int _pageIndex;
    private int _beatIndex;
    private bool _isTyping;

    public PlayerState State { get; private set; } = PlayerState.Idle;
    public VNPage? CurrentPage => _document?.Pages.ElementAtOrDefault(_pageIndex);
    public DialogueBeat? CurrentBeat => CurrentPage?.Beats.ElementAtOrDefault(_beatIndex);
    public int PageIndex => _pageIndex;
    public int BeatIndex => _beatIndex;
    public float TypewriterProgress { get; private set; }
    public DialoguePlayerEvents Events { get; } = new();

    public void LoadDocument(VNDocument document)
    {
        _document = document;
        _pageIndex = 0;
        _beatIndex = 0;
        State = PlayerState.Idle;
    }

    public void Start()
    {
        if (_document == null || _document.Pages.Count == 0)
        {
            State = PlayerState.Complete;
            return;
        }
        State = PlayerState.Playing;
        EnterPage();
    }

    public void Advance()
    {
        if (State == PlayerState.WaitingForAdvance)
        {
            if (!AdvanceBeat())
            {
                if (!AdvancePage())
                {
                    State = PlayerState.Complete;
                    Events.RaiseDocumentEnd();
                }
            }
        }
        else if (State == PlayerState.Playing && _isTyping)
        {
            TypewriterProgress = 1f;
            _isTyping = false;
            State = PlayerState.WaitingForAdvance;
            if (CurrentBeat != null)
                Events.RaiseBeatComplete(CurrentBeat);
        }
    }

    public void SelectChoice(int index)
    {
        if (State != PlayerState.WaitingForChoice) return;
        State = PlayerState.Playing;
        AdvancePage();
    }

    public void Pause()
    {
        if (State == PlayerState.Playing || State == PlayerState.WaitingForAdvance)
            State = PlayerState.Paused;
    }

    public void Resume()
    {
        if (State == PlayerState.Paused)
            State = PlayerState.Playing;
    }

    public void Stop()
    {
        State = PlayerState.Idle;
        _document = null;
        _pageIndex = 0;
        _beatIndex = 0;
    }

    public void UpdateTypewriter(float dt, float textSpeed)
    {
        if (State != PlayerState.Playing || !_isTyping) return;
        TypewriterProgress += dt / textSpeed;
        if (TypewriterProgress >= 1f)
        {
            TypewriterProgress = 1f;
            _isTyping = false;
            State = PlayerState.WaitingForAdvance;
            if (CurrentBeat != null)
                Events.RaiseBeatComplete(CurrentBeat);
        }
    }

    public void SaveState(out int pageIndex, out int beatIndex)
    {
        pageIndex = _pageIndex;
        beatIndex = _beatIndex;
    }

    public void LoadState(int pageIndex, int beatIndex)
    {
        _pageIndex = pageIndex;
        _beatIndex = beatIndex;
        State = PlayerState.Playing;
        if (CurrentPage != null)
        {
            Events.RaisePageEnter(CurrentPage);
            if (_beatIndex < CurrentPage.Beats.Count)
                StartBeat();
            else
                State = PlayerState.WaitingForAdvance;
        }
        else
        {
            State = PlayerState.Complete;
            Events.RaiseDocumentEnd();
        }
    }

    private void EnterPage()
    {
        var page = CurrentPage;
        if (page == null)
        {
            State = PlayerState.Complete;
            Events.RaiseDocumentEnd();
            return;
        }

        Events.RaisePageEnter(page);

        if (page.Choice != null)
        {
            State = PlayerState.WaitingForChoice;
            Events.RaiseChoicePresented(page.Choice);
            return;
        }

        if (page.Beats.Count > 0)
        {
            _beatIndex = 0;
            StartBeat();
        }
        else
        {
            State = PlayerState.WaitingForAdvance;
        }
    }

    private void StartBeat()
    {
        var beat = CurrentBeat;
        if (beat == null)
        {
            State = PlayerState.WaitingForAdvance;
            return;
        }

        TypewriterProgress = 0f;
        _isTyping = true;
        State = PlayerState.Playing;
        Events.RaiseBeatStart(beat);
    }

    private bool AdvanceBeat()
    {
        if (CurrentPage == null) return false;
        if (_beatIndex < CurrentPage.Beats.Count - 1)
        {
            _beatIndex++;
            StartBeat();
            return true;
        }
        return false;
    }

    private bool AdvancePage()
    {
        if (_document == null) return false;
        if (_pageIndex < _document.Pages.Count - 1)
        {
            var oldPage = CurrentPage;
            if (oldPage != null) Events.RaisePageExit(oldPage);
            _pageIndex++;
            _beatIndex = 0;
            EnterPage();
            return true;
        }
        return false;
    }
}
