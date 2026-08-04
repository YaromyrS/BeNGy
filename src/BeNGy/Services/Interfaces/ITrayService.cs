namespace BeNGy.Services.Interfaces;

/// <summary>
/// Abstraction for system tray icon management.
/// Platform implementations provide native tray icon and context menu behavior.
/// </summary>
public interface ITrayService : IDisposable
{
    /// <summary>
    /// Initialize the system tray icon and context menu.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Show the tray icon (if hidden).
    /// </summary>
    void ShowIcon();

    /// <summary>
    /// Hide the tray icon.
    /// </summary>
    void HideIcon();

    /// <summary>
    /// Fired when user clicks "Open" in context menu.
    /// </summary>
    event EventHandler? OpenRequested;

    /// <summary>
    /// Fired when user clicks "Settings" in context menu.
    /// </summary>
    event EventHandler? SettingsRequested;

    /// <summary>
    /// Fired when user clicks "Exit" in context menu.
    /// </summary>
    event EventHandler? ExitRequested;

    /// <summary>
    /// Shows a non-blocking balloon/toast notification near the tray icon.
    /// Used for hotkey conflict notifications (FR31).
    /// </summary>
    void ShowBalloon(string title, string message);
}
