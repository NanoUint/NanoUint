using NanoUint;

namespace NanoUintVN.Components;

/// <summary>A single dialogue backlog entry.</summary>
public sealed record BacklogEntry(string SpeakerName, string Text, string? VoicePath);

/// <summary>Dialogue backlog view that records all dialogue.</summary>
public sealed class BacklogView : Behaviour
{
    private readonly List<BacklogEntry> _entries = new();
    private bool _isOpen;
    private int _scrollOffset;

    public IReadOnlyList<BacklogEntry> Entries => _entries;

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

    public int ScrollOffset
    {
        get => _scrollOffset;
        set { _scrollOffset = Math.Max(0, value); MarkDirty(); }
    }

    public void Add(string speaker, string text, string? voicePath = null)
    {
        _entries.Add(new BacklogEntry(speaker, text, voicePath));
        if (!_isOpen) MarkDirty();
    }

    public void Clear()
    {
        _entries.Clear();
        _scrollOffset = 0;
        _isOpen = false;
        MarkDirty();
    }

    public void Open()
    {
        _isOpen = true;
        _scrollOffset = Math.Max(0, _entries.Count - 12);
        MarkDirty();
    }

    public void Close()
    {
        _isOpen = false;
        MarkDirty();
    }

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
