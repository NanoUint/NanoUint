namespace NanoUint.Services;

/// <summary>
/// 最小化的本地化桩 —— 游戏仅为中文。
/// 保留此模块以兼容现有代码模式。
/// </summary>
public static class LocalizationService
{
    /// <summary>从应用程序资源中获取本地化字符串</summary>
    public static string GetString(string key)
    {
        var found = System.Windows.Application.Current.TryFindResource(key);
        return found as string ?? $"[{key}]";
    }
}
