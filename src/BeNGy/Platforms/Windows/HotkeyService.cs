using BeNGy.Helpers;
using BeNGy.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BeNGy.Platforms.Windows;

/// <summary>
/// Windows implementation of <see cref="IHotkeyService"/>.
/// Uses Win32 RegisterHotKey + window subclassing to intercept WM_HOTKEY in WinUI3
/// (which lacks the traditional WinForms message loop).
/// </summary>
public class HotkeyService : IHotkeyService
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 9000; // Arbitrary unique ID

    private readonly ILogger<HotkeyService> _logger;
    private IntPtr _windowHandle;
    private bool _isRegistered;
    private NativeMethods.WndProcDelegate? _newWndProc;
    private IntPtr _oldWndProc;
    private bool _disposed;

    public event EventHandler? HotkeyPressed;
    public bool IsRegistered => _isRegistered;

    public HotkeyService(ILogger<HotkeyService> logger)
    {
        _logger = logger;
    }

    public HotkeyRegistrationResult RegisterHotkey(HotkeyModifiers modifiers, HotkeyKey key)
    {
        _windowHandle = GetWindowHandle();

        if (_windowHandle == IntPtr.Zero)
        {
            var msg = "Window handle not available — hotkey cannot be registered yet";
            _logger.LogWarning(msg);
            return HotkeyRegistrationResult.Failed(msg);
        }

        // Fix C1: Ensure we don't double-subclass if already registered or re-registering
        UnregisterHotkey();

        uint mod = (uint)modifiers;
        uint vk = (uint)key;

        _isRegistered = NativeMethods.RegisterHotKey(_windowHandle, HOTKEY_ID, mod, vk);

        if (_isRegistered)
        {
            SubclassWindow();
            _logger.LogInformation("Global hotkey registered successfully: {Modifiers}+{Key}", modifiers, key);
            return HotkeyRegistrationResult.Success();
        }

        // Story 5.2: detailed error for user notification
        var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        var message = error switch
        {
            1409 => "Hotkey is already registered by another application",
            _ => $"Failed to register hotkey (error code: {error})"
        };

        _logger.LogWarning("Hotkey registration failed: {Message}", message);
        return HotkeyRegistrationResult.Failed(message);
    }

    public void UnregisterHotkey()
    {
        if (_isRegistered && _windowHandle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, HOTKEY_ID);
            _isRegistered = false;
            _logger.LogDebug("Global hotkey unregistered");
        }

        RestoreWindowProc();
    }

    /// <summary>
    /// Restores the original window procedure to prevent crashes from a dangling delegate pointer.
    /// </summary>
    private void RestoreWindowProc()
    {
        if (_oldWndProc != IntPtr.Zero && _windowHandle != IntPtr.Zero)
        {
            NativeMethods.SetWindowLongPtr(_windowHandle, NativeMethods.GWLP_WNDPROC, _oldWndProc);
            _oldWndProc = IntPtr.Zero;
            _newWndProc = null;
            _logger.LogDebug("Original window procedure restored");
        }
    }

    /// <summary>
    /// WinUI3 does not have a traditional message loop like WinForms.
    /// We subclass the native HWND to intercept WM_HOTKEY messages using SetWindowLongPtr.
    /// </summary>
    private void SubclassWindow()
    {
        if (_oldWndProc != IntPtr.Zero) return; // Already subclassed

        _newWndProc = WndProc;
        _oldWndProc = NativeMethods.SetWindowLongPtr(
            _windowHandle,
            NativeMethods.GWLP_WNDPROC,
            System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_newWndProc));
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            _logger.LogDebug("Hotkey pressed, firing event on UI thread");
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }

        return NativeMethods.CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private static IntPtr GetWindowHandle()
    {
        var mauiWindow = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
            as Microsoft.UI.Xaml.Window;

        if (mauiWindow != null)
        {
            return WinRT.Interop.WindowNative.GetWindowHandle(mauiWindow);
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterHotkey();
        GC.SuppressFinalize(this);
    }
}
