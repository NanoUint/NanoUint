namespace NanoUint;

/// <summary>滑动条控件。用于设置面板的音量/速度等连续值调节。</summary>
public sealed class Slider : Behaviour
{
    private string _label = "";
    private float _value = 0.5f;
    private float _minValue;
    private float _maxValue = 1f;
    private float _step = 0.05f;

    /// <summary>滑块标签（显示在滑块前）。</summary>
    public string Label
    {
        get => _label;
        set { if (_label != value) { _label = value; MarkDirty(); } }
    }

    /// <summary>当前值 [MinValue, MaxValue]。</summary>
    public float Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, _minValue, _maxValue);
            if (Math.Abs(_value - clamped) > 0.0001f)
            {
                _value = clamped;
                MarkDirty();
                OnValueChanged?.Invoke(_value);
            }
        }
    }

    /// <summary>最小值。</summary>
    public float MinValue
    {
        get => _minValue;
        set { _minValue = value; if (_value < _minValue) Value = _minValue; }
    }

    /// <summary>最大值。</summary>
    public float MaxValue
    {
        get => _maxValue;
        set { _maxValue = value; if (_value > _maxValue) Value = _maxValue; }
    }

    /// <summary>步进值（键盘微调）。</summary>
    public float Step
    {
        get => _step;
        set => _step = value;
    }

    /// <summary>值变更事件。</summary>
    public event Action<float>? OnValueChanged;

    private string? _valueTextOverride;

    /// <summary>可选的值显示文本覆盖（null 则自动计算百分比）。</summary>
    public string? ValueTextOverride
    {
        get => _valueTextOverride;
        set { if (_valueTextOverride != value) { _valueTextOverride = value; MarkDirty(); } }
    }

    /// <summary>获取百分比形式的显示文本。</summary>
    public string ValueText
    {
        get
        {
            if (ValueTextOverride != null) return ValueTextOverride;
            if (_maxValue <= 1f && _minValue == 0f && _step >= 0.01f)
                return $"{(int)(_value * 100)}%";
            return $"{_value:F2}";
        }
    }

    public override string ToString() =>
        $"Slider [{_label}: {ValueText}] ({_minValue}~{_maxValue})";
}
