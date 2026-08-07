namespace BeNGy.Services.Interfaces;

/// <summary>
/// The palette's fixed window surfaces. The window hugs its content: the main
/// form is compact, grows when the validation InfoBar appears, and grows
/// further for the settings page.
/// </summary>
public enum PaletteSurface
{
    Main,
    MainExpanded,
    Settings,
}

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

    /// <summary>
    /// Resizes the palette to the given surface. Resize only — the window's
    /// position is the user's business once they have dragged it somewhere.
    /// </summary>
    void SetPaletteSize(PaletteSurface surface);
}
