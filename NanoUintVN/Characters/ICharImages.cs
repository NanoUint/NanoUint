namespace NanoUintVN.Characters;

/// <summary>Portrait lookup for one character, keyed by expression.</summary>
public interface ICharImages
{
    /// <summary>Every expression key this character can show.</summary>
    IReadOnlyCollection<string> Expressions { get; }

    /// <summary>Returns the asset path for an expression, or null when it is not available.</summary>
    string? GetImage(string expression);
}

/// <summary>Empty lookup used by characters that have no portraits.</summary>
public sealed class EmptyCharImages : ICharImages
{
    public static readonly EmptyCharImages Instance = new();

    private EmptyCharImages() { }

    public IReadOnlyCollection<string> Expressions => Array.Empty<string>();

    public string? GetImage(string expression) => null;
}
