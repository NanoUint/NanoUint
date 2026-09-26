namespace NanoUintVN.History;

/// <summary>
/// Record of which lines the player has already read. Read history outlives save files and
/// playthroughs, which is what makes "skip read text only" meaningful.
/// </summary>
public interface IReadHistory
{
    int Count { get; }

    IReadOnlyCollection<string> All { get; }

    bool HasRead(string lineKey);

    void MarkRead(string lineKey);

    /// <summary>Persists the current record. Returns false when the backing store rejected it.</summary>
    bool Save();

    void Clear();
}
