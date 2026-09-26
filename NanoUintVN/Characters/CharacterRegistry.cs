namespace NanoUintVN.Characters;

/// <summary>Lookup surface over the registered cast.</summary>
public interface ICharacterRegistry
{
    IReadOnlyCollection<Character> All { get; }

    Character? Find(string id);

    Character? FindByDisplayName(string displayName);
}

/// <summary>Default in-memory registry. The game registers its cast here once at startup.</summary>
public sealed class CharacterRegistry : ICharacterRegistry
{
    private readonly Dictionary<string, Character> _byId = new(StringComparer.Ordinal);

    public int Count => _byId.Count;

    public IReadOnlyCollection<Character> All => _byId.Values;

    public void Register(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        _byId[character.Id] = character;
    }

    public bool Unregister(string id) => _byId.Remove(id);

    public Character? Find(string id)
        => string.IsNullOrEmpty(id) ? null : _byId.GetValueOrDefault(id);

    public Character? FindByDisplayName(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return null;
        foreach (var c in _byId.Values)
        {
            if (string.Equals(c.DisplayName, displayName, StringComparison.Ordinal)) return c;
        }
        return null;
    }

    public void Clear() => _byId.Clear();
}
