namespace NanoUint;

/// <summary>Displays multiple choice buttons.</summary>
public sealed class ChoiceGroup : Component
{
    private string[] _choiceTexts = Array.Empty<string>();
    private int _selectedIndex = -1;
    private TaskCompletionSource<int>? _tcs;

    /// <summary>Array of choice texts.</summary>
    public string[]? ChoiceTexts => _choiceTexts;

    /// <summary>Number of choices.</summary>
    public int ChoiceCount => _choiceTexts.Length;

    /// <summary>Index chosen by the player (-1 = none).</summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>Whether a choice has been made.</summary>
    public bool HasResult => _selectedIndex >= 0;

    /// <summary>Whether to lay out choices inline instead of a full-screen centered menu.</summary>
    public bool Inline { get; set; }

    /// <summary>Highlighted index; -1 = no highlight.</summary>
    public int HighlightIndex { get; set; } = -1;

    /// <summary>Raised when the player makes a choice.</summary>
    public event Action<int>? OnChoiceSelected;

    /// <summary>Shows the choices.</summary>
    public void Show(string[] choices)
    {
        _choiceTexts = choices ?? Array.Empty<string>();
        _selectedIndex = -1;
        _tcs = new TaskCompletionSource<int>();
        MarkDirty();
        Debug.Log($"ChoiceGroup: showing {_choiceTexts.Length} choices");
    }

    internal void Select(int index)
    {
        if (index < 0 || index >= _choiceTexts.Length) return;
        _selectedIndex = index;
        _tcs?.TrySetResult(index);
        OnChoiceSelected?.Invoke(index);
        Debug.Log($"ChoiceGroup: selected [{index}] '{_choiceTexts[index]}'");
    }

    /// <summary>Asynchronously waits for the player's choice.</summary>
    public Task<int> WaitForChoiceAsync()
    {
        _tcs ??= new TaskCompletionSource<int>();
        return _tcs.Task;
    }

    /// <summary>Hides the choices.</summary>
    public void Hide()
    {
        _choiceTexts = Array.Empty<string>();
        _selectedIndex = -1;
        MarkDirty();
    }

    public override string ToString() =>
        $"ChoiceGroup ({_choiceTexts.Length} choices, selected={(HasResult ? _selectedIndex.ToString() : "none")})";
}
