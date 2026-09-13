namespace NanoUint;

/// <summary>Button that swaps its sprite on hover.</summary>
public sealed class SpriteButton : Behaviour
{
    private Sprite? _sprite;
    private Sprite? _hoverSprite;
    private SpriteRenderer? _cachedRenderer;
    private PassiveButton? _passive;

    /// <summary>Normal-state sprite.</summary>
    public Sprite? Sprite
    {
        get => _sprite;
        set
        {
            _sprite = value;
            if (!_passive?.IsHovered == true && _cachedRenderer != null)
                _cachedRenderer.Sprite = value;
        }
    }

    /// <summary>Hover-state sprite.</summary>
    public Sprite? HoverSprite
    {
        get => _hoverSprite;
        set => _hoverSprite = value;
    }

    /// <summary>Whether the button is hovered.</summary>
    public bool IsHovered => _passive?.IsHovered ?? false;

    /// <summary>Whether the button is pressed.</summary>
    public bool IsPressed => _passive?.IsPressed ?? false;

    /// <summary>Raised on click.</summary>
    public event Action? OnClick;

    /// <summary>Raised when the mouse enters.</summary>
    public event Action? OnEnter;

    /// <summary>Raised when the mouse exits.</summary>
    public event Action? OnExit;

    /// <summary>Hint text shown on hover.</summary>
    public override string? HintText { get; set; }

    protected internal override void Awake()
    {
        _cachedRenderer = GameObject?.GetComponent<SpriteRenderer>();

        // Ensure a PassiveButton exists to handle interaction
        _passive = GameObject?.GetComponent<PassiveButton>();
        if (_passive == null && GameObject != null)
            _passive = GameObject.AddComponent<PassiveButton>();

        _passive!.OnEnter += HandleEnter;
        _passive.OnExit += HandleExit;
        _passive.OnClick += HandleClick;

        if (_cachedRenderer != null && _sprite != null && !_passive.IsHovered)
            _cachedRenderer.Sprite = _sprite;

        _passive.HintText = HintText;
    }

    private void HandleEnter()
    {
        if (_hoverSprite != null && _cachedRenderer != null)
            _cachedRenderer.Sprite = _hoverSprite;
        OnEnter?.Invoke();
    }

    private void HandleExit()
    {
        if (_sprite != null && _cachedRenderer != null)
            _cachedRenderer.Sprite = _sprite;
        OnExit?.Invoke();
    }

    private void HandleClick()
    {
        OnClick?.Invoke();
    }

    public override string ToString() =>
        $"SpriteButton (hovered={IsHovered}, pressed={IsPressed}, hint='{HintText}')";
}
