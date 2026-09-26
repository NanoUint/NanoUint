namespace NanoUintVN.History;

/// <summary>In-memory read history. Used by tests and by sessions that should not persist.</summary>
public sealed class MemoryReadHistory : IReadHistory
{
    private readonly HashSet<string> _read = new(StringComparer.Ordinal);

    public int Count => _read.Count;

    public IReadOnlyCollection<string> All => _read;

    public bool HasRead(string lineKey) => !string.IsNullOrEmpty(lineKey) && _read.Contains(lineKey);

    public void MarkRead(string lineKey)
    {
        if (!string.IsNullOrEmpty(lineKey)) _read.Add(lineKey);
    }

    public bool Save() => true;

    public void Clear() => _read.Clear();

    /// <summary>Seeds the record, replacing whatever was there.</summary>
    public void Load(IEnumerable<string> lineKeys)
    {
        _read.Clear();
        foreach (var key in lineKeys)
        {
            if (!string.IsNullOrEmpty(key)) _read.Add(key);
        }
    }
}
