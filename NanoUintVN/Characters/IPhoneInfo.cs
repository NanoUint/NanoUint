namespace NanoUintVN.Characters;

/// <summary>
/// Read-only contract a game exposes for its phone system. The engine never implements a phone:
/// it only reads state, so the phone stays entirely on the game side.
/// </summary>
public interface IPhoneInfo
{
    bool IsEnabled { get; }

    bool IsOpen { get; }

    int UnreadCount { get; }

    IReadOnlyCollection<string> ContactIds { get; }

    /// <summary>Raised when unread count, open state or contacts change.</summary>
    event Action? Changed;
}
