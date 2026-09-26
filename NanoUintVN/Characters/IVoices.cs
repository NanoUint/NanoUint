namespace NanoUintVN.Characters;

/// <summary>Voice clip lookup for one character, keyed by line identifier.</summary>
public interface IVoices
{
    /// <summary>Every line identifier that has a recorded voice clip.</summary>
    IReadOnlyCollection<string> LineIds { get; }

    /// <summary>Returns the voice asset path for a line, or null when it is not voiced.</summary>
    string? GetVoice(string lineId);
}
