using System.Runtime.InteropServices;

namespace BeNGy.Helpers;

/// <summary>
/// Windows native method declarations for window management and global hotkey registration.
/// All Win32 interop consolidated here per architecture guidelines.
/// </summary>
internal static class NativeMethods
{
    // ── Window management ─────────────────────────────────────────

    internal const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    // ── Borderless window dragging (WM_NCHITTEST subclass) ────────
    // A chromeless palette has no title bar; answering WM_NCHITTEST with
    // HTCAPTION for the header strip makes Windows treat it as a native
    // caption: drag works from the first pixel of movement, with no extra
    // input-sink windows polluting the UI Automation tree.

    internal const uint WM_NCHITTEST = 0x0084;
    internal const uint WM_NCLBUTTONDOWN = 0x00A1;
    internal const uint WM_NCLBUTTONUP = 0x00A2;
    internal const uint WM_SYSCOMMAND = 0x0112;
    internal const uint WM_MOUSEMOVE = 0x0200;
    internal const uint WM_LBUTTONDOWN = 0x0201;
    internal const uint WM_LBUTTONUP = 0x0202;
    internal const uint WM_CAPTURECHANGED = 0x0215;
    internal const uint WM_ENTERSIZEMOVE = 0x0231;
    internal const uint WM_EXITSIZEMOVE = 0x0232;
    internal const uint WM_NCPOINTERDOWN = 0x0242;
    internal const uint WM_NCPOINTERUP = 0x0243;
    internal const uint WM_POINTERUPDATE = 0x0245;
    internal const uint WM_POINTERDOWN = 0x0246;
    internal const uint WM_POINTERUP = 0x0247;
    internal const uint WM_POINTERENTER = 0x0249;
    internal const uint WM_POINTERLEAVE = 0x024A;
    internal const uint WM_POINTERCAPTURECHANGED = 0x024C;
    internal static readonly IntPtr HTCLIENT = new(1);
    internal static readonly IntPtr HTCAPTION = new(2);
    internal static readonly IntPtr HTTRANSPARENT = new(-1);

    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ScreenToClient(IntPtr hWnd, ref POINT point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string? lpszClass, string? lpszWindow);

    // ── Global hotkey (Epic 5) ────────────────────────────────────

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ── Window subclassing (for WM_HOTKEY interception in WinUI3) ─

    internal const int GWLP_WNDPROC = -4;

    internal delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // 32-bit Windows only has SetWindowLong; 64-bit has SetWindowLongPtr.
    // We wrap both behind a managed helper to avoid EntryPointNotFoundException.

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    internal static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    internal static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
            return GetWindowLongPtr64(hWnd, nIndex);
        return new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    [DllImport("user32.dll")]
    internal static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
