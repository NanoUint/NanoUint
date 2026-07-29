namespace NanoUint;

/// <summary>
/// 选择分支组件。挂载到 GameObject 上以显示多个选项按钮。
/// </summary>
public sealed class ChoiceGroup : Component
{
    private string[] _choiceTexts = Array.Empty<string>();
    private int _selectedIndex = -1;
    private TaskCompletionSource<int>? _tcs;

    /// <summary>选项文本数组。</summary>
    public string[]? ChoiceTexts => _choiceTexts;

    /// <summary>选项数量。</summary>
    public int ChoiceCount => _choiceTexts.Length;

    /// <summary>玩家选择的索引（-1 表示未选择）。</summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>是否有选择结果。</summary>
    public bool HasResult => _selectedIndex >= 0;

    /// <summary>当玩家做出选择时触发。</summary>
    public event Action<int>? OnChoiceSelected;

    /// <summary>显示选项。标记为 dirty 以触发重建按钮。</summary>
    public void Show(string[] choices)
    {
        _choiceTexts = choices ?? Array.Empty<string>();
        _selectedIndex = -1;
        _tcs = new TaskCompletionSource<int>();
        MarkDirty();
        Debug.Log($"ChoiceGroup: showing {_choiceTexts.Length} choices");
    }

    /// <summary>由 WpfRenderer 的按钮点击事件调用。</summary>
    internal void Select(int index)
    {
        if (index < 0 || index >= _choiceTexts.Length) return;
        _selectedIndex = index;
        _tcs?.TrySetResult(index);
        OnChoiceSelected?.Invoke(index);
        Debug.Log($"ChoiceGroup: selected [{index}] '{_choiceTexts[index]}'");
    }

    /// <summary>异步等待玩家选择。</summary>
    public Task<int> WaitForChoiceAsync()
    {
        _tcs ??= new TaskCompletionSource<int>();
        return _tcs.Task;
    }

    /// <summary>隐藏选项。</summary>
    public void Hide()
    {
        _choiceTexts = Array.Empty<string>();
        _selectedIndex = -1;
        MarkDirty();
    }

    public override string ToString() =>
        $"ChoiceGroup ({_choiceTexts.Length} choices, selected={(HasResult ? _selectedIndex.ToString() : "none")})";
}
