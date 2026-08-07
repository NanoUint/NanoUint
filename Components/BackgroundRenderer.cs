namespace NanoUint;

/// <summary>背景渲染器。挂载到 GameObject 上以显示全屏背景图，支持双纹理交叉淡入淡出。</summary>
public sealed class BackgroundRenderer : Component
{
    private Sprite? _sprite;

    /// <summary>当前背景精灵。</summary>
    public Sprite? Sprite
    {
        get => _sprite;
        set
        {
            if (_sprite != value)
            {
                _sprite = value;
                MarkDirty();
                if (GameObject?.Transform != null)
                {
                    GameObject.Transform.SortingOrder = -100;
                    GameObject.Transform.X = 0;
                    GameObject.Transform.Y = 0;
                }
            }
        }
    }

    /// <summary>交叉淡入淡出到新背景。引擎内部用双层 Image + 动画实现。</summary>
    public void CrossfadeTo(Sprite target, float durationSeconds)
    {
        PreviousSprite = Sprite;
        Sprite = target;
        FadeProgress = 0f;
        FadeDuration = durationSeconds;
        IsCrossfading = true;
        MarkDirty();
        Debug.Log($"CrossfadeTo: {target.Name} over {durationSeconds}s");
    }

    /// <summary>上一张背景（用于交叉淡入淡出）。</summary>
    public Sprite? PreviousSprite { get; private set; }

    private float _fadeProgress;
    /// <summary>交叉淡入淡出进度 (0=旧图, 1=新图)。</summary>
    public float FadeProgress
    {
        get => _fadeProgress;
        internal set { if (!_fadeProgress.Equals(value)) { _fadeProgress = value; MarkDirty(); } }
    }

    /// <summary>交叉淡入淡出持续时间。</summary>
    public float FadeDuration { get; private set; }

    private bool _isCrossfading;
    /// <summary>是否正在交叉淡入淡出。</summary>
    public bool IsCrossfading
    {
        get => _isCrossfading;
        private set { if (_isCrossfading != value) { _isCrossfading = value; MarkDirty(); } }
    }

    private Drawing.Color? _tintColor;
    /// <summary>背景色调叠加（回忆/恐怖氛围）。null = 无叠加。</summary>
    public Drawing.Color? TintColor
    {
        get => _tintColor;
        set { if (_tintColor != value) { _tintColor = value; MarkDirty(); } }
    }

    protected internal override void Update(float deltaTime)
    {
        if (!_isCrossfading) return;
        FadeProgress += deltaTime / FadeDuration;
        if (FadeProgress >= 1f)
        {
            FadeProgress = 1f;
            IsCrossfading = false;
            PreviousSprite = null;
        }
    }
}
