using System.Collections;

namespace NanoUint;

/// <summary>Continue indicator that prompts the player to press Enter.</summary>
[Obsolete("Use NanoUintVN.Components.AdvanceIndicator instead.")]
public sealed class AdvanceIndicator : Behaviour
{
    private bool _visible;

    /// <summary>Whether the indicator is visible.</summary>
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
