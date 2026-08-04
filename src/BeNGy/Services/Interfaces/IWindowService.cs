namespace BeNGy.Services.Interfaces;

/// <summary>
/// Abstraction for main window visibility management.
/// Platform implementations handle native window show/hide/center behavior.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Shows the main window and brings it to the foreground.
    /// </summary>
    void ShowWindow();

    /// <summary>
    /// Hides the main window (keeps it in memory for fast re-show).
    /// </summary>
    void HideWindow();

    /// <summary>
    /// Gets whether the main window is currently visible.
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// Centers the window on the primary monitor work area.
    /// </summary>
    void CenterOnScreen();
}
