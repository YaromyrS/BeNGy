using BeNGy.Services.Interfaces;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using Windows.Graphics;
using BeNGy.Helpers;

namespace BeNGy.Platforms.Windows;

/// <summary>
/// Windows implementation of <see cref="IWindowService"/>.
/// Manages main window visibility via AppWindow.Hide/Show for sub-500ms popup latency.
/// </summary>
public class WindowService : IWindowService
{
    private readonly IThemeService? _themeService;
    private Microsoft.UI.Xaml.Window? _nativeWindow;
    private AppWindow? _appWindow;

    public WindowService(IThemeService? themeService = null)
    {
        _themeService = themeService;
    }

    // All layout constants below are device-independent pixels (the units
    // XAML lays out in). AppWindow.Resize and every Win32 coordinate we touch
    // are PHYSICAL pixels, so they go through Px()/Scale. The two coincide
    // only at 100% display scaling -- at 125% an unscaled 560x216 window gave
    // the content just 448x173 DIPs of room, so the UI did not fit.
    private const int PaletteWidth = 560;

    // The main page hugs its MEASURED content (FitPaletteToContent): a fixed
    // "expanded" height can't be right for both a one-line and a wrapped
    // multi-error warning. MainHeight is only the size before the first
    // layout pass; Settings is a fixed scrolling page. Every state must fit
    // every control it shows -- an undersized window once left the Copy
    // button laid out past the bottom edge, invisible and unclickable, while
    // UI Automation still Invoked it happily (see LayoutFitTests).
    private const int MainHeight = 216;
    private const int SettingsHeight = 620;
    private const int MinContentHeight = 100;   // ignore transient mid-layout measurements

    private int _currentHeight = MainHeight;
    private bool _currentIsSettings;

    /// <summary>Physical pixels per device-independent pixel for the window's current monitor.</summary>
    private double Scale => _hwnd == IntPtr.Zero ? 1.0 : NativeMethods.GetDpiForWindow(_hwnd) / 96.0;

    private static int Px(int dip, double scale) => (int)Math.Round(dip * scale);

    // DWM window corner preference (Win11): 33 = DWMWA_WINDOW_CORNER_PREFERENCE, 2 = DWMWCP_ROUND
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [global::System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public bool IsVisible { get; private set; } = true;

    public event EventHandler? Shown;

    /// <summary>
    /// Binds this service to the native WinUI window. Must be called once after the window is created.
    /// </summary>
    public void Initialize(Microsoft.UI.Xaml.Window nativeWindow)
    {
        _nativeWindow = nativeWindow;
        var hwnd = WindowNative.GetWindowHandle(nativeWindow);
        _hwnd = hwnd;   // needed by Scale, the drag and the WndProc subclass
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // Palette chrome: no border, no title bar, no resize/minimize/maximize.
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        // Win11 rounded corners on a borderless window need an explicit opt-in.
        var corner = DWMWCP_ROUND;
        _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        // Acrylic like PowerToys Run; Mica fallback; null (solid PageFallback
        // color from tokens) when composition is unavailable.
        nativeWindow.SystemBackdrop =
            Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported()
                ? new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop()
                : Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported()
                    ? new Microsoft.UI.Xaml.Media.MicaBackdrop()
                    : null;

        SetPaletteSize(PaletteSurface.Main);
        PositionPalette();   // initial placement only; afterwards position belongs to the user
        SubclassWindow(hwnd);
        AttachDragHandlers();
        // Content is usually set after this point, and a theme switch may
        // replace it: re-check on activation (a no-op once attached).
        nativeWindow.Activated += (_, _) => AttachDragHandlers();

        // The acrylic/mica tint and WinUI control themes key off the native
        // root's RequestedTheme, which does NOT track MAUI's UserAppTheme
        // override. Without this, "app Light on Windows dark" renders light
        // text tokens over a dark backdrop -- unreadable.
        //
        // Two subscriptions on purpose: MAUI's RequestedThemeChanged proved
        // droppable in practice (reproduced: theme switch left the native root
        // dark while MAUI bindings went light), so the authoritative signal is
        // IThemeService.ThemeChanged, which fires on every explicit change and
        // on system auto-switch. The MAUI event stays as belt-and-braces.
        if (Microsoft.Maui.Controls.Application.Current is { } app)
        {
            ApplyNativeTheme(app);
            app.RequestedThemeChanged += (_, _) => ApplyNativeTheme(app);
        }
        if (_themeService is not null)
        {
            _themeService.ThemeChanged += (_, effective) =>
                MainThread.BeginInvokeOnMainThread(() => ApplyNativeTheme(effective));
            ApplyNativeTheme(_themeService.EffectiveTheme);
        }

        // Intercept close button: hide instead of close
        _appWindow.Closing += OnWindowClosing;
    }

    private void ApplyNativeTheme(Microsoft.Maui.Controls.Application app)
    {
        SetNativeRootTheme(app.RequestedTheme == AppTheme.Dark);
    }

    private void ApplyNativeTheme(BeNGyTheme effective)
    {
        SetNativeRootTheme(effective == BeNGyTheme.Dark);
    }

    private void SetNativeRootTheme(bool dark)
    {
        if (_nativeWindow?.Content is Microsoft.UI.Xaml.FrameworkElement root)
        {
            root.RequestedTheme = dark
                ? Microsoft.UI.Xaml.ElementTheme.Dark
                : Microsoft.UI.Xaml.ElementTheme.Light;
        }
    }

    public void SetPaletteSize(PaletteSurface surface)
    {
        var isSettings = surface == PaletteSurface.Settings;
        ApplyPaletteSize(isSettings ? SettingsHeight : MainHeight, isSettings);
    }

    public void FitPaletteToContent(double contentHeight)
    {
        if (contentHeight < MinContentHeight) return;
        ApplyPaletteSize((int)Math.Ceiling(Math.Min(contentHeight, SettingsHeight)), isSettings: false);
    }

    private void ApplyPaletteSize(int height, bool isSettings)
    {
        // Keep every window mutation on the UI thread: an off-thread native
        // window call once took the whole app down (COM E_UNEXPECTED)
        // mid-E2E-suite, and callers include property-change handlers that can
        // run elsewhere (e.g. UIA-driven value changes).
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => ApplyPaletteSize(height, isSettings));
            return;
        }

        if (_appWindow is null) return;
        (_currentHeight, _currentIsSettings) = (height, isSettings);

        // Resize, don't reposition: once the user drags the palette somewhere,
        // that position is theirs. Growth is anchored top-left (downward), which
        // reads naturally for the InfoBar and the settings page alike -- the one
        // exception is the on-screen nudge in KeepInsideWorkArea.
        var scale = Scale;
        _appWindow.Resize(new SizeInt32(Px(PaletteWidth, scale), Px(height, scale)));
        KeepInsideWorkArea();

        // Drag zone exclusions differ per page: main has gear+close on the
        // right; settings has back on the left and close on the right.
        (_dragLeftExclusion, _dragRightExclusion) = isSettings ? (60, 60) : (0, 100);
    }

    // ── Header dragging ────────────────────────────────────────────
    // A press in the header strip captures the pointer on the XAML root; each
    // move repositions the window by the cursor's screen-space delta; release
    // or capture loss ends it (PaletteDrag holds the zone and move math).
    // XAML pointer events are WinUI's public input API, so this holds however
    // the OS routes input into the content island. The previous approach
    // subclassed the island's DesktopChildSiteBridge HWND and read WM_POINTER*
    // there: after the Windows updates of 2026-11-11 no real input (not even
    // hover) reached that wndproc any more while messages SENT to it still
    // did -- and the E2E drag test sent its messages exactly there, so it
    // stayed green. Rejected earlier, in order: WM_NCLBUTTONDOWN from a MAUI
    // gesture fought WinUI pointer capture (drag only started after release);
    // InputNonClientPointerSource caption regions inject a "Non Client Input
    // Sink Window" into the UIA tree that broke element lookups in the E2E
    // sandbox; a WM_NCHITTEST/HTCAPTION override made header presses vanish.

    private int _dragLeftExclusion;
    private int _dragRightExclusion = 100;
    private IntPtr _hwnd;
    private NativeMethods.WndProcDelegate? _wndProc;   // field: keeps the delegate alive for GC
    private IntPtr _oldWndProc;
    private readonly PaletteDrag _drag = new();
    private Microsoft.UI.Xaml.UIElement? _dragRoot;
    private Microsoft.UI.Xaml.Input.PointerEventHandler? _onPressed;
    private Microsoft.UI.Xaml.Input.PointerEventHandler? _onMoved;
    private Microsoft.UI.Xaml.Input.PointerEventHandler? _onEnded;

    // Top-level subclass, for WM_DPICHANGED only.
    private void SubclassWindow(IntPtr hwnd)
    {
        _wndProc = WndProc;
        _oldWndProc = NativeMethods.SetWindowLongPtr(
            hwnd,
            NativeMethods.GWLP_WNDPROC,
            System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_wndProc));
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        var result = NativeMethods.CallWindowProc(_oldWndProc, hwnd, msg, wParam, lParam);

        // Moved to a monitor with a different scale (or the scale changed):
        // the DIP height is unchanged, so re-derive the physical window size
        // from it for the new DPI.
        if (msg == NativeMethods.WM_DPICHANGED)
        {
            ApplyPaletteSize(_currentHeight, _currentIsSettings);
        }

        return result;
    }

    // MAUI can fill the window's Content after Initialize runs (we run on
    // Window.Created), so ShowWindow retries this. Re-attaches if the root
    // element is ever replaced; a no-op otherwise.
    private void AttachDragHandlers()
    {
        if (_nativeWindow?.Content is not Microsoft.UI.Xaml.UIElement root || ReferenceEquals(root, _dragRoot))
            return;

        _onPressed ??= OnDragPointerPressed;
        _onMoved ??= OnDragPointerMoved;
        _onEnded ??= OnDragPointerEnded;

        if (_dragRoot is not null)
        {
            _dragRoot.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent, _onPressed);
            _dragRoot.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerMovedEvent, _onMoved);
            _dragRoot.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerReleasedEvent, _onEnded);
            _dragRoot.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerCanceledEvent, _onEnded);
            _dragRoot.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerCaptureLostEvent, _onEnded);
            _drag.End();
        }

        // handledEventsToo: the header label or a panel may mark the press handled.
        root.AddHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent, _onPressed, true);
        root.AddHandler(Microsoft.UI.Xaml.UIElement.PointerMovedEvent, _onMoved, true);
        root.AddHandler(Microsoft.UI.Xaml.UIElement.PointerReleasedEvent, _onEnded, true);
        root.AddHandler(Microsoft.UI.Xaml.UIElement.PointerCanceledEvent, _onEnded, true);
        root.AddHandler(Microsoft.UI.Xaml.UIElement.PointerCaptureLostEvent, _onEnded, true);
        _dragRoot = root;
        DragLog($"handlers attached to {root.GetType().Name}");
    }

    private void OnDragPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_dragRoot is null || _drag.IsActive) return;

        // Root-relative DIPs; the root fills the client area. Touch contact
        // and the pen tip report as the left button too.
        var point = e.GetCurrentPoint(_dragRoot);
        if (!point.Properties.IsLeftButtonPressed) return;
        if (!PaletteDrag.IsInZone(point.Position.X, point.Position.Y, PaletteWidth, _dragLeftExclusion, _dragRightExclusion))
            return;

        // Physical screen pixels from here on: the cursor delta is immune to
        // the window moving under the pointer.
        if (!NativeMethods.GetCursorPos(out var cursor) || !NativeMethods.GetWindowRect(_hwnd, out var rect)) return;
        if (!_dragRoot.CapturePointer(e.Pointer)) return;

        _drag.Begin(cursor.X, cursor.Y, rect.Left, rect.Top);
        DragLog($"drag start at client DIP ({point.Position.X:0},{point.Position.Y:0})");
    }

    private void OnDragPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_drag.IsActive || !NativeMethods.GetCursorPos(out var cursor)) return;
        if (_drag.Move(cursor.X, cursor.Y) is not { } origin) return;

        NativeMethods.SetWindowPos(
            _hwnd,
            IntPtr.Zero,
            origin.X,
            origin.Y,
            0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    // Release, cancel and capture loss all end the drag. Releasing capture
    // raises PointerCaptureLost, which lands here again as a no-op.
    private void OnDragPointerEnded(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_drag.IsActive) return;
        _drag.End();
        _dragRoot?.ReleasePointerCapture(e.Pointer);
        DragLog("drag end");
    }

    // Diagnostic tap, active only when BENGY_DRAG_LOG names a file (the E2E
    // sandbox sets it).
    private static readonly string? DragLogPath =
        Environment.GetEnvironmentVariable("BENGY_DRAG_LOG");

    private static void DragLog(string text)
    {
        if (DragLogPath is null) return;

        try
        {
            System.IO.File.AppendAllText(DragLogPath, $"{DateTime.Now:HH:mm:ss.fff} {text}\r\n");
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            // An unwritable log path only loses diagnostics; it must never take the input path down.
        }
    }

    /// <summary>
    /// Growth is downward, so a palette low on the screen -- dragged there, or
    /// simply opened on a short display (1080p at 125% leaves 816 DIPs of work
    /// area; Settings opened from the 25% default position needs 824) -- would
    /// grow under the taskbar. Nudge it up just enough; never above the top.
    /// </summary>
    private void KeepInsideWorkArea()
    {
        if (_appWindow is null) return;
        var area = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var position = _appWindow.Position;
        var overflow = position.Y + _appWindow.Size.Height - (area.Y + area.Height);
        if (overflow <= 0) return;
        _appWindow.Move(new PointInt32(position.X, Math.Max(area.Y, position.Y - overflow)));
    }

    /// <summary>PowerToys Run position: centered horizontally, top at 25% of the work area.</summary>
    private void PositionPalette()
    {
        if (_appWindow is null) return;
        var area = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var x = area.X + (area.Width - _appWindow.Size.Width) / 2;
        var y = area.Y + area.Height / 4;
        _appWindow.Move(new PointInt32(x, y));
    }

    public void ShowWindow()
    {
        if (_nativeWindow == null || _appWindow == null) return;

        // The real window state, not IsVisible: the window can be hidden without
        // going through HideWindow (and IsVisible starts out true).
        var wasHidden = !_appWindow.IsVisible;

        AttachDragHandlers();   // no-op once attached; covers Content set after Initialize
        _appWindow.Show();

        // Restore from minimized state (taskbar) before activating
        var hwnd = WindowNative.GetWindowHandle(_nativeWindow);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);

        _nativeWindow.Activate();
        IsVisible = true;

        // Ensure foreground
        NativeMethods.SetForegroundWindow(hwnd);

        if (wasHidden)
            Shown?.Invoke(this, EventArgs.Empty);
    }

    public void HideWindow()
    {
        if (_appWindow == null) return;

        _appWindow.Hide();
        IsVisible = false;
    }

    public void CenterOnScreen() => PositionPalette();

    private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // Cancel close — hide to tray instead (AC #2)
        args.Cancel = true;
        HideWindow();
    }
}
