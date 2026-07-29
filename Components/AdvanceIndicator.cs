using System.Collections;

namespace NanoUint;

/// <summary>
/// 继续阅读指示器。在文字框右下角提示玩家按 Enter 继续。
/// 打字机完成后显示闪烁动画，Enter 按下后隐藏。
/// </summary>
public sealed class AdvanceIndicator : Behaviour
{
    private bool _visible;

    /// <summary>指示器是否可见。</summary>
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible != value)
            {
                _visible = value;
                MarkDirty();
                if (_visible) StartBlink();
                else { StopAllCoroutines(); _frame = 0; }
            }
        }
    }

    /// <summary>当前动画帧（0-11，12 帧循环）。</summary>
    internal int CurrentFrame => _frame;
    private int _frame;

    private void StartBlink()
    {
        StopAllCoroutines();
        StartCoroutine(BlinkLoop());
    }

    private IEnumerator BlinkLoop()
    {
        while (_visible)
        {
            _frame = (_frame + 1) % 12;
            MarkDirty();
            yield return new WaitForSeconds(0.08f);
        }
    }

    protected internal override void OnDestroy()
    {
        StopAllCoroutines();
        base.OnDestroy();
    }

    public override string ToString() => $"AdvanceIndicator (visible={_visible}, frame={_frame})";
}
