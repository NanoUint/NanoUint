namespace NanoUintVN.Dialogue;

/// <summary>A single page in a VN document. Contains elements and dialogue beats.</summary>
public sealed class VNPage
{
    public string PageId { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public List<VNElement> Elements { get; init; } = new();
    public List<DialogueBeat> Beats { get; init; } = new();
    public PageTransition EntryTransition { get; init; } = PageTransition.None;
    public PageAdvance Advance { get; init; } = PageAdvance.Click;

    private ChoiceElement? _choice;

    public BackgroundElement? Background => Elements.OfType<BackgroundElement>().FirstOrDefault();
    public IReadOnlyList<SpriteElement> Sprites => Elements.OfType<SpriteElement>().ToList();
    public BgmElement? Bgm => Elements.OfType<BgmElement>().FirstOrDefault();
    public ChoiceElement? Choice
    {
        get => _choice ?? Elements.OfType<ChoiceElement>().FirstOrDefault();
        init => _choice = value;
    }
}
