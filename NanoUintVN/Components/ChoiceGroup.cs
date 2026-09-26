using NanoUint;

namespace NanoUintVN.Components;

/// <summary>Displays multiple choice buttons. Choice is a page-level element, not a temporary GameObject.</summary>
public sealed class ChoiceGroup : Component
{
    private string[] _choiceTexts = Array.Empty<string>();
    private int _selectedIndex = -1;
    private TaskCompletionSource<int>? _tcs;

    public string[]? ChoiceTexts => _choiceTexts;
    public int ChoiceCount => _choiceTexts.Length;
    public int SelectedIndex => _selectedIndex;
    public bool HasResult => _selectedIndex >= 0;
    public bool Inline { get; set; }
    public int HighlightIndex { get; set; } = -1;

    public event Action<int>? OnChoiceSelected;

    public void Show(string[] choices)
    {
        _choiceTexts = choices ?? Array.Empty<string>();
        _selectedIndex = -1;
        _tcs = new TaskCompletionSource<int>();
        MarkDirty();
    }

    internal void Select(int index)
    {
        if (index < 0 || index >= _choiceTexts.Length) return;
        _selectedIndex = index;
        _tcs?.TrySetResult(index);
        OnChoiceSelected?.Invoke(index);
    }

    public Task<int> WaitForChoiceAsync()
    {
        _tcs ??= new TaskCompletionSource<int>();
        return _tcs.Task;
    }

    public void Hide()
    {
        _choiceTexts = Array.Empty<string>();
        _selectedIndex = -1;
        MarkDirty();
    }

    public override string ToString() =>
        $"ChoiceGroup ({_choiceTexts.Length} choices, selected={(HasResult ? _selectedIndex.ToString() : "none")})";
}
