using System.Windows.Media;

namespace NanoUint.Debugging.UE;

/// <summary>UnityExplorer 1:1 复刻配色表。</summary>
internal static class UEPalette
{

    #region 全局按钮
    /// <summary>按钮启用/选中色 (0.2, 0.4, 0.28) — 顶栏 tab 选中态、面板 tab 选中态。</summary>
    public static readonly Color ButtonEnabled = Rgb(0x33, 0x66, 0x47);
    /// <summary>按钮禁用/未选色 (0.25) — 未选中 tab。</summary>
    public static readonly Color ButtonDisabled = Rgb(0x40, 0x40, 0x40);

    #endregion

    #region 顶栏 (UIManager)
    /// <summary>顶栏背景 (0.1, 0.1, 0.1)。</summary>
    public static readonly Color TopBarBackground = Rgb(0x1A, 0x1A, 0x1A);
    public static readonly Color TopBarCloseNormal = Rgb(0xA1, 0x52, 0x4F);
    public static readonly Color TopBarCloseHighlight = Rgb(0xCF, 0x40, 0x33);
    public static readonly Color TopBarClosePressed = Rgb(0x99, 0x2E, 0x29);

    #endregion

    #region 面板基座 (PanelBase)
    /// <summary>标题栏背景 (0.06)。</summary>
    public static readonly Color PanelTitleBar = Rgb(0x0F, 0x0F, 0x0F);
    /// <summary>标题栏 "—" 关闭按钮 (0.33, 0.32, 0.31)。</summary>
    public static readonly Color PanelCloseButton = Rgb(0x54, 0x52, 0x4F);

    #endregion

    #region 面板内容
    /// <summary>面板内容区 (0.1)。</summary>
    public static readonly Color PanelContent = Rgb(0x1A, 0x1A, 0x1A);
    /// <summary>Inspector tab 条 GridGroup 背景 (0.05)。</summary>
    public static readonly Color TabBarBackground = Rgb(0x0D, 0x0D, 0x0D);
    /// <summary>Inspector tab 选中 (0.15, 0.22, 0.15)。</summary>
    public static readonly Color TabSelected = Rgb(0x26, 0x38, 0x26);
    /// <summary>Inspector tab 未选中 (0.13)。</summary>
    public static readonly Color TabUnselected = Rgb(0x21, 0x21, 0x21);
    /// <summary>Inspector tab 的 X 按钮背景 (0.15)。</summary>
    public static readonly Color TabCloseButton = Rgb(0x26, 0x26, 0x26);

    #endregion

    #region Inspector (ReflectionInspector)
    /// <summary>ReflectionInspector 根背景 (0.065)。</summary>
    public static readonly Color InspectorRoot = Rgb(0x11, 0x11, 0x11);
    /// <summary>TopRow / 内容 (0.12)。</summary>
    public static readonly Color InspectorTopRow = Rgb(0x1F, 0x1F, 0x1F);
    /// <summary>滚动区 (0.09)。</summary>
    public static readonly Color InspectorScroll = Rgb(0x17, 0x17, 0x17);
    /// <summary>边框 (0.05)。</summary>
    public static readonly Color InspectorBorder = Rgb(0x0D, 0x0D, 0x0D);
    /// <summary>Scope 按钮选中 (0.2, 0.27, 0.2)。</summary>
    public static readonly Color ScopeSelected = Rgb(0x33, 0x45, 0x33);
    /// <summary>Scope 按钮未选 (0.24)。</summary>
    public static readonly Color ScopeUnselected = Rgb(0x3D, 0x3D, 0x3D);
    /// <summary>"Update displayed values" 按钮 (0.22, 0.28, 0.22)。</summary>
    public static readonly Color UpdateButton = Rgb(0x38, 0x47, 0x38);
    /// <summary>Destroy 按钮 (0.3, 0.2, 0.2)。</summary>
    public static readonly Color DestroyButton = Rgb(0x4D, 0x33, 0x33);
    /// <summary>普通按钮 (0.2)。</summary>
    public static readonly Color NormalButton = Rgb(0x33, 0x33, 0x33);
    /// <summary>次要按钮 "Show in Explorer" (0.15)。</summary>
    public static readonly Color SubtleButton = Rgb(0x26, 0x26, 0x26);
    /// <summary>成员输入框背景 (0.12)。</summary>
    public static readonly Color InputBackground = Rgb(0x1F, 0x1F, 0x1F);
    /// <summary>输入框边框 (0.25)。</summary>
    public static readonly Color InputBorder = Rgb(0x40, 0x40, 0x40);

    #endregion

    #region 组件行 (ComponentCell)
    /// <summary>Behaviour 启用 toggle 图形 (0.8, 1, 0.8, 0.3) 半透明绿。</summary>
    public static readonly Color BehaviourToggleGraphic = Argb(0x4D, 0xCC, 0xFF, 0xCC);

    #endregion

    #region Object Explorer (SceneExplorer / TransformCell)
    /// <summary>工具栏 (0.15)。</summary>
    public static readonly Color Toolbar = Rgb(0x26, 0x26, 0x26);
    /// <summary>树滚动区 (0.11)。</summary>
    public static readonly Color TreeScroll = Rgb(0x1C, 0x1C, 0x1C);
    /// <summary>树节点名称按钮 normal (0.11)。</summary>
    public static readonly Color TreeNodeNormal = Rgb(0x1C, 0x1C, 0x1C);
    /// <summary>树节点名称按钮 highlight (0.25)。</summary>
    public static readonly Color TreeNodeHover = Rgb(0x40, 0x40, 0x40);
    /// <summary>树节点名称按钮 pressed (0.05)。</summary>
    public static readonly Color TreeNodePressed = Rgb(0x0D, 0x0D, 0x0D);
    /// <summary>展开箭头 (0.5) ▼。</summary>
    public static readonly Color ArrowExpanded = Rgb(0x80, 0x80, 0x80);
    /// <summary>收起箭头 (0.3) ► / 无子 ▪。</summary>
    public static readonly Color ArrowCollapsed = Rgb(0x4D, 0x4D, 0x4D);
    /// <summary>Sibling index 输入框 (0, 0, 0, 0.25) 半透明黑。</summary>
    public static readonly Color SiblingInputBackground = Argb(0x40, 0x00, 0x00, 0x00);
    /// <summary>Load (Single/Additive) 按钮 (0.1, 0.3, 0.3)。</summary>
    public static readonly Color LoadButton = Rgb(0x1A, 0x4D, 0x4D);
    /// <summary>过滤输入框 normal (0.4)。</summary>
    public static readonly Color FilterNormal = Rgb(0x66, 0x66, 0x66);
    /// <summary>过滤输入框 highlight (0.2)。</summary>
    public static readonly Color FilterHighlight = Rgb(0x33, 0x33, 0x33);
    /// <summary>过滤输入框 pressed (0.08)。</summary>
    public static readonly Color FilterPressed = Rgb(0x14, 0x14, 0x14);

    #endregion

    #region Log 面板 (LogPanel)
    /// <summary>日志滚动区 (0.03)。</summary>
    public static readonly Color LogScroll = Rgb(0x08, 0x08, 0x08);
    /// <summary>日志行交替色 A (0.34)。</summary>
    public static readonly Color LogRowAltA = Rgb(0x57, 0x57, 0x57);
    /// <summary>日志行交替色 B (0.28)。</summary>
    public static readonly Color LogRowAltB = Rgb(0x47, 0x47, 0x47);
    /// <summary>日志消息输入框 image (0.2)。</summary>
    public static readonly Color LogInputImage = Rgb(0x33, 0x33, 0x33);
    public static readonly Color LogInputNormal = Rgb(0x1A, 0x1A, 0x1A);
    public static readonly Color LogInputHighlight = Rgb(0x21, 0x21, 0x21);
    public static readonly Color LogInputPressed = Rgb(0x12, 0x12, 0x12);
    /// <summary>Log 级别文字: 白。</summary>
    public static readonly Color LogInfo = Colors.White;
    /// <summary>Warning / Assert: 黄。</summary>
    public static readonly Color LogWarning = Colors.Yellow;
    /// <summary>Error / Exception: 红。</summary>
    public static readonly Color LogError = Colors.Red;
    /// <summary>日志索引标签灰色。</summary>
    public static readonly Color LogIndex = Rgb(0x9C, 0x9C, 0x9C);

    #endregion

    #region 文本
    public static readonly Color TextDefault = Colors.White;
    /// <summary>非激活对象/灰色辅助文本。</summary>
    public static readonly Color TextInactive = Rgb(0x9C, 0x9C, 0x9C);
    /// <summary>已销毁对象 [Destroyed] 红。</summary>
    public static readonly Color TextDestroyed = Colors.Red;
    /// <summary>Copy 按钮文字黄。</summary>
    public static readonly Color TextYellow = Colors.Yellow;
    /// <summary>"Scene:" 标签 cyan。</summary>
    public static readonly Color TextCyan = Colors.Cyan;

    #endregion

    #region 签名高亮 (SignatureHighlighter 近似)
    public static readonly Color SigClass = Rgb(0x5F, 0xB3, 0x5F);      // 类/结构/枚举/接口/委托
    public static readonly Color SigField = Rgb(0xE8, 0x9C, 0x4A);      // 字段/常量
    public static readonly Color SigProperty = Rgb(0x4F, 0xC1, 0xFF);   // 属性/索引器
    public static readonly Color SigMethod = Rgb(0xD2, 0x9B, 0xFF);     // 方法/构造函数
    public static readonly Color SigKeyword = Rgb(0x56, 0x9C, 0xD6);    // 关键字/基元类型
    public static readonly Color SigGeneric = Rgb(0x9C, 0xD9, 0xFE);    // 泛型参数
    public static readonly Color SigNamespace = Rgb(0x8A, 0x8A, 0x8A);  // 命名空间

    #endregion

    #region 字体
    /// <summary>默认字体 (UnityExplorer 默认 Arial)。</summary>
    public static readonly FontFamily DefaultFont = new("Arial");
    /// <summary>控制台/日志字体。</summary>
    public static readonly FontFamily ConsoleFont = new("Consolas");

    #endregion

    #region 尺寸常量
    /// <summary>面板标题栏高度 (UnityExplorer 25px)。</summary>
    public const double TitleBarHeight = 25;
    /// <summary>缩放边缘热区厚度 (PanelDragger RESIZE_THICKNESS = 10)。</summary>
    public const double ResizeThickness = 10;
    /// <summary>顶栏高度。</summary>
    public const double TopBarHeight = 35;

    /// <summary>0-255 RGB → WPF Color。</summary>
    internal static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    /// <summary>0-255 ARGB → WPF Color。</summary>
    internal static Color Argb(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);
    #endregion
}
