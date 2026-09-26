using System.Collections;
using NanoUint.Drawing;

namespace NanoUint;

/// <summary>Dialogue box component with a progressive text reveal effect.</summary>
[Obsolete("Use NanoUintVN.Components.DialogueBox instead.")]
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

    /// <summary>Currently displayed text.</summary>
    public string Text => _displayedText;

    /// <summary>Full text, including the not-yet-revealed part.</summary>
    public string FullText => _text;

    public Color BoxColor
    {
        get => _boxColor;
        set { if (!_boxColor.Equals(value)) { _boxColor = value; MarkDirty(); } }
    }

    /// <summary>Reveal progress (0 = fully hidden, 1 = fully visible).</summary>
    public float RevealProgress { get; private set; } = 1f;

    /// <summary>Total reveal duration in seconds.</summary>
    public float RevealDuration { get; set; }

    /// <summary>Reveal speed in seconds per character.</summary>
    public float TextSpeed { get; set; } = 0.05f;

    public bool IsComplete { get; private set; } = true;

    /// <summary>Raised when the text is fully revealed.</summary>
    public event Action? OnTextComplete;

    /// <summary>Raised when the reveal is interrupted (Enter skip).</summary>
    public event Action? OnRevealInterrupted;

    /// <summary>Shows the dialogue and starts the reveal animation.</summary>
    public void Show(string? speaker, string text)
    {
        SpeakerName = speaker ?? "";
        _text = text;
        _displayedText = text; // Set the full text immediately; the reveal is driven by the OpacityMask
        IsComplete = false;
        MarkDirty();

        CoroutineScheduler.Instance.Start(ProgressiveReveal(), this);
    }

    /// <summary>Progressive reveal coroutine; Enter skips to the full text.</summary>
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
        // Without voice: estimate from TextSpeed * character count, at least 1.0s
        if (duration <= 0f)
            duration = Math.Max(1.0f, _text.Length * TextSpeed);

        // Track real time so the reveal stays aligned with the voice duration
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

    /// <summary>Shows the full text immediately, skipping the reveal animation.</summary>
    public void ShowAll()
    {
        _displayedText = _text;
        RevealProgress = 1f;
        IsComplete = true;
        MarkDirty();
    }

    /// <summary>Clears the dialogue box.</summary>
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
