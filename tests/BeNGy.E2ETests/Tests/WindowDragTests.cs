using System.Drawing;
using BeNGy.E2ETests.Helpers;
using FlaUI.Core.Tools;

namespace BeNGy.E2ETests.Tests;

[Collection("BeNGy App")]
[Trait("Category", "E2E")]
public class WindowDragTests
{
    // Header drag shipped broken three times without any test noticing (WinUI
    // gesture pointer capture; WM_NCHITTEST answered but never consulted;
    // hit-test overrides misrouting pointer input) -- it is invisible to
    // UIA-pattern tests. This drives the app's drag state machine
    // (WindowService.BridgeWndProc) through its LEGACY mouse-message dialect
    // by sending WM_LBUTTONDOWN / WM_MOUSEMOVE / WM_LBUTTONUP straight to the
    // bridge window.
    //
    // Why not real input or pointer messages: synthesized input
    // (SendInput/SetCursorPos) proved undebuggably flaky inside the sandbox VM
    // (the pointer pipeline attributed injected presses to stale positions,
    // differently on every run), and fabricated WM_POINTER* messages are
    // DISCARDED by the message pump before reaching any wndproc (the pointer
    // input stack validates them against its live pointer table). Legacy mouse
    // messages are deliverable, deterministic, and the state machine treats
    // them as a first-class dialect. Every message sent here lands on the
    // swallowed path of an armed drag machine, so no fabricated input ever
    // reaches WinUI's own input stack. The one link this cannot cover -- the
    // OS delivering real presses to the bridge as WM_POINTERDOWN -- is
    // standard OS routing, verified by hand.
    //
    // SendMessage (synchronous) rather than PostMessage: each move is
    // processed before the call returns, so the next client-space point can be
    // computed against the window's LIVE position -- client coordinates ride
    // along with the window as it moves.

    private readonly AppTestFixture _fixture;

    public WindowDragTests(AppTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetForTest();
    }

    [Fact]
    public void DragHeader_MouseMessageSequence_MovesWindow_AndDisarmsOnRelease()
    {
        var window = _fixture.GetMainWindow();
        var bridge = GetBridgeWindow(window);
        var before = GetRect(bridgeOwner: window);

        // Header drag zone on the main surface: y in [0, 56), x in [0, 460)
        // (right 100px excluded for the gear + close buttons). The grab point
        // is client (200, 20); the cursor path moves +120,+150 in screen space.
        DragViaMessages(window, bridge, grabClient: new Point(200, 20), screenDelta: new Point(120, 150));

        // Substantial movement in the commanded direction is the contract; the
        // exact delta is asserted loosely because residual DPI virtualization
        // (when the awareness opt-in in AppTestFixture could not take effect)
        // skews the feedback path on scaled displays.
        var afterDrag = GetRect(bridgeOwner: window);
        Assert.True(
            afterDrag.Left - before.Left >= 60 && afterDrag.Top - before.Top >= 60,
            $"Window did not follow the message drag: before=({before.Left},{before.Top}), "
            + $"after=({afterDrag.Left},{afterDrag.Top}).");

        // Disarm/re-arm check via a reversing drag: only works if the UP above
        // disarmed the machine and a fresh DOWN re-arms it. Also parks the
        // window back at its origin for later tests.
        DragViaMessages(window, bridge, grabClient: new Point(200, 20), screenDelta: new Point(-(afterDrag.Left - before.Left), -(afterDrag.Top - before.Top)));

        // Loose tolerance for the same DPI reason; a machine stuck armed after
        // the UP would leave the window far away or moving, not near origin.
        var parked = GetRect(bridgeOwner: window);
        Assert.True(
            Math.Abs(parked.Left - before.Left) < 60 && Math.Abs(parked.Top - before.Top) < 60,
            "Second drag did not return the window: the UP either failed to disarm the machine "
            + $"or a fresh DOWN failed to re-arm it. rect=({parked.Left},{parked.Top}), expected near ({before.Left},{before.Top}).");
    }

    private void DragViaMessages(FlaUI.Core.AutomationElements.Window window, IntPtr bridge, Point grabClient, Point screenDelta)
    {
        var start = GetRect(bridgeOwner: window);
        var grabScreen = new Point(start.Left + grabClient.X, start.Top + grabClient.Y);

        SendMouse(bridge, WM_LBUTTONDOWN, grabClient);

        const int steps = 10;
        for (var i = 1; i <= steps; i++)
        {
            // Screen-space cursor target for this step, translated into the
            // window's CURRENT client space (the window moved last step).
            var target = new Point(
                grabScreen.X + screenDelta.X * i / steps,
                grabScreen.Y + screenDelta.Y * i / steps);
            var current = GetRect(bridgeOwner: window);
            SendMouse(bridge, WM_MOUSEMOVE, new Point(target.X - current.Left, target.Y - current.Top));
            Thread.Sleep(10);
        }

        var final = GetRect(bridgeOwner: window);
        SendMouse(bridge, WM_LBUTTONUP, new Point(
            grabScreen.X + screenDelta.X - final.Left,
            grabScreen.Y + screenDelta.Y - final.Top));
    }

    // ── Plumbing ─────────────────────────────────────────────────────────────

    private const uint WM_MOUSEMOVE = 0x0200;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;

    private IntPtr GetBridgeWindow(FlaUI.Core.AutomationElements.Window window)
    {
        var top = (IntPtr)window.Properties.NativeWindowHandle.Value;
        var bridge = FindWindowEx(top, IntPtr.Zero, "Microsoft.UI.Content.DesktopChildSiteBridge", null);
        Assert.True(bridge != IntPtr.Zero, "DesktopChildSiteBridge child window not found.");
        return bridge;
    }

    private RECT GetRect(FlaUI.Core.AutomationElements.Window bridgeOwner)
    {
        var top = (IntPtr)bridgeOwner.Properties.NativeWindowHandle.Value;
        GetWindowRect(top, out var r);
        return r;
    }

    private static void SendMouse(IntPtr hwnd, uint msg, Point clientPoint)
    {
        // Legacy mouse lParams pack signed 16-bit CLIENT coords. wParam
        // carries button state: the drag handler follows only moves with
        // MK_LBUTTON set, which is how it ignores the button-up moves Windows
        // synthesizes when the window slides under the VM's resting cursor.
        var lparam = new IntPtr(((clientPoint.Y & 0xFFFF) << 16) | (clientPoint.X & 0xFFFF));
        SendMessage(hwnd, msg, new IntPtr(msg == WM_LBUTTONUP ? 0 : 1), lparam);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowTitle);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
