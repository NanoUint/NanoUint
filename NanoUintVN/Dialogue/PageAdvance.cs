namespace NanoUintVN.Dialogue;

/// <summary>How a page advances to the next.</summary>
public enum PageAdvanceMode
{
    Click,
    Auto,
    Timed
}

/// <summary>Defines how the current page advances.</summary>
public readonly record struct PageAdvance
{
    public PageAdvanceMode Mode { get; init; }
    public float AutoDelay { get; init; }

    public static PageAdvance Click => new() { Mode = PageAdvanceMode.Click };
    public static PageAdvance Auto(float delay = 3f) => new() { Mode = PageAdvanceMode.Auto, AutoDelay = delay };
    public static PageAdvance Timed(float seconds) => new() { Mode = PageAdvanceMode.Timed, AutoDelay = seconds };
}
