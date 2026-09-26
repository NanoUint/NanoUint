namespace NanoUintVN.Characters;

/// <summary>One line of dialogue inside a conversation.</summary>
public sealed record ConversationLine(
    string SpeakerId,
    string Text,
    string? VoiceKey = null,
    string? Expression = null);

/// <summary>
/// An ordered exchange of lines. Game code supplies the content; the engine only walks it,
/// which keeps authored dialogue out of the VN layer.
/// </summary>
public interface IConversation
{
    string Id { get; }

    IReadOnlyList<ConversationLine> Lines { get; }

    /// <summary>Index of the current line, or -1 before the first advance.</summary>
    int Position { get; }

    bool IsComplete { get; }

    ConversationLine? Current { get; }

    /// <summary>Moves to the next line. Returns false once the conversation is finished.</summary>
    bool Advance();

    void Reset();
}
