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

    private const int WindowWidth = 440;
    private const int WindowHeight = 480;

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

        // Set compact popup size and disable resize
        _appWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        CenterOnScreen();

        // Intercept close button: hide instead of close
        _appWindow.Closing += OnWindowClosing;
    }

    public void ShowWindow()
    {
        if (_nativeWindow == null || _appWindow == null) return;

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

    public void CenterOnScreen()
    {
        if (_appWindow == null) return;

        var displayArea = DisplayArea.Primary;
        if (displayArea == null) return;

        var centerX = (displayArea.WorkArea.Width - _appWindow.Size.Width) / 2;
        var centerY = (displayArea.WorkArea.Height - _appWindow.Size.Height) / 2;

        _appWindow.Move(new PointInt32(centerX, centerY));
    }

    private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // Cancel close — hide to tray instead (AC #2)
        args.Cancel = true;
        HideWindow();
    }
}
