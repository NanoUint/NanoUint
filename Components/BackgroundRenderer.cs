namespace NanoUint;

/// <summary>Displays a full-screen background with crossfade support.</summary>
public sealed class BackgroundRenderer : Component
{
    private Sprite? _sprite;

    /// <summary>Current background sprite.</summary>
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

    /// <summary>Crossfades to a new background over the given duration.</summary>
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

    /// <summary>Previous background, used during the crossfade.</summary>
    public Sprite? PreviousSprite { get; private set; }

    private float _fadeProgress;
    /// <summary>Crossfade progress (0 = old sprite, 1 = new sprite).</summary>
    public float FadeProgress
    {
        get => _fadeProgress;
        internal set { if (!_fadeProgress.Equals(value)) { _fadeProgress = value; MarkDirty(); } }
    }

    /// <summary>Crossfade duration.</summary>
    public float FadeDuration { get; private set; }

    private bool _isCrossfading;
    /// <summary>Whether a crossfade is in progress.</summary>
    public bool IsCrossfading
    {
        get => _isCrossfading;
        private set { if (_isCrossfading != value) { _isCrossfading = value; MarkDirty(); } }
    }

    private Drawing.Color? _tintColor;
    /// <summary>Background tint overlay; null = no overlay.</summary>
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
