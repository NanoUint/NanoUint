using NanoUint;

namespace NanoUintVN.Components;

/// <summary>Displays a full-screen background with crossfade support.</summary>
public sealed class BackgroundRenderer : Component
{
    private Sprite? _sprite;

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

    public void CrossfadeTo(Sprite target, float durationSeconds)
    {
        PreviousSprite = Sprite;
        Sprite = target;
        FadeProgress = 0f;
        FadeDuration = durationSeconds;
        IsCrossfading = true;
        MarkDirty();
    }

    public Sprite? PreviousSprite { get; private set; }

    private float _fadeProgress;
    public float FadeProgress
    {
        get => _fadeProgress;
        internal set { if (!_fadeProgress.Equals(value)) { _fadeProgress = value; MarkDirty(); } }
    }

    public float FadeDuration { get; private set; }

    private bool _isCrossfading;
    public bool IsCrossfading
    {
        get => _isCrossfading;
        private set { if (_isCrossfading != value) { _isCrossfading = value; MarkDirty(); } }
    }

    private NanoUint.Drawing.Color? _tintColor;
    public NanoUint.Drawing.Color? TintColor
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
