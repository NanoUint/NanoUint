using System.Collections;
using NanoUint.Drawing;

namespace NanoUint;

/// <summary>对白框组件。渐进式文字渐显效果。</summary>
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

    /// <summary>当前显示的文本（渐进渐显模式始终为全文）。</summary>
    public string Text => _displayedText;

    /// <summary>完整文本（含未显示部分 — 渐进模式始终与 Text 相同）。</summary>
    public string FullText => _text;

    public Color BoxColor
    {
        get => _boxColor;
        set { if (!_boxColor.Equals(value)) { _boxColor = value; MarkDirty(); } }
    }

    /// <summary>渐显进度（0=全部隐藏, 1=全部可见）。WpfRenderer 据此应用 OpacityMask。</summary>
    public float RevealProgress { get; private set; } = 1f;

    /// <summary>渐显动画总时长（秒）。由语音长度或默认估算决定。</summary>
    public float RevealDuration { get; set; }

    /// <summary>文字渐显速度（秒/字符）。无语音时用于估算 RevealDuration。</summary>
    public float TextSpeed { get; set; } = 0.05f;

    public bool IsComplete { get; private set; } = true;

    /// <summary>文本完全显示后触发。</summary>
    public event Action? OnTextComplete;

    /// <summary>渐显动画被打断（Enter 跳过）时触发。用于停止语音。</summary>
    public event Action? OnRevealInterrupted;

    /// <summary>显示对白并启动渐显动画。</summary>
    public void Show(string? speaker, string text)
    {
        SpeakerName = speaker ?? "";
        _text = text;
        _displayedText = text; // 立即显示全文，渐显由 OpacityMask 控制
        IsComplete = false;
        MarkDirty();

        CoroutineScheduler.Instance.Start(ProgressiveReveal(), this);
    }

    /// <summary>渐进式渐显协程。每帧更新 RevealProgress，按 Enter 打断并显示全文。</summary>
    public IEnumerator ProgressiveReveal()
    {
        _displayedText = _text;
        RevealProgress = 0f;
        IsComplete = false;
        MarkDirty();

        // 启动口型动画
        var characterSR = GameObject?.Scene?.FindObject("Character")?.GetComponent<SpriteRenderer>();
        if (characterSR?.HasMouthFlap == true)
            characterSR.StartMouthFlap();

        float duration = RevealDuration;
        // 无语音时：由 TextSpeed * 字符数 估算，最少 1.0s
        if (duration <= 0f)
            duration = Math.Max(1.0f, _text.Length * TextSpeed);

        // 使用真实时间追踪，确保渐显与语音时长对齐
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

        // 停止口型动画
        characterSR?.StopMouthFlap();

        OnTextComplete?.Invoke();
    }

    /// <summary>立即显示全部文本（跳过渐显动画）。</summary>
    public void ShowAll()
    {
        _displayedText = _text;
        RevealProgress = 1f;
        IsComplete = true;
        MarkDirty();
    }

    /// <summary>清空对白框。</summary>
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
