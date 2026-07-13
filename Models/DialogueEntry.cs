using System.Windows.Media;

namespace NanoUint.Models;

/// <summary>
/// 角色精灵图在屏幕上的位置。
/// </summary>
public enum CharacterPosition
{
    Left,
    Center,
    Right,
    OffscreenLeft,
    OffscreenRight
}

/// <summary>
/// 视觉小说中的一条对白条目。
/// 仅中文文本。
/// </summary>
public class DialogueEntry
{
    /// <summary>说话者姓名</summary>
    public string Speaker { get; set; } = string.Empty;

    /// <summary>对白文本</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>角色精灵图图像路径（相对于游戏资源目录）</summary>
    public string? CharacterSprite { get; set; }

    /// <summary>角色在屏幕上出现的位置</summary>
    public CharacterPosition CharacterPosition { get; set; } = CharacterPosition.Center;

    /// <summary>背景图像路径（相对于游戏资源目录）</summary>
    public string? BackgroundImage { get; set; }

    /// <summary>BGM 或语音的音频文件路径</summary>
    public string? AudioFile { get; set; }

    /// <summary>此音频是 BGM（循环播放）还是语音台词</summary>
    public bool IsBGM { get; set; } = false;

    /// <summary>本条目的对话文本框颜色</summary>
    public Color DialogueBoxColor { get; set; } = Color.FromRgb(10, 10, 10);

    /// <summary>是否显示角色名称标签</summary>
    public bool ShowSpeakerName { get; set; } = true;

    /// <summary>淡入淡出过渡时长（毫秒）</summary>
    public int TransitionMs { get; set; } = 500;

    /// <summary>要播放的特殊效果（震动、闪屏等）</summary>
    public string? Effect { get; set; }
}
