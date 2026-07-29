using System.Collections;
using NanoUint.Drawing;

namespace NanoUint;

/// <summary>
/// 对白框组件。内置打字机效果（协程驱动）。
/// </summary>
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

    /// <summary>当前显示的文本（可能被打字机截断）。</summary>
    public string Text => _displayedText;

    /// <summary>完整文本（含未显示部分）。</summary>
    public string FullText => _text;

    public Color BoxColor
    {
        get => _boxColor;
        set { if (!_boxColor.Equals(value)) { _boxColor = value; MarkDirty(); } }
    }

    public float TextSpeed { get; set; } = 0.05f;

    public bool IsComplete { get; private set; } = true;

    public event Action? OnTextComplete;

    /// <summary>显示对白并启动打字机效果。</summary>
    public void Show(string? speaker, string text)
    {
        SpeakerName = speaker ?? "";
        _text = text;
        _displayedText = "";
        IsComplete = false;
        MarkDirty();

        CoroutineScheduler.Instance.Start(TypewriterShow(), this);
    }

    /// <summary>打字机协程。按 Enter 可跳过直接显示全文。</summary>
    public IEnumerator TypewriterShow()
    {
        _displayedText = "";
        IsComplete = false;

        // 启动角色口型动画
        var characterSR = GameObject?.Scene?.FindObject("Character")?.GetComponent<SpriteRenderer>();
        if (characterSR?.HasMouthFlap == true)
            characterSR.StartMouthFlap();

        for (int i = 1; i <= _text.Length; i++)
        {
            // 按 Enter 跳过打字机，直接显示全文
            if (InputManager.IsAdvancePressedThisFrame())
            {
                InputManager.ConsumeAdvancePress();
                break;
            }

            _displayedText = _text[..i];
            MarkDirty();
            // TypewriterDelay: 等 TextSpeed 秒，但如果 Enter 被按下也立即返回（不消费，留给循环头检查）
            yield return new TypewriterDelay(TextSpeed);
        }

        _displayedText = _text;
        IsComplete = true;
        MarkDirty();

        // 停止口型动画
        characterSR?.StopMouthFlap();

        OnTextComplete?.Invoke();
    }

    /// <summary>立即显示全部文本（跳过打字机/点击跳过）。</summary>
    public void ShowAll()
    {
        _displayedText = _text;
        IsComplete = true;
        MarkDirty();
    }

    /// <summary>清空对白框。</summary>
    public void Clear()
    {
        SpeakerName = "";
        _text = "";
        _displayedText = "";
        IsComplete = true;
        MarkDirty();
    }

    public override string ToString() =>
        $"DialogueBox '{SpeakerName}: {(_text.Length > 30 ? _text[..30] + "..." : _text)}'";
}
