namespace NanoUint;

/// <summary>
/// Platform-neutral key codes for the engine input abstraction.
/// Replaces System.Windows.Input.Key in public APIs so the Core layer has no WPF dependency.
/// </summary>
public enum EngineKey
{
    None = 0,

    // Letters
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    // Digits
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

    // Navigation
    Left, Right, Up, Down,
    PageUp, PageDown, Home, End,

    // Control
    Space, Enter, Escape, Tab, Backspace, Delete, Insert,

    // Modifier
    LeftShift, RightShift,
    LeftCtrl, RightCtrl,
    LeftAlt, RightAlt,

    // Function
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
}
