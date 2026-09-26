using NanoUint;
using NanoUintVN.Dialogue;

namespace NanoUintVN;

/// <summary>Abstract base class for VN screens with integrated dialogue playback.</summary>
public abstract class VNScreen
{
    protected readonly Scene Scene;
    private VNDocument? _currentDocument;
    private readonly DialoguePlayer _player = new();

    public Scene ActiveScene => Scene;
    public DialoguePlayer Player => _player;
    public VNDocument? CurrentDocument => _currentDocument;
    public bool IsActive { get; private set; }

    protected VNScreen(Scene scene)
    {
        Scene = scene;
        _player.Events.onPageEnter += OnPageEnter;
        _player.Events.OnBeatStart += OnBeatStart;
        _player.Events.OnBeatComplete += OnBeatComplete;
        _player.Events.OnDocumentEnd += OnDocumentEnd;
        _player.Events.OnChoicePresented += OnChoicePresented;
    }

    public void Open()
    {
        IsActive = true;
        OnOpen();
    }

    public void Close()
    {
        IsActive = false;
        _player.Stop();
        OnClose();
    }

    public void PlayDocument(VNDocument document)
    {
        _currentDocument = document;
        _player.LoadDocument(document);
        _player.Start();
    }

    public void Advance() => _player.Advance();
    public void SelectChoice(int index) => _player.SelectChoice(index);
    public void Pause() => _player.Pause();
    public void Resume() => _player.Resume();

    public bool ProcessKeyDown(EngineKey key)
    {
        if (!IsActive) return false;
        switch (key)
        {
            case EngineKey.Space:
            case EngineKey.Enter:
                Advance();
                return true;
            default:
                return OnKeyDown(key);
        }
    }

    protected virtual void OnOpen() { }
    protected virtual void OnClose() { }
    protected virtual void OnPageEnter(VNPage page) { }
    protected virtual void OnBeatStart(DialogueBeat beat) { }
    protected virtual void OnBeatComplete(DialogueBeat beat) { }
    protected virtual void OnDocumentEnd() { }
    protected virtual void OnChoicePresented(ChoiceElement choice) { }
    protected virtual bool OnKeyDown(EngineKey key) => false;

    protected void ClearScene()
    {
        foreach (var go in Scene.RootObjects.ToList())
            go.Destroy();
    }

    protected static string L(string key) => LocalizationManager.Get(key);
}
