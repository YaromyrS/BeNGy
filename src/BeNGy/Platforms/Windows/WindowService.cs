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
        _hwnd = hwnd;   // needed by Scale before SubclassForDrag runs
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
        SubclassForDrag(hwnd);

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
    // The drag lives in a subclass of the WinUI content-island bridge window
    // (Microsoft.UI.Content.DesktopChildSiteBridge), because that is where
    // mouse input actually arrives: WinUI 3 delivers it as client-area
    // WM_POINTER* messages to the bridge, bypassing the legacy non-client
    // pipeline entirely. See BridgeWndProc for the manual move loop.
    // Rejected alternatives, in order: WM_NCLBUTTONDOWN from a MAUI gesture
    // fought WinUI pointer capture (drag only started after mouse release);
    // InputNonClientPointerSource caption regions inject a "Non Client Input
    // Sink Window" into the UIA tree that broke element lookups in the E2E
    // sandbox; WM_NCHITTEST/HTCAPTION subclassing was worse than useless --
    // the pointer pipeline consults the hit test when ROUTING input, so a
    // non-HTCLIENT answer over the header made presses there vanish (routed
    // toward native caption handling the island doesn't have) while never
    // producing the legacy WM_NCLBUTTONDOWN/WM_SYSCOMMAND that would have made
    // DefWindowProc drag the window. Message-trace proof: with the overrides
    // active, only presses OUTSIDE the drag zone arrived at the bridge.

    private const int CaptionHeight = 56;
    private int _dragLeftExclusion;
    private int _dragRightExclusion = 100;
    private IntPtr _hwnd;
    private NativeMethods.WndProcDelegate? _wndProc;         // fields: keep the
    private NativeMethods.WndProcDelegate? _bridgeWndProc;   // delegates alive for GC
    private IntPtr _oldWndProc;
    private IntPtr _oldBridgeWndProc;
    private IntPtr _bridgeHwnd;

    private void SubclassForDrag(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _wndProc = DragWndProc;
        _oldWndProc = NativeMethods.SetWindowLongPtr(
            hwnd,
            NativeMethods.GWLP_WNDPROC,
            System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_wndProc));
        TrySubclassBridge();
    }

    // The bridge child is created when the XAML content island attaches, which
    // can be after Initialize (we run on MAUI's Window.Created). Callable
    // repeatedly; binds once. ShowWindow retries it as a fallback.
    private void TrySubclassBridge()
    {
        // Re-bind if WinUI ever destroys and recreates the bridge (a recreated
        // bridge would silently revert to an un-subclassed, drag-less window).
        if (_bridgeHwnd != IntPtr.Zero && !NativeMethods.IsWindow(_bridgeHwnd))
        {
            _bridgeHwnd = IntPtr.Zero;
        }
        if (_bridgeHwnd != IntPtr.Zero || _hwnd == IntPtr.Zero) return;

        var bridge = NativeMethods.FindWindowEx(
            _hwnd, IntPtr.Zero, "Microsoft.UI.Content.DesktopChildSiteBridge", null);
        if (bridge == IntPtr.Zero)
        {
            DragLog("INIT", "bridge NOT found");
            return;
        }

        _bridgeHwnd = bridge;
        _bridgeWndProc = BridgeWndProc;
        _oldBridgeWndProc = NativeMethods.SetWindowLongPtr(
            bridge,
            NativeMethods.GWLP_WNDPROC,
            System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_bridgeWndProc));
        DragLog("INIT", $"bridge bound 0x{bridge.ToInt64():X}, oldProc=0x{_oldBridgeWndProc.ToInt64():X}");
    }

    // Diagnostic tap only. Deliberately NO WM_NCHITTEST override here: the
    // pointer pipeline consults hit-testing when routing input, and any answer
    // other than HTCLIENT over the header made it route the press away from
    // the bridge (toward native caption handling that does not exist for a
    // WinUI island), so the press simply vanished. Message-trace proof: with
    // the overrides active, only presses OUTSIDE the drag zone ever arrived
    // as WM_POINTERDOWN on the bridge.
    private IntPtr DragWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        DragLog("TOP", msg, wParam);

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

    // Manual move loop on the bridge window: record the window origin on a
    // press in the drag zone, follow the cursor delta with SetWindowPos on
    // every move, stop on release. Drag messages are swallowed while a drag is
    // active; the drag zone holds nothing interactive.
    //
    // TWO input dialects on purpose. Real mice reach a WinUI island as
    // client-area WM_POINTER* messages (message trace: a real press arrives as
    // WM_POINTERDOWN on the bridge; no WM_NCLBUTTONDOWN / WM_NCPOINTERDOWN /
    // WM_SYSCOMMAND is ever generated, so DefWindowProc's native caption drag
    // is unreachable). Legacy WM_LBUTTONDOWN/WM_MOUSEMOVE/WM_LBUTTONUP cover
    // input paths that bypass mouse-in-pointer promotion -- and they are what
    // the E2E suite posts: fabricated WM_POINTER* messages are DISCARDED by
    // the message pump before reaching any wndproc (the pointer input stack
    // validates them against its live pointer table), while plain mouse
    // messages are deliverable via PostMessage. A drag is owned by the family
    // that armed it, so the paths cannot interfere. Pointer lParams carry
    // screen coords; legacy lParams carry client coords, converted before the
    // shared zone/delta math (all screen-space).
    private enum DragSource { None, Pointer, Legacy }

    private DragSource _dragSource;
    private int _dragStartCursorX;
    private int _dragStartCursorY;
    private NativeMethods.RECT _dragStartRect;

    private IntPtr BridgeWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        DragLog("BRIDGE", msg, wParam);

        switch (msg)
        {
            case NativeMethods.WM_POINTERDOWN
                when _dragSource == DragSource.None && TryStartDrag(UnpackPoint(lParam), DragSource.Pointer):
                return IntPtr.Zero;

            case NativeMethods.WM_POINTERUPDATE when _dragSource == DragSource.Pointer:
                MoveWindowWithCursor(UnpackPoint(lParam));
                return IntPtr.Zero;

            case NativeMethods.WM_POINTERUP when _dragSource == DragSource.Pointer:
                EndDrag();
                return IntPtr.Zero;

            case NativeMethods.WM_LBUTTONDOWN
                when _dragSource == DragSource.None && TryStartDrag(ClientToScreen(hwnd, UnpackPoint(lParam)), DragSource.Legacy):
                return IntPtr.Zero;

            // Only moves with the left button held belong to the drag. Windows
            // also synthesizes WM_MOUSEMOVE (button up) whenever the window
            // moves under a stationary cursor; following those snapped the
            // palette to wherever the cursor rested (E2E drag test, 125%
            // sandbox, cursor parked over the palette) and would make a drag
            // whose button-up landed elsewhere stick to the cursor. They pass
            // through to WinUI as ordinary hover input.
            case NativeMethods.WM_MOUSEMOVE
                when _dragSource == DragSource.Legacy && (wParam.ToInt64() & NativeMethods.MK_LBUTTON) != 0:
                MoveWindowWithCursor(ClientToScreen(hwnd, UnpackPoint(lParam)));
                return IntPtr.Zero;

            case NativeMethods.WM_LBUTTONUP when _dragSource == DragSource.Legacy:
                EndDrag();
                return IntPtr.Zero;

            case NativeMethods.WM_POINTERCAPTURECHANGED when _dragSource != DragSource.None:
                _dragSource = DragSource.None;   // capture stolen: cancel, but let others see the message
                break;
        }

        return NativeMethods.CallWindowProc(_oldBridgeWndProc, hwnd, msg, wParam, lParam);
    }

    private bool TryStartDrag((int X, int Y) screenPoint, DragSource source)
    {
        if (!IsInDragZone(screenPoint)) return false;

        _dragSource = source;
        (_dragStartCursorX, _dragStartCursorY) = screenPoint;
        NativeMethods.GetWindowRect(_hwnd, out _dragStartRect);
        DragLog("BRIDGE", $"drag start ({source})");
        return true;
    }

    private void MoveWindowWithCursor((int X, int Y) screenPoint)
    {
        NativeMethods.SetWindowPos(
            _hwnd,
            IntPtr.Zero,
            _dragStartRect.Left + (screenPoint.X - _dragStartCursorX),
            _dragStartRect.Top + (screenPoint.Y - _dragStartCursorY),
            0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private void EndDrag()
    {
        DragLog("BRIDGE", $"drag end ({_dragSource})");
        _dragSource = DragSource.None;
    }

    private static (int X, int Y) UnpackPoint(IntPtr lParam)
    {
        return (unchecked((short)(lParam.ToInt64() & 0xFFFF)),
                unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF)));
    }

    private static (int X, int Y) ClientToScreen(IntPtr hwnd, (int X, int Y) clientPoint)
    {
        var p = new NativeMethods.POINT { X = clientPoint.X, Y = clientPoint.Y };
        NativeMethods.ClientToScreen(hwnd, ref p);
        return (p.X, p.Y);
    }

    // Diagnostic tap, active only when BENGY_DRAG_LOG names a file (the E2E
    // sandbox sets it). Records the input messages relevant to the caption
    // drag so a failed drag can be attributed from a test run's artifacts.
    private static readonly string? DragLogPath =
        Environment.GetEnvironmentVariable("BENGY_DRAG_LOG");

    private static void DragLog(string source, uint msg, IntPtr wParam)
    {
        if (DragLogPath is null) return;

        var name = msg switch
        {
            NativeMethods.WM_NCLBUTTONDOWN => "WM_NCLBUTTONDOWN",
            NativeMethods.WM_NCLBUTTONUP => "WM_NCLBUTTONUP",
            NativeMethods.WM_SYSCOMMAND => "WM_SYSCOMMAND",
            NativeMethods.WM_LBUTTONDOWN => "WM_LBUTTONDOWN",
            NativeMethods.WM_CAPTURECHANGED => "WM_CAPTURECHANGED",
            NativeMethods.WM_ENTERSIZEMOVE => "WM_ENTERSIZEMOVE",
            NativeMethods.WM_EXITSIZEMOVE => "WM_EXITSIZEMOVE",
            NativeMethods.WM_NCPOINTERDOWN => "WM_NCPOINTERDOWN",
            NativeMethods.WM_NCPOINTERUP => "WM_NCPOINTERUP",
            NativeMethods.WM_POINTERDOWN => "WM_POINTERDOWN",
            NativeMethods.WM_POINTERUP => "WM_POINTERUP",
            NativeMethods.WM_POINTERENTER => "WM_POINTERENTER",
            NativeMethods.WM_POINTERLEAVE => "WM_POINTERLEAVE",
            _ => null,
        };
        if (name is null) return;

        DragLog(source, $"{name} wParam=0x{wParam.ToInt64():X}");
    }

    private static void DragLog(string source, string text)
    {
        if (DragLogPath is null) return;

        try
        {
            System.IO.File.AppendAllText(
                DragLogPath,
                $"{DateTime.Now:HH:mm:ss.fff} {source} {text}\r\n");
        }
        catch
        {
            // Diagnostics must never take the window proc down.
        }
    }

    // Screen-space in, evaluated in top-level client space (which the bridge
    // shares, since it fills the client area).
    private bool IsInDragZone((int X, int Y) screenPoint)
    {
        var point = new NativeMethods.POINT { X = screenPoint.X, Y = screenPoint.Y };
        var scale = Scale;   // client coords are physical; zone constants are DIPs
        return NativeMethods.ScreenToClient(_hwnd, ref point)
            && point.Y >= 0 && point.Y < Px(CaptionHeight, scale)
            && point.X >= Px(_dragLeftExclusion, scale)
            && point.X < Px(PaletteWidth - _dragRightExclusion, scale);
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

        TrySubclassBridge();   // no-op once bound; covers bridge created after Initialize
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
