using System.Collections;
using NanoUint.Drawing;

namespace NanoUint;

/// <summary>
/// 精灵渲染器。挂载到 GameObject 上以显示一张 2D 图片（角色立绘、Logo 等）。
/// 引擎内部翻译为 WPF Image 控件。
/// 支持口型动画：设置 MouthClosed/MouthHalf/MouthOpen 后调用 StartMouthFlap/StopMouthFlap。
/// </summary>
public sealed class SpriteRenderer : Behaviour
{
    private Sprite? _sprite;
    private Color _tint = Color.White;

    // ── 口型动画 ──
    private Sprite? _mouthClosed, _mouthHalf, _mouthOpen;
    private bool _isSpeaking;
    private int _currentMouthFrame; // 0=闭口, 1=半开, 2=全开
    private Coroutine? _mouthFlapRoutine;

    /// <summary>要显示的精灵。</summary>
    public Sprite? Sprite
    {
        get => _sprite;
        set
        {
            if (_sprite != value)
            {
                _sprite = value;
                MarkDirty();
                if (value != null && GameObject?.Transform != null && GameObject.Transform.X == 0 && GameObject.Transform.Y == 0)
                {
                    GameObject.Transform.X = 0.5f;
                    GameObject.Transform.Y = 0.3f;
                }
            }
        }
    }

    /// <summary>颜色叠加（Tint）。默认白色即无叠加。</summary>
    public Color Tint
    {
        get => _tint;
        set { if (!_tint.Equals(value)) { _tint = value; MarkDirty(); } }
    }

    // ── 口型帧 ──

    /// <summary>闭口帧（口型动画 _0）。</summary>
    public Sprite? MouthClosed
    {
        get => _mouthClosed;
        set { _mouthClosed = value; if (_isSpeaking) MarkDirty(); }
    }

    /// <summary>半开口型帧（口型动画 _1）。</summary>
    public Sprite? MouthHalf
    {
        get => _mouthHalf;
        set { _mouthHalf = value; if (_isSpeaking) MarkDirty(); }
    }

    /// <summary>全开口型帧（口型动画 _2）。</summary>
    public Sprite? MouthOpen
    {
        get => _mouthOpen;
        set { _mouthOpen = value; if (_isSpeaking) MarkDirty(); }
    }

    /// <summary>是否正在播放口型动画。</summary>
    public bool IsSpeaking => _isSpeaking;

    /// <summary>当前口型帧索引（0=闭, 1=半, 2=全）。引擎内部读取。</summary>
    internal int CurrentMouthFrame => _currentMouthFrame;

    /// <summary>是否有口型动画帧。</summary>
    public bool HasMouthFlap => _mouthClosed != null || _mouthHalf != null || _mouthOpen != null;

    /// <summary>获取当前应显示的口型 Sprite（含 fallback）。</summary>
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

    /// <summary>开始口型动画循环。</summary>
    public void StartMouthFlap()
    {
        if (_isSpeaking) return;
        _isSpeaking = true;
        _currentMouthFrame = 0;
        _mouthFlapRoutine = StartCoroutine(MouthFlapLoop());
    }

    /// <summary>停止口型动画并回复闭口状态。</summary>
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

    /// <summary>口型循环协程: _0→_1→_2→_1→_0→_1→... (3帧循环，~80ms/帧)。</summary>
    private IEnumerator MouthFlapLoop()
    {
        while (_isSpeaking)
        {
            // 闭口短暂停留
            yield return new WaitForSeconds(0.07f);
            if (!_isSpeaking) break;

            // → 半开
            _currentMouthFrame = 1; MarkDirty();
            yield return new WaitForSeconds(0.08f);
            if (!_isSpeaking) break;

            // → 全开
            _currentMouthFrame = 2; MarkDirty();
            yield return new WaitForSeconds(0.08f);
            if (!_isSpeaking) break;

            // → 半开
            _currentMouthFrame = 1; MarkDirty();
            yield return new WaitForSeconds(0.08f);
            if (!_isSpeaking) break;

            // → 闭口
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
}
