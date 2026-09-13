namespace NanoUint;

/// <summary>Abstraction for OS-level UI services (message boxes, clipboard, window state).</summary>
public interface ISystemServices
{
    void ShowMessage(string text, string title);
    bool ShowConfirm(string text, string title);
    void SetClipboard(string text);
    void SetWindowTitle(string title);
    void MinimizeWindow();
    void MaximizeWindow();
    void RestoreWindow();
    void Shutdown(int code);
}
