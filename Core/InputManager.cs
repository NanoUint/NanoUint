namespace NanoUint;

/// <summary>
/// 输入管理器。类似 Unity 的 Input，提供跨平台的输入抽象。
/// 由引擎内部的 WPF 宿主向它喂事件。
/// </summary>
public static class InputManager
{
    private static bool _advancePressedThisFrame;
    private static bool _skipHeld;
    private static bool _autoToggledThisFrame;
    private static bool _menuPressedThisFrame;

    // ── 全局热键事件（由游戏层订阅） ──

    /// <summary>F5 快速存档请求。</summary>
    public static event Action? QuickSaveRequested;

    /// <summary>F9 快速读档请求。</summary>
    public static event Action? QuickLoadRequested;

    /// <summary>Esc/右键 系统菜单请求。</summary>
    public static event Action? SystemMenuRequested;

    /// <summary>Page Up Backlog 请求。</summary>
    public static event Action? BacklogRequested;

    // ── 触发方法（引擎主机 + 游戏层调用） ──

    public static void FireQuickSave() => QuickSaveRequested?.Invoke();
    public static void FireQuickLoad() => QuickLoadRequested?.Invoke();
    public static void FireSystemMenu() => SystemMenuRequested?.Invoke();
    public static void FireBacklog() => BacklogRequested?.Invoke();

    // ── 由引擎宿主调用 ──

    internal static void FeedAdvancePress()
    {
        _advancePressedThisFrame = true;
    }

    internal static void FeedSkipState(bool held)
    {
        _skipHeld = held;
    }

    internal static void FeedAutoToggle()
    {
        _autoToggledThisFrame = true;
    }

    internal static void FeedMenuPress()
    {
        _menuPressedThisFrame = true;
    }

    internal static void EndFrame()
    {
        _advancePressedThisFrame = false;
        _autoToggledThisFrame = false;
        _menuPressedThisFrame = false;

        // 每帧结束触发累积的热键事件
        if (_menuPressedThisFrame)
            SystemMenuRequested?.Invoke();
    }

    // ── 公开 API ──

    /// <summary>本帧是否按下了"推进"键（空格/回车/鼠标左键）。</summary>
    public static bool IsAdvancePressedThisFrame() => _advancePressedThisFrame;

    /// <summary>消耗本帧的推进输入，防止被多个消费者重复处理（如打字机跳过→WaitForAdvance）。</summary>
    public static void ConsumeAdvancePress()
    {
        _advancePressedThisFrame = false;
    }

    /// <summary>是否正按住"跳过"键（Ctrl）。</summary>
    public static bool IsSkipHeld() => _skipHeld;

    /// <summary>本帧是否切换了自动模式（A 键）。</summary>
    public static bool IsAutoModeToggledThisFrame() => _autoToggledThisFrame;

    /// <summary>本帧是否按了系统菜单键（Esc）。</summary>
    public static bool IsMenuPressedThisFrame() => _menuPressedThisFrame;
}
