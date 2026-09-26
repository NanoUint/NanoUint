using System.Collections;
using NanoUint;
using NanoUint.Drawing;

namespace NanoUintVN.Components;

/// <summary>Dialogue box component with a progressive text reveal effect.</summary>
public sealed class DialogueBox : Component
{
    private string _speakerName = "";
    private string _text = "";
    private string _displayedText = "";
    private Color _boxColor = Color.FromRgb(10, 10, 10);

    public string SpeakerName
    {
        get => _speakerName;
        set { if (_speakerName != value) { _speakerName = value; MarkDirty(); } }
    }

    public string Text => _displayedText;
    public string FullText => _text;

    public Color BoxColor
    {
        get => _boxColor;
        set { if (!_boxColor.Equals(value)) { _boxColor = value; MarkDirty(); } }
    }

    public float RevealProgress { get; private set; } = 1f;
    public float RevealDuration { get; set; }
    public float TextSpeed { get; set; } = 0.05f;
    public bool IsComplete { get; private set; } = true;

    public event Action? OnTextComplete;
    public event Action? OnRevealInterrupted;

    public void Show(string? speaker, string text)
    {
        SpeakerName = speaker ?? "";
        _text = text;
        _displayedText = text;
        IsComplete = false;
        MarkDirty();
        CoroutineScheduler.Instance.Start(ProgressiveReveal(), this);
    }

    public IEnumerator ProgressiveReveal()
    {
        _displayedText = _text;
        RevealProgress = 0f;
        IsComplete = false;
        MarkDirty();

        var characterSR = GameObject?.Scene?.FindObject("Character")?.GetComponent<SpriteRenderer>();
        if (characterSR?.HasMouthFlap == true)
            characterSR.StartMouthFlap();

        float duration = RevealDuration;
        if (duration <= 0f)
            duration = Math.Max(1.0f, _text.Length * TextSpeed);

        var startTime = DateTime.UtcNow;
        while (true)
        {
            if (InputManager.IsAdvancePressedThisFrame())
            {
                InputManager.ConsumeAdvancePress();
                OnRevealInterrupted?.Invoke();
                break;
            }

            float elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            RevealProgress = Math.Clamp(elapsed / duration, 0f, 1f);
            MarkDirty();

            if (elapsed >= duration) break;
            yield return WaitForEndOfFrame.Instance;
        }

        RevealProgress = 1f;
        IsComplete = true;
        MarkDirty();

        characterSR?.StopMouthFlap();
        OnTextComplete?.Invoke();
    }

    public void ShowAll()
    {
        _displayedText = _text;
        RevealProgress = 1f;
        IsComplete = true;
        MarkDirty();
    }

    public void Clear()
    {
        SpeakerName = "";
        _text = "";
        _displayedText = "";
        RevealProgress = 1f;
        IsComplete = true;
        MarkDirty();
    }

    public override string ToString() =>
        $"DialogueBox '{SpeakerName}: {(_text.Length > 30 ? _text[..30] + "..." : _text)}'";
}
