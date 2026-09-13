namespace NanoUint;

/// <summary>Manifest of the resource paths a chapter needs for preloading.</summary>
public class ChapterAssets
{
    /// <summary>Background image path list.</summary>
    public List<string> Backgrounds { get; set; } = new();

    /// <summary>Character sprite path list.</summary>
    public List<string> Sprites { get; set; } = new();

    /// <summary>BGM path list.</summary>
    public List<string> BGM { get; set; } = new();

    /// <summary>Sound effect path list.</summary>
    public List<string> SFX { get; set; } = new();

    /// <summary>Voice path list.</summary>
    public List<string> Voice { get; set; } = new();

    /// <summary>All image paths.</summary>
    public IEnumerable<string> AllImagePaths => Backgrounds.Concat(Sprites);

    /// <summary>Resolves all image paths to absolute file paths.</summary>
    public IEnumerable<string> ResolveAllImages()
    {
        foreach (var p in AllImagePaths)
        {
            var full = AssetDatabase.GetFullPath(p);
            if (full != null) yield return full;
        }
    }

    /// <summary>Resolves all audio paths to absolute file paths.</summary>
    public IEnumerable<string> ResolveAllAudio()
    {
        foreach (var list in new[] { BGM, SFX, Voice })
            foreach (var p in list)
            {
                var full = AssetDatabase.GetFullPath(p);
                if (full != null) yield return full;
            }
    }

    /// <summary>Total image count.</summary>
    public int TotalImageCount => Backgrounds.Count + Sprites.Count;

    /// <summary>Total audio count.</summary>
    public int TotalAudioCount => BGM.Count + SFX.Count + Voice.Count;

    /// <summary>Merges in another asset manifest.</summary>
    public void Merge(ChapterAssets other)
    {
        Backgrounds.AddRange(other.Backgrounds.Where(b => !Backgrounds.Contains(b)));
        Sprites.AddRange(other.Sprites.Where(s => !Sprites.Contains(s)));
        BGM.AddRange(other.BGM.Where(b => !BGM.Contains(b)));
        SFX.AddRange(other.SFX.Where(s => !SFX.Contains(s)));
        Voice.AddRange(other.Voice.Where(v => !Voice.Contains(v)));
    }
}
