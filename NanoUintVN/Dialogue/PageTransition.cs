namespace NanoUintVN.Dialogue;

/// <summary>Transition kind between pages.</summary>
public enum TransitionKind
{
    None,
    FlashBlack,
    FlashWhite,
    Crossfade,
    FadeIn
}

/// <summary>Defines how one page transitions to the next.</summary>
public readonly record struct PageTransition
{
    public TransitionKind Kind { get; init; }
    public float Duration { get; init; }

    public static PageTransition None => new() { Kind = TransitionKind.None, Duration = 0f };
    public static PageTransition FlashBlack(float duration = 0.6f) => new() { Kind = TransitionKind.FlashBlack, Duration = duration };
    public static PageTransition FlashWhite(float duration = 0.6f) => new() { Kind = TransitionKind.FlashWhite, Duration = duration };
    public static PageTransition Crossfade(float duration = 0.8f) => new() { Kind = TransitionKind.Crossfade, Duration = duration };
    public static PageTransition FadeIn(float duration = 0.5f) => new() { Kind = TransitionKind.FadeIn, Duration = duration };
}
