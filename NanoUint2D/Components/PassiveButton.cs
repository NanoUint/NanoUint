using NanoUint.EventSystems;

namespace NanoUint;

/// <summary>Makes the visual components on the same GameObject clickable and hoverable.</summary>
public sealed class PassiveButton : Behaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    IPointerClickHandler
{
    /// <summary>Whether the mouse is currently hovering over this element.</summary>
    public bool IsHovered { get; private set; }

    /// <summary>Whether the left mouse button is currently pressed on this element.</summary>
    public bool IsPressed { get; private set; }

    #region UnityEvent-style callbacks

    /// <summary>Raised when the pointer enters.</summary>
    public event Action? OnEnter;

    /// <summary>Raised when the pointer exits.</summary>
    public event Action? OnExit;

    /// <summary>Raised when the pointer is pressed down.</summary>
    public event Action? OnDown;

    /// <summary>Raised when the pointer is released.</summary>
    public event Action? OnUp;

    /// <summary>Full click (press and release on the same element).</summary>
    public event Action? OnClick;

    #endregion

    #region Hint

    /// <summary>Hint text shown on hover.</summary>
    public override string? HintText { get; set; }

    #endregion

    #region IPointer*Handler implementation

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
