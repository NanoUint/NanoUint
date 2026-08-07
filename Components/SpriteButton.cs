namespace NanoUint;

/// <summary>图片按钮。悬停时自动切换 SpriteRenderer 的 Sprite（正常/悬停）。</summary>
public sealed class SpriteButton : Behaviour
{
    private Sprite? _sprite;
    private Sprite? _hoverSprite;
    private SpriteRenderer? _cachedRenderer;
    private PassiveButton? _passive;

    /// <summary>正常状态精灵。</summary>
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

    /// <summary>悬停状态精灵。</summary>
    public Sprite? HoverSprite
    {
        get => _hoverSprite;
        set => _hoverSprite = value;
    }

    /// <summary>是否正在悬停。</summary>
    public bool IsHovered => _passive?.IsHovered ?? false;

    /// <summary>是否正在按下。</summary>
    public bool IsPressed => _passive?.IsPressed ?? false;

    /// <summary>点击事件（由 PassiveButton 转发）。</summary>
    public event Action? OnClick;

    /// <summary>鼠标进入事件。</summary>
    public event Action? OnEnter;

    /// <summary>鼠标离开事件。</summary>
    public event Action? OnExit;

    /// <summary>悬停提示文本。</summary>
    public override string? HintText { get; set; }

    protected internal override void Awake()
    {
        _cachedRenderer = GameObject?.GetComponent<SpriteRenderer>();

        // 确保有 PassiveButton 处理交互
        _passive = GameObject?.GetComponent<PassiveButton>();
        if (_passive == null && GameObject != null)
            _passive = GameObject.AddComponent<PassiveButton>();

        // 转发 PassiveButton 事件
        _passive!.OnEnter += HandleEnter;
        _passive.OnExit += HandleExit;
        _passive.OnClick += HandleClick;

        // 显示初始 Sprite
        if (_cachedRenderer != null && _sprite != null && !_passive.IsHovered)
            _cachedRenderer.Sprite = _sprite;

        // 同步 HintText
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
