using NanoUint.EventSystems;

namespace NanoUint;

/// <summary>使同 GameObject 上的视觉组件变为可按/可悬停交互。</summary>
public sealed class PassiveButton : Behaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    IPointerClickHandler
{
    /// <summary>鼠标是否正在悬停在此元素上。</summary>
    public bool IsHovered { get; private set; }

    /// <summary>鼠标左键是否正在此元素上按下。</summary>
    public bool IsPressed { get; private set; }

    #region UnityEvent 风格回调

    /// <summary>指针进入时触发。</summary>
    public event Action? OnEnter;

    /// <summary>指针离开时触发。</summary>
    public event Action? OnExit;

    /// <summary>指针按下时触发。</summary>
    public event Action? OnDown;

    /// <summary>指针抬起时触发。</summary>
    public event Action? OnUp;

    /// <summary>完整点击（按下+抬起在同一元素）。</summary>
    public event Action? OnClick;

    #endregion

    #region Hint

    /// <summary>悬停时显示的提示文本。</summary>
    public override string? HintText { get; set; }

    #endregion

    #region IPointer*Handler 实现

    void IPointerEnterHandler.OnPointerEnter()
    {
        IsHovered = true;
        OnEnter?.Invoke();
        MarkDirty();
    }

    void IPointerExitHandler.OnPointerExit()
    {
        IsHovered = false;
        IsPressed = false;
        OnExit?.Invoke();
        MarkDirty();
    }

    void IPointerDownHandler.OnPointerDown()
    {
        IsPressed = true;
        OnDown?.Invoke();
        MarkDirty();
    }

    void IPointerUpHandler.OnPointerUp()
    {
        if (IsPressed)
        {
            OnUp?.Invoke();
            MarkDirty();
        }
        IsPressed = false;
    }

    void IPointerClickHandler.OnPointerClick()
    {
        OnClick?.Invoke();
    }

    public override string ToString() =>
        $"PassiveButton (hovered={IsHovered}, pressed={IsPressed}, hint='{HintText}')";
    #endregion
}
