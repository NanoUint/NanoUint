namespace NanoUint;

/// <summary>手机渲染配置。由游戏层初始化后注入 PhoneScreen，提供素材路径、主题色、尺寸等。</summary>
public sealed class PhoneRenderConfig
{

    #region 主题色 (0xAARRGGBB)

    public uint TextPrimary   = 0xFFFFFFFF;
    public uint TextSecondary = 0xFF888888;
    public uint BgPrimary     = 0xFF000000;
    public uint BgSecondary   = 0xFF1A1A1A;
    public uint Accent        = 0xFFFFFFFF;
    public uint Divider       = 0xFF444444;

    #endregion

    #region 框体尺寸

    public int FrameWidth  = 533;
    public int FrameHeight = 1045;
    public int ScreenX     = 42;
    public int ScreenY     = 134;
    public int ScreenW     = 449;
    public int ScreenH     = 777;
    public double Scale    = 0.48;
    public int ZOrder   = 800;

    #endregion

    #region 动画

    public int SlideFrameCount = 14;
    public double SlideDurationSec = 0.6;

    #endregion

    #region 素材路径（相对于 Resources/System/）

    public string FramePath   = "";
    public string GlassPath   = "";
    public string DenhaIconPath    = "";
    public string DenhaHighlightPath = "";
    public string RineIconPath     = "";
    public string RineHighlightPath  = "";
    public string SettingsIconPath    = "";
    public string SettingsHighlightPath = "";
    public string DenhaAcceptPath  = "";
    public string DenhaRefusePath  = "";
    public string DenhaCallPath    = "";
    public string DenhaSpeakerPath = "";
    public string DenhaNewPath     = "";
    public string[] DenhaCallingFrames = Array.Empty<string>();
    public string RineBubbleOtherPath   = "";
    public string RineBubbleSelfPath    = "";
    public string RineDefaultAvatarPath = "";
    public string ArrowUpPath    = "";
    public string ArrowDownPath  = "";
    public string ArrowLeftPath  = "";
    public string ArrowRightPath = "";
    public string ArrowSkipPath  = "";
    public string[] WallpaperPaths = Array.Empty<string>();
    public string[] SlideFramePaths = Array.Empty<string>();

    #endregion

    #region 联系人头像映射 (联系人名 → profile路径)
    public Dictionary<string, string> ContactAvatars = new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region Stamp 映射 (贴纸名 → stamp路径)
    public Dictionary<string, string> RineStamps = new(StringComparer.OrdinalIgnoreCase);
    #endregion
}
