namespace NanoUint.Models;

/// <summary>
/// 表示一个包含完整游戏状态的存档槽位。
/// </summary>
public class SaveData
{
    /// <summary>槽位索引（从0开始）</summary>
    public int SlotIndex { get; set; }

    /// <summary>存档创建的时间戳</summary>
    public DateTime SaveTime { get; set; } = DateTime.Now;

    /// <summary>当前章节/场景的名称</summary>
    public string ChapterName { get; set; } = string.Empty;

    /// <summary>当前对话序列中的索引</summary>
    public int DialogueIndex { get; set; }

    /// <summary>当前场景/背景路径</summary>
    public string? CurrentBackground { get; set; }

    /// <summary>当前 BGM 路径</summary>
    public string? CurrentBGM { get; set; }

    /// <summary>用于分支逻辑的游戏标记</summary>
    public Dictionary<string, bool> Flags { get; set; } = new();

    /// <summary>玩家已做出的选择（用于分支路径）</summary>
    public List<string> ChoiceHistory { get; set; } = new();

    /// <summary>存档时的总游戏时间</summary>
    public TimeSpan PlayTime { get; set; }

    /// <summary>脚本引擎状态的 JSON（步骤索引、变量、标记）</summary>
    public string? ScriptStateJson { get; set; }

    /// <summary>存档截图的 Base64 缩略图（占位符）</summary>
    public string? ThumbnailBase64 { get; set; }

    /// <summary>适合显示的存档描述</summary>
    public string GetDescription()
    {
        return $"{SaveTime:yyyy/MM/dd HH:mm} - {ChapterName}";
    }
}
