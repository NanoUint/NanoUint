namespace NanoUintVN.Dialogue;

/// <summary>A complete VN document (chapter) containing ordered pages.</summary>
public sealed class VNDocument
{
    public string DocumentId { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = string.Empty;
    public List<VNPage> Pages { get; init; } = new();

    public VNPage? GetPage(string pageId) => Pages.FirstOrDefault(p => p.PageId == pageId);
    public int GetPageIndex(string pageId) => Pages.FindIndex(p => p.PageId == pageId);
}
