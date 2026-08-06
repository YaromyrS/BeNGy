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
    private Microsoft.UI.Xaml.Window? _nativeWindow;
    private AppWindow? _appWindow;

    private const int PaletteWidth = 560;

    // Palette hugs its content; the taller size fits the settings cards. Both
    // must fit EVERY control including the validation warning rows -- an
    // undersized window once left the Copy button laid out past the bottom
    // edge, invisible and unclickable, while UI Automation still Invoked it
    // happily (see LayoutFitTests).
    private const int MainHeight = 400;
    private const int SettingsHeight = 620;

    // DWM window corner preference (Win11): 33 = DWMWA_WINDOW_CORNER_PREFERENCE, 2 = DWMWCP_ROUND
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [global::System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public bool IsVisible { get; private set; } = true;

    /// <summary>
    /// Binds this service to the native WinUI window. Must be called once after the window is created.
    /// </summary>
    public void Initialize(Microsoft.UI.Xaml.Window nativeWindow)
    {
        _nativeWindow = nativeWindow;
        var hwnd = WindowNative.GetWindowHandle(nativeWindow);
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

        SetPaletteSize(settings: false);

        // The acrylic/mica tint and WinUI control themes key off the native
        // root's RequestedTheme, which does NOT track MAUI's UserAppTheme
        // override. Without this, "app Light on Windows dark" renders light
        // text tokens over a dark backdrop -- unreadable.
        if (Microsoft.Maui.Controls.Application.Current is { } app)
        {
            ApplyNativeTheme(app);
            app.RequestedThemeChanged += (_, _) => ApplyNativeTheme(app);
        }

        // Intercept close button: hide instead of close
        _appWindow.Closing += OnWindowClosing;
    }

    private void ApplyNativeTheme(Microsoft.Maui.Controls.Application app)
    {
        if (_nativeWindow?.Content is Microsoft.UI.Xaml.FrameworkElement root)
        {
            root.RequestedTheme = app.RequestedTheme == AppTheme.Dark
                ? Microsoft.UI.Xaml.ElementTheme.Dark
                : Microsoft.UI.Xaml.ElementTheme.Light;
        }
    }

    public void SetPaletteSize(bool settings)
    {
        if (_appWindow is null) return;
        _appWindow.Resize(new SizeInt32(PaletteWidth, settings ? SettingsHeight : MainHeight));
        PositionPalette();
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

        PositionPalette();
        _appWindow.Show();

        // Restore from minimized state (taskbar) before activating
        var hwnd = WindowNative.GetWindowHandle(_nativeWindow);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);

        _nativeWindow.Activate();
        IsVisible = true;

        // Ensure foreground
        NativeMethods.SetForegroundWindow(hwnd);
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
