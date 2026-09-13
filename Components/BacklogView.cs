using System.Collections;
using NanoUint.Drawing;

namespace NanoUint;

/// <summary>A single dialogue backlog entry.</summary>
public sealed record BacklogEntry(string SpeakerName, string Text, string? VoicePath);

/// <summary>Dialogue backlog view that records all dialogue and replays a line's voice when clicked.</summary>
[Obsolete("Use NanoUintVN.Components.BacklogView instead.")]
public sealed class BacklogView : Behaviour
{
    private readonly List<BacklogEntry> _entries = new();
    private bool _isOpen;
    private int _scrollOffset;

    /// <summary>List of backlog entries.</summary>
    public IReadOnlyList<BacklogEntry> Entries => _entries;

    /// <summary>Whether the backlog view is open.</summary>
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

    /// <summary>Current scroll offset in lines.</summary>
    public int ScrollOffset
    {
        get => _scrollOffset;
        set { _scrollOffset = Math.Max(0, value); MarkDirty(); }
    }

    /// <summary>Adds a dialogue entry.</summary>
    public void Add(string speaker, string text, string? voicePath = null)
    {
        _entries.Add(new BacklogEntry(speaker, text, voicePath));
        if (!_isOpen) MarkDirty();
    }

    /// <summary>Clears the history.</summary>
    public void Clear()
    {
        _entries.Clear();
        _scrollOffset = 0;
        _isOpen = false;
        MarkDirty();
    }

    /// <summary>Opens the backlog view.</summary>
    public void Open()
    {
        _isOpen = true;
        _scrollOffset = Math.Max(0, _entries.Count - 12); // Scroll to the bottom by default
        MarkDirty();
    }

    /// <summary>Closes the backlog view.</summary>
    public void Close()
    {
        _isOpen = false;
        MarkDirty();
    }

    /// <summary>Gets the backlog entry at the given index.</summary>
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
