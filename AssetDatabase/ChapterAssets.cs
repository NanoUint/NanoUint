namespace NanoUint;

/// <summary>章节资源清单。每个章节声明需要的资源路径，供启动前预加载。</summary>
public class ChapterAssets
{
    /// <summary>背景图片路径列表</summary>
    public List<string> Backgrounds { get; set; } = new();

    /// <summary>角色立绘路径列表</summary>
    public List<string> Sprites { get; set; } = new();

    /// <summary>背景音乐路径列表</summary>
    public List<string> BGM { get; set; } = new();

    /// <summary>音效路径列表</summary>
    public List<string> SFX { get; set; } = new();

    /// <summary>语音路径列表</summary>
    public List<string> Voice { get; set; } = new();

    /// <summary>所有图片路径（背景 + 立绘）</summary>
    public IEnumerable<string> AllImagePaths => Backgrounds.Concat(Sprites);

    /// <summary>将所有图片路径解析为绝对文件路径。</summary>
    public IEnumerable<string> ResolveAllImages()
    {
        foreach (var p in AllImagePaths)
        {
            var full = AssetDatabase.GetFullPath(p);
            if (full != null) yield return full;
        }
    }

    /// <summary>将所有音频路径解析为绝对文件路径（BGM + SFX + Voice）。</summary>
    public IEnumerable<string> ResolveAllAudio()
    {
        foreach (var list in new[] { BGM, SFX, Voice })
            foreach (var p in list)
            {
                var full = AssetDatabase.GetFullPath(p);
                if (full != null) yield return full;
            }
    }

    /// <summary>总图片数</summary>
    public int TotalImageCount => Backgrounds.Count + Sprites.Count;

    /// <summary>总音频数</summary>
    public int TotalAudioCount => BGM.Count + SFX.Count + Voice.Count;

    /// <summary>合并另一个资源清单</summary>
    public void Merge(ChapterAssets other)
    {
        Backgrounds.AddRange(other.Backgrounds.Where(b => !Backgrounds.Contains(b)));
        Sprites.AddRange(other.Sprites.Where(s => !Sprites.Contains(s)));
        BGM.AddRange(other.BGM.Where(b => !BGM.Contains(b)));
        SFX.AddRange(other.SFX.Where(s => !SFX.Contains(s)));
        Voice.AddRange(other.Voice.Where(v => !Voice.Contains(v)));
    }
}
