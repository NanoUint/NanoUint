namespace NanoUint;

/// <summary>Slider control for continuous values such as volume or speed.</summary>
public sealed class Slider : Behaviour
{
    private string _label = "";
    private float _value = 0.5f;
    private float _minValue;
    private float _maxValue = 1f;
    private float _step = 0.05f;

    /// <summary>Slider label, shown before the slider.</summary>
    public string Label
    {
        get => _label;
        set { if (_label != value) { _label = value; MarkDirty(); } }
    }

    /// <summary>Current value, clamped to [MinValue, MaxValue].</summary>
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

    /// <summary>Minimum value.</summary>
    public float MinValue
    {
        get => _minValue;
        set { _minValue = value; if (_value < _minValue) Value = _minValue; }
    }

    /// <summary>Maximum value.</summary>
    public float MaxValue
    {
        get => _maxValue;
        set { _maxValue = value; if (_value > _maxValue) Value = _maxValue; }
    }

    /// <summary>Step size for keyboard adjustment.</summary>
    public float Step
    {
        get => _step;
        set => _step = value;
    }

    /// <summary>Raised when the value changes.</summary>
    public event Action<float>? OnValueChanged;

    private string? _valueTextOverride;

    /// <summary>Optional override for the value text (null = compute the percentage automatically).</summary>
    public string? ValueTextOverride
    {
        get => _valueTextOverride;
        set { if (_valueTextOverride != value) { _valueTextOverride = value; MarkDirty(); } }
    }

    /// <summary>Gets the display text, as a percentage.</summary>
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
