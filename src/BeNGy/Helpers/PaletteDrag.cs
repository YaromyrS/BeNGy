namespace BeNGy.Helpers;

/// <summary>
/// Header drag for the borderless palette, kept free of window types so it is
/// unit-testable. The platform layer feeds it pointer events: the zone check
/// works in client DIPs (what XAML reports), and the move math works in
/// physical screen pixels (cursor and window position), so the window moving
/// under the pointer never feeds back into the delta.
/// </summary>
public sealed class PaletteDrag
{
    /// <summary>Height of the draggable header strip, in DIPs.</summary>
    public const double CaptionHeight = 56;

    private int _startCursorX;
    private int _startCursorY;
    private int _startWindowX;
    private int _startWindowY;

    public bool IsActive { get; private set; }

    /// <summary>
    /// True when a press at client point (x, y), in DIPs, should start a drag: the
    /// header strip, minus the button columns at either end.
    /// </summary>
    public static bool IsInZone(double x, double y, double width, double leftExclusion, double rightExclusion) =>
        y >= 0 && y < CaptionHeight && x >= leftExclusion && x < width - rightExclusion;

    public void Begin(int cursorX, int cursorY, int windowX, int windowY)
    {
        (_startCursorX, _startCursorY) = (cursorX, cursorY);
        (_startWindowX, _startWindowY) = (windowX, windowY);
        IsActive = true;
    }

    /// <summary>The window origin for the current cursor position, or null when no drag is active.</summary>
    public (int X, int Y)? Move(int cursorX, int cursorY) =>
        IsActive
            ? (_startWindowX + (cursorX - _startCursorX), _startWindowY + (cursorY - _startCursorY))
            : null;

    public void End() => IsActive = false;
}
