using System.Windows.Media;

namespace NanoUint.Services;

/// <summary>
/// 引擎层所需的画面显示操作接口。
/// 由游戏项目（Liminal）实现，注入到引擎命令中。
/// </summary>
public interface IGameDisplay
{
    void SetBackground(string fullPath);
    void ClearBackground();
    void SetSprite(string fullPath, string position, double opacity);
    void ClearSprite();
    void SetSpritePosition(string position);
    void SetSpriteOpacity(double opacity);
}
