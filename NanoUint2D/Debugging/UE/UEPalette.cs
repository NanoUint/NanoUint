using System.Windows.Media;

namespace NanoUint.Debugging.UE;

internal static class UEPalette
{

    #region Global buttons
    public static readonly Color ButtonEnabled = Rgb(0x33, 0x66, 0x47);
    public static readonly Color ButtonDisabled = Rgb(0x40, 0x40, 0x40);

    #endregion

    #region Top bar (UIManager)
    public static readonly Color TopBarBackground = Rgb(0x1A, 0x1A, 0x1A);
    public static readonly Color TopBarCloseNormal = Rgb(0xA1, 0x52, 0x4F);
    public static readonly Color TopBarCloseHighlight = Rgb(0xCF, 0x40, 0x33);
    public static readonly Color TopBarClosePressed = Rgb(0x99, 0x2E, 0x29);

    #endregion

    #region Panel base (PanelBase)
    public static readonly Color PanelTitleBar = Rgb(0x0F, 0x0F, 0x0F);
    public static readonly Color PanelCloseButton = Rgb(0x54, 0x52, 0x4F);

    #endregion

    #region Panel content
    public static readonly Color PanelContent = Rgb(0x1A, 0x1A, 0x1A);
    public static readonly Color TabBarBackground = Rgb(0x0D, 0x0D, 0x0D);
    public static readonly Color TabSelected = Rgb(0x26, 0x38, 0x26);
    public static readonly Color TabUnselected = Rgb(0x21, 0x21, 0x21);
    public static readonly Color TabCloseButton = Rgb(0x26, 0x26, 0x26);

    #endregion

    #region Inspector (ReflectionInspector)
    public static readonly Color InspectorRoot = Rgb(0x11, 0x11, 0x11);
    public static readonly Color InspectorTopRow = Rgb(0x1F, 0x1F, 0x1F);
    public static readonly Color InspectorScroll = Rgb(0x17, 0x17, 0x17);
    public static readonly Color InspectorBorder = Rgb(0x0D, 0x0D, 0x0D);
    public static readonly Color ScopeSelected = Rgb(0x33, 0x45, 0x33);
    public static readonly Color ScopeUnselected = Rgb(0x3D, 0x3D, 0x3D);
    public static readonly Color UpdateButton = Rgb(0x38, 0x47, 0x38);
    public static readonly Color DestroyButton = Rgb(0x4D, 0x33, 0x33);
    public static readonly Color NormalButton = Rgb(0x33, 0x33, 0x33);
    public static readonly Color SubtleButton = Rgb(0x26, 0x26, 0x26);
    public static readonly Color InputBackground = Rgb(0x1F, 0x1F, 0x1F);
    public static readonly Color InputBorder = Rgb(0x40, 0x40, 0x40);

    #endregion

    #region Component row (ComponentCell)
    public static readonly Color BehaviourToggleGraphic = Argb(0x4D, 0xCC, 0xFF, 0xCC);

    #endregion

    #region Object Explorer (SceneExplorer / TransformCell)
    public static readonly Color Toolbar = Rgb(0x26, 0x26, 0x26);
    public static readonly Color TreeScroll = Rgb(0x1C, 0x1C, 0x1C);
    public static readonly Color TreeNodeNormal = Rgb(0x1C, 0x1C, 0x1C);
    public static readonly Color TreeNodeHover = Rgb(0x40, 0x40, 0x40);
    public static readonly Color TreeNodePressed = Rgb(0x0D, 0x0D, 0x0D);
    public static readonly Color ArrowExpanded = Rgb(0x80, 0x80, 0x80);
    public static readonly Color ArrowCollapsed = Rgb(0x4D, 0x4D, 0x4D);
    public static readonly Color SiblingInputBackground = Argb(0x40, 0x00, 0x00, 0x00);
    public static readonly Color LoadButton = Rgb(0x1A, 0x4D, 0x4D);
    public static readonly Color FilterNormal = Rgb(0x66, 0x66, 0x66);
    public static readonly Color FilterHighlight = Rgb(0x33, 0x33, 0x33);
    public static readonly Color FilterPressed = Rgb(0x14, 0x14, 0x14);

    #endregion

    #region Log panel (LogPanel)
    public static readonly Color LogScroll = Rgb(0x08, 0x08, 0x08);
    public static readonly Color LogRowAltA = Rgb(0x57, 0x57, 0x57);
    public static readonly Color LogRowAltB = Rgb(0x47, 0x47, 0x47);
    public static readonly Color LogInputImage = Rgb(0x33, 0x33, 0x33);
    public static readonly Color LogInputNormal = Rgb(0x1A, 0x1A, 0x1A);
    public static readonly Color LogInputHighlight = Rgb(0x21, 0x21, 0x21);
    public static readonly Color LogInputPressed = Rgb(0x12, 0x12, 0x12);
    public static readonly Color LogInfo = Colors.White;
    public static readonly Color LogWarning = Colors.Yellow;
    public static readonly Color LogError = Colors.Red;
    public static readonly Color LogIndex = Rgb(0x9C, 0x9C, 0x9C);

    #endregion

    #region Text
    public static readonly Color TextDefault = Colors.White;
    public static readonly Color TextInactive = Rgb(0x9C, 0x9C, 0x9C);
    public static readonly Color TextDestroyed = Colors.Red;
    public static readonly Color TextYellow = Colors.Yellow;
    public static readonly Color TextCyan = Colors.Cyan;

    #endregion

    #region Signature highlighting (SignatureHighlighter approximation)
    public static readonly Color SigClass = Rgb(0x5F, 0xB3, 0x5F);
    public static readonly Color SigField = Rgb(0xE8, 0x9C, 0x4A);
    public static readonly Color SigProperty = Rgb(0x4F, 0xC1, 0xFF);
    public static readonly Color SigMethod = Rgb(0xD2, 0x9B, 0xFF);
    public static readonly Color SigKeyword = Rgb(0x56, 0x9C, 0xD6);
    public static readonly Color SigGeneric = Rgb(0x9C, 0xD9, 0xFE);
    public static readonly Color SigNamespace = Rgb(0x8A, 0x8A, 0x8A);

    #endregion

    #region Fonts
    public static readonly FontFamily DefaultFont = new("Arial");
    public static readonly FontFamily ConsoleFont = new("Consolas");

    #endregion

    #region Size constants
    public const double TitleBarHeight = 25;
    public const double ResizeThickness = 10;
    public const double TopBarHeight = 35;

    internal static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    internal static Color Argb(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);
    #endregion
}
