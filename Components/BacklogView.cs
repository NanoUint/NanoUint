using System.Collections;
using NanoUint.Drawing;

namespace NanoUint;

/// <summary>对话历史条目。</summary>
public sealed record BacklogEntry(string SpeakerName, string Text, string? VoicePath);

/// <summary>对话历史回看组件。记录所有对话，按 Page Up 打开回看，支持点击台词重播配音。</summary>
public sealed class BacklogView : Behaviour
{
    private readonly List<BacklogEntry> _entries = new();
    private bool _isOpen;
    private int _scrollOffset;

    /// <summary>对话历史条目列表。</summary>
    public IReadOnlyList<BacklogEntry> Entries => _entries;

    /// <summary>是否打开回看界面。</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (_isOpen != value)
            {
                _isOpen = value;
                MarkDirty();
            }
        }
    }

    /// <summary>当前滚动偏移（行数）。</summary>
    public int ScrollOffset
    {
        get => _scrollOffset;
        set { _scrollOffset = Math.Max(0, value); MarkDirty(); }
    }

    /// <summary>添加一条对话记录。</summary>
    public void Add(string speaker, string text, string? voicePath = null)
    {
        _entries.Add(new BacklogEntry(speaker, text, voicePath));
        if (!_isOpen) MarkDirty(); // 静默添加
    }

    /// <summary>清空历史（切换场景时调用）。</summary>
    public void Clear()
    {
        _entries.Clear();
        _scrollOffset = 0;
        _isOpen = false;
        MarkDirty();
    }

    /// <summary>打开回看界面。</summary>
    public void Open()
    {
        _isOpen = true;
        _scrollOffset = Math.Max(0, _entries.Count - 12); // 默认滚动到底部
        MarkDirty();
    }

    /// <summary>关闭回看界面。</summary>
    public void Close()
    {
        _isOpen = false;
        MarkDirty();
    }

    /// <summary>获取指定索引的对话条目。</summary>
    public BacklogEntry? GetEntry(int index)
    {
        if (index < 0 || index >= _entries.Count) return null;
        return _entries[index];
    }

    protected internal override void OnDestroy()
    {
        Clear();
        base.OnDestroy();
    }

    public override string ToString() => $"BacklogView ({_entries.Count} entries)";
}
