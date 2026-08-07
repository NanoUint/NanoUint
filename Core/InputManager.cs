namespace NanoUint;

/// <summary>输入管理器。提供跨平台的输入抽象。</summary>
public static class InputManager
{
    /// <summary>按键分发委托。参数：Key；返回 true 表示已处理。</summary>
    public static Func<System.Windows.Input.Key, bool>? KeyDispatch;

    private static bool _advancePressedThisFrame;
    private static bool _skipHeld;
    private static bool _autoToggledThisFrame;
    private static bool _menuPressedThisFrame;
    private static bool _upPressedThisFrame;
    private static bool _downPressedThisFrame;
    private static bool _leftPressedThisFrame;
    private static bool _rightPressedThisFrame;

    #region 全局热键事件（由游戏层订阅）

    /// <summary>F5 快速存档请求。</summary>
    public static event Action? QuickSaveRequested;

    /// <summary>F9 快速读档请求。</summary>
    public static event Action? QuickLoadRequested;

    /// <summary>Esc/右键 系统菜单请求。</summary>
    public static event Action? SystemMenuRequested;

    /// <summary>Page Up Backlog 请求。</summary>
    public static event Action? BacklogRequested;

    /// <summary>Shift 隐藏 UI 切换请求。</summary>
    public static event Action? HideUIToggleRequested;

    /// <summary>F6 打开存档画面请求。</summary>
    public static event Action? SaveScreenRequested;

    /// <summary>F7 打开读档画面请求。</summary>
    public static event Action? LoadScreenRequested;

    /// <summary>Skip 模式切换请求（热键或菜单触发）。</summary>
    public static event Action? SkipToggleRequested;

    /// <summary>Auto 模式切换请求。</summary>
    public static event Action? AutoToggleRequested;

    #endregion

    #region 触发方法（引擎主机 + 游戏层调用）

    public static void FireQuickSave() => QuickSaveRequested?.Invoke();
    public static void FireQuickLoad() => QuickLoadRequested?.Invoke();
    public static void FireSystemMenu() => SystemMenuRequested?.Invoke();
    public static void FireBacklog() => BacklogRequested?.Invoke();
    public static void FireHideUIToggle() => HideUIToggleRequested?.Invoke();
    public static void FireSaveScreen() => SaveScreenRequested?.Invoke();
    public static void FireLoadScreen() => LoadScreenRequested?.Invoke();
    public static void FireSkipToggle() => SkipToggleRequested?.Invoke();
    public static void FireAutoToggle() => AutoToggleRequested?.Invoke();

    /// <summary>手机界面切换请求（Ctrl+P）。</summary>
    public static event Action? PhoneToggleRequested;
    public static void FirePhoneToggle() => PhoneToggleRequested?.Invoke();

    #endregion

    #region 由引擎宿主调用

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

    internal static void FeedUpPress()
    {
        _upPressedThisFrame = true;
    }

    internal static void FeedDownPress()
    {
        _downPressedThisFrame = true;
    }

    internal static void FeedLeftPress()
    {
        _leftPressedThisFrame = true;
    }

    internal static void FeedRightPress()
    {
        _rightPressedThisFrame = true;
    }

    internal static void EndFrame()
    {
        // 在清零前先读取本帧事件状态
        bool menuPressed = _menuPressedThisFrame;

        _advancePressedThisFrame = false;
        _autoToggledThisFrame = false;
        _menuPressedThisFrame = false;
        _upPressedThisFrame = false;
        _downPressedThisFrame = false;
        _leftPressedThisFrame = false;
        _rightPressedThisFrame = false;

        // 每帧结束触发累积的热键事件
        if (menuPressed)
            SystemMenuRequested?.Invoke();
    }

    #endregion

    #region 公开 API

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

    /// <summary>本帧是否按了上方向键。</summary>
    public static bool IsUpPressedThisFrame() => _upPressedThisFrame;

    /// <summary>本帧是否按了下方向键。</summary>
    public static bool IsDownPressedThisFrame() => _downPressedThisFrame;

    /// <summary>本帧是否按了左方向键。</summary>
    public static bool IsLeftPressedThisFrame() => _leftPressedThisFrame;

    /// <summary>本帧是否按了右方向键。</summary>
    public static bool IsRightPressedThisFrame() => _rightPressedThisFrame;

    /// <summary>消耗本帧的上方向键输入。</summary>
    public static void ConsumeUpPress()
    {
        _upPressedThisFrame = false;
    }

    /// <summary>消耗本帧的下方向键输入。</summary>
    public static void ConsumeDownPress()
    {
        _downPressedThisFrame = false;
    }

    /// <summary>消耗本帧的左方向键输入。</summary>
    public static void ConsumeLeftPress()
    {
        _leftPressedThisFrame = false;
    }

    /// <summary>消耗本帧的右方向键输入。</summary>
    public static void ConsumeRightPress()
    {
        _rightPressedThisFrame = false;
    }
    #endregion
}
