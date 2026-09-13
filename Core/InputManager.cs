namespace NanoUint;

/// <summary>Input manager providing a cross-platform input abstraction.</summary>
public static class InputManager
{
    /// <summary>Key dispatch delegate; returns true when the key was handled.</summary>
        public static Func<EngineKey, bool>? KeyDispatch;

    private static bool _advancePressedThisFrame;
    private static bool _skipHeld;
    private static bool _autoToggledThisFrame;
    private static bool _menuPressedThisFrame;
    private static bool _upPressedThisFrame;
    private static bool _downPressedThisFrame;
    private static bool _leftPressedThisFrame;
    private static bool _rightPressedThisFrame;

    #region Global hotkey events (subscribed by game code)

    /// <summary>F5 quick-save request.</summary>
        public static event Action? QuickSaveRequested;

    /// <summary>F9 quick-load request.</summary>
        public static event Action? QuickLoadRequested;

    /// <summary>Esc/right-click system menu request.</summary>
        public static event Action? SystemMenuRequested;

    /// <summary>Page Up backlog request.</summary>
        public static event Action? BacklogRequested;

    /// <summary>Shift hide-UI toggle request.</summary>
        public static event Action? HideUIToggleRequested;

    /// <summary>F6 open save screen request.</summary>
        public static event Action? SaveScreenRequested;

    /// <summary>F7 open load screen request.</summary>
        public static event Action? LoadScreenRequested;

    /// <summary>Skip mode toggle request (fired by hotkey or menu).</summary>
        public static event Action? SkipToggleRequested;

    /// <summary>Auto mode toggle request.</summary>
        public static event Action? AutoToggleRequested;

    #endregion

    #region Trigger methods (called by the engine host and game code)

        public static void FireQuickSave() => QuickSaveRequested?.Invoke();
        public static void FireQuickLoad() => QuickLoadRequested?.Invoke();
        public static void FireSystemMenu() => SystemMenuRequested?.Invoke();
        public static void FireBacklog() => BacklogRequested?.Invoke();
        public static void FireHideUIToggle() => HideUIToggleRequested?.Invoke();
        public static void FireSaveScreen() => SaveScreenRequested?.Invoke();
        public static void FireLoadScreen() => LoadScreenRequested?.Invoke();
        public static void FireSkipToggle() => SkipToggleRequested?.Invoke();
        public static void FireAutoToggle() => AutoToggleRequested?.Invoke();

    /// <summary>Phone UI toggle request (Ctrl+P).</summary>
        public static event Action? PhoneToggleRequested;
        public static void FirePhoneToggle() => PhoneToggleRequested?.Invoke();

    #endregion

    #region Called by the engine host

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
        // Read this frame's event state before clearing it
        bool menuPressed = _menuPressedThisFrame;

        _advancePressedThisFrame = false;
        _autoToggledThisFrame = false;
        _menuPressedThisFrame = false;
        _upPressedThisFrame = false;
        _downPressedThisFrame = false;
        _leftPressedThisFrame = false;
        _rightPressedThisFrame = false;

        // Fire accumulated hotkey events at end of frame
        if (menuPressed)
            SystemMenuRequested?.Invoke();
    }

    #endregion

    #region Public API

    /// <summary>Whether the advance key (Space/Enter/left mouse) was pressed this frame.</summary>
        public static bool IsAdvancePressedThisFrame() => _advancePressedThisFrame;

    /// <summary>Consumes this frame's advance input.</summary>
        public static void ConsumeAdvancePress()
    {
        _advancePressedThisFrame = false;
    }

    /// <summary>Whether the skip key (Ctrl) is held down.</summary>
        public static bool IsSkipHeld() => _skipHeld;

    /// <summary>Whether auto mode was toggled this frame (A key).</summary>
        public static bool IsAutoModeToggledThisFrame() => _autoToggledThisFrame;

    /// <summary>Whether the system menu key (Esc) was pressed this frame.</summary>
        public static bool IsMenuPressedThisFrame() => _menuPressedThisFrame;

    /// <summary>Whether the up arrow was pressed this frame.</summary>
        public static bool IsUpPressedThisFrame() => _upPressedThisFrame;

    /// <summary>Whether the down arrow was pressed this frame.</summary>
        public static bool IsDownPressedThisFrame() => _downPressedThisFrame;

    /// <summary>Whether the left arrow was pressed this frame.</summary>
        public static bool IsLeftPressedThisFrame() => _leftPressedThisFrame;

    /// <summary>Whether the right arrow was pressed this frame.</summary>
        public static bool IsRightPressedThisFrame() => _rightPressedThisFrame;

    /// <summary>Consumes this frame's up-arrow input.</summary>
        public static void ConsumeUpPress()
    {
        _upPressedThisFrame = false;
    }

    /// <summary>Consumes this frame's down-arrow input.</summary>
        public static void ConsumeDownPress()
    {
        _downPressedThisFrame = false;
    }

    /// <summary>Consumes this frame's left-arrow input.</summary>
        public static void ConsumeLeftPress()
    {
        _leftPressedThisFrame = false;
    }

    /// <summary>Consumes this frame's right-arrow input.</summary>
        public static void ConsumeRightPress()
    {
        _rightPressedThisFrame = false;
    }
    #endregion
}
