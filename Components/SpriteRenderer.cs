using System.Collections;
using NanoUint.Drawing;

namespace NanoUint;

/// <summary>Displays a 2D sprite with optional mouth-flap animation.</summary>
public sealed class SpriteRenderer : Behaviour
{
    private Sprite? _sprite;
    private Color _tint = Color.White;
    private bool _fullScreen;
    private float _scale = 1f;

    #region Mouth animation
    private Sprite? _mouthClosed, _mouthHalf, _mouthOpen;
    private bool _isSpeaking;
    private int _currentMouthFrame; // 0 = closed, 1 = half-open, 2 = open
    private Coroutine? _mouthFlapRoutine;

    /// <summary>Sprite to display.</summary>
    public Sprite? Sprite
    {
        get => _sprite;
        set
        {
            if (_sprite != value)
            {
                _sprite = value;
                MarkDirty();
            }
        }
    }

    /// <summary>Color tint; white means no tint.</summary>
    public Color Tint
    {
        get => _tint;
        set { if (!_tint.Equals(value)) { _tint = value; MarkDirty(); } }
    }

    /// <summary>Scale multiplier; 1 = original size, 0.5 = half size.</summary>
    public float Scale
    {
        get => _scale;
        set { if (!_scale.Equals(value)) { _scale = value; MarkDirty(); } }
    }

    /// <summary>Whether to stretch the sprite to fill the whole screen.</summary>
    public bool FullScreen
    {
        get => _fullScreen;
        set
        {
            if (_fullScreen == value) return;
            _fullScreen = value;
            if (value && GameObject?.Transform != null)
            {
                GameObject.Transform.X = 0.5f;
                GameObject.Transform.Y = 0.5f;
            }
            MarkDirty();
        }
    }

    /// <summary>Makes the sprite fill the screen.</summary>
    public SpriteRenderer SetFullScreen(bool fullScreen = true)
    {
        FullScreen = fullScreen;
        return this;
    }

    #endregion

    #region Mouth frames

    /// <summary>Closed-mouth frame.</summary>
    public Sprite? MouthClosed
    {
        get => _mouthClosed;
        set { _mouthClosed = value; if (_isSpeaking) MarkDirty(); }
    }

    /// <summary>Half-open mouth frame.</summary>
    public Sprite? MouthHalf
    {
        get => _mouthHalf;
        set { _mouthHalf = value; if (_isSpeaking) MarkDirty(); }
    }

    /// <summary>Open mouth frame.</summary>
    public Sprite? MouthOpen
    {
        get => _mouthOpen;
        set { _mouthOpen = value; if (_isSpeaking) MarkDirty(); }
    }

    /// <summary>Whether the mouth animation is playing.</summary>
    public bool IsSpeaking => _isSpeaking;

    internal int CurrentMouthFrame => _currentMouthFrame;

    /// <summary>Whether any mouth animation frames are set.</summary>
    public bool HasMouthFlap => _mouthClosed != null || _mouthHalf != null || _mouthOpen != null;

    internal Sprite? GetActiveMouthSprite()
    {
        if (!_isSpeaking || !HasMouthFlap) return _sprite;
        return _currentMouthFrame switch
        {
            0 => _mouthClosed ?? _sprite,
            1 => _mouthHalf ?? _sprite,
            2 => _mouthOpen ?? _sprite,
            _ => _sprite
        };
    }

    /// <summary>Starts the mouth-flap animation loop.</summary>
    public void StartMouthFlap()
    {
        if (_isSpeaking) return;
        _isSpeaking = true;
        _currentMouthFrame = 0;
        _mouthFlapRoutine = StartCoroutine(MouthFlapLoop());
    }

    /// <summary>Stops the mouth animation and returns to the closed-mouth state.</summary>
    public void StopMouthFlap()
    {
        _isSpeaking = false;
        if (_mouthFlapRoutine != null)
        {
            StopCoroutine(_mouthFlapRoutine);
            _mouthFlapRoutine = null;
        }
        if (_currentMouthFrame != 0)
        {
            _currentMouthFrame = 0;
            MarkDirty();
        }
    }

    private IEnumerator MouthFlapLoop()
    {
        while (_isSpeaking)
        {
            // Brief hold on the closed frame
            yield return new WaitForSeconds(0.07f);
            if (!_isSpeaking) break;

            _currentMouthFrame = 1; MarkDirty();
            yield return new WaitForSeconds(0.08f);
            if (!_isSpeaking) break;

            _currentMouthFrame = 2; MarkDirty();
            yield return new WaitForSeconds(0.08f);
            if (!_isSpeaking) break;

            _currentMouthFrame = 1; MarkDirty();
            yield return new WaitForSeconds(0.08f);
            if (!_isSpeaking) break;

            _currentMouthFrame = 0; MarkDirty();
        }
        _currentMouthFrame = 0;
    }

    protected internal override void OnDestroy()
    {
        StopMouthFlap();
        base.OnDestroy();
    }

    public override string ToString() => $"SpriteRenderer (sprite={(Sprite != null ? Sprite.Name : "null")}" +
        $"{(HasMouthFlap ? " [mouth flap]" : "")})";
    #endregion
}
