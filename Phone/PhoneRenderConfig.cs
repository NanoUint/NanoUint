namespace NanoUint;

/// <summary>Configuration for phone rendering.</summary>
public sealed class PhoneRenderConfig
{

    #region Theme Colors (0xAARRGGBB)

    public uint TextPrimary   = 0xFFFFFFFF;
    public uint TextSecondary = 0xFF888888;
    public uint BgPrimary     = 0xFF000000;
    public uint BgSecondary   = 0xFF1A1A1A;
    public uint Accent        = 0xFFFFFFFF;
    public uint Divider       = 0xFF444444;

    #endregion

    #region Frame Dimensions

    public int FrameWidth  = 533;
    public int FrameHeight = 1045;
    public int ScreenX     = 42;
    public int ScreenY     = 134;
    public int ScreenW     = 449;
    public int ScreenH     = 777;
    public double Scale    = 0.48;
    public int ZOrder   = 800;

    #endregion

    #region Animation

    public int SlideFrameCount = 14;
    public double SlideDurationSec = 0.6;

    #endregion

    #region Asset Paths (relative to Resources/System/)

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

    #region Contact Avatar Map (contact name → profile path)
    public Dictionary<string, string> ContactAvatars = new(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region Stamp Map (sticker name → stamp path)
    public Dictionary<string, string> RineStamps = new(StringComparer.OrdinalIgnoreCase);
    #endregion
}
