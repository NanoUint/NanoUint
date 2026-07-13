namespace NanoUint.Models;

/// <summary>
/// 表示视觉小说中的一个角色，包含其精灵图和元数据。
/// </summary>
public class CharacterData
{
    /// <summary>此角色的唯一标识符</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>角色显示名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>默认精灵图路径</summary>
    public string? DefaultSprite { get; set; }

    /// <summary>表情名称 → 精灵图路径的字典</summary>
    public Dictionary<string, string> Expressions { get; set; } = new();

    /// <summary>屏幕上的默认位置</summary>
    public CharacterPosition DefaultPosition { get; set; } = CharacterPosition.Center;

    /// <summary>根据表情名称获取精灵图路径，找不到时回退到默认值</summary>
    public string? GetSprite(string? expression = null)
    {
        if (expression != null && Expressions.TryGetValue(expression, out var sprite))
            return sprite;
        return DefaultSprite;
    }
}
