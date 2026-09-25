namespace BeNGy.Services.Interfaces;

/// <summary>
/// The palette's fixed window surfaces. Main is the initial size of the compact
/// form (the main page then hugs its measured content, see
/// <see cref="IWindowService.FitPaletteToContent"/>); Settings is a fixed,
/// scrolling page.
/// </summary>
public enum PaletteSurface
{
    Main,
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
    /// Resizes the palette to the given surface. The window's position is the
    /// user's business once they have dragged it somewhere; it only moves if
    /// growing would push it off the bottom of the work area.
    /// </summary>
    void SetPaletteSize(PaletteSurface surface);

    /// <summary>
    /// Sizes the palette to hug the main page's measured content height, in
    /// device-independent pixels, so every state (no warning, a one-line
    /// warning, a multi-error warning that wraps) fits exactly with no dead space.
    /// </summary>
    void FitPaletteToContent(double contentHeight);
}
