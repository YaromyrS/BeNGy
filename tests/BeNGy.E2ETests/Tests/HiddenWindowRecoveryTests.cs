using System.Diagnostics;
using BeNGy.E2ETests.Helpers;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace BeNGy.E2ETests.Tests;

/// <summary>
/// Regression tests for the test harness itself.
///
/// BeNGy hides its window rather than closing it -- after a copy (auto-close),
/// on Escape, and on close-to-tray. A hidden window reports
/// Process.MainWindowHandle == IntPtr.Zero, and FlaUI's parameterless
/// Application.GetMainWindow(automation) overload waits on that handle with
/// TimeSpan.FromMilliseconds(-1) (infinite). That combination previously wedged
/// the entire suite: Epic4 CopyButton_BranchNameEmpty_IsDisabled blocked for
/// 15m01s on a call whose declared timeout was 5s, and the run had to be killed.
/// </summary>
[Collection("BeNGy App")]
[Trait("Category", "E2E")]
public class HiddenWindowRecoveryTests
{
    private readonly AppTestFixture _fixture;

    public HiddenWindowRecoveryTests(AppTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetForTest();
    }

    [Fact]
    public void HiddenWindow_GetMainWindow_FailsFastInsteadOfBlockingForever()
    {
        _fixture.EnsureRunning();
        _fixture.HideMainWindow();
        Assert.False(_fixture.HasVisibleMainWindow(), "Window should be hidden for this test to be meaningful.");

        // The contract: a bounded wait must be bounded. Whether it finds the
        // window or throws is secondary -- it must not block past its timeout.
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _fixture.GetMainWindow(TimeSpan.FromSeconds(2));
        }
        catch (TimeoutException)
        {
            // Acceptable outcome -- the point is that it returned at all.
        }
        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(20),
            $"GetMainWindow(2s) took {stopwatch.Elapsed.TotalSeconds:F1}s -- the timeout is not bounding the call.");
    }

    [Fact]
    public void HiddenWindow_EnsureRunning_SummonsWindowBackForTheNextTest()
    {
        _fixture.EnsureRunning();
        _fixture.HideMainWindow();
        Assert.False(_fixture.HasVisibleMainWindow(), "Window should be hidden for this test to be meaningful.");

        // This is what every test class does in its constructor. After a
        // hide-to-tray it must leave the app interactive again.
        var stopwatch = Stopwatch.StartNew();
        _fixture.EnsureRunning();
        var window = _fixture.GetMainWindow(TimeSpan.FromSeconds(10));
        stopwatch.Stop();

        Assert.NotNull(window);
        Assert.False(window.IsOffscreen, "Summoned window should be on screen.");
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(30),
            $"Recovery took {stopwatch.Elapsed.TotalSeconds:F1}s.");

        // And the UI is genuinely usable, not just present.
        var ticketEntry = _fixture.WaitForElement(window, AutomationIds.TicketEntry, TimeSpan.FromSeconds(5));
        Assert.NotNull(ticketEntry);
    }

    [Fact]
    public void EscapeDismiss_ThenSummon_LeavesFormUsable()
    {
        _fixture.EnsureRunning();
        var window = _fixture.GetMainWindow();

        var ticketEntry = _fixture.WaitForElement(window, AutomationIds.TicketEntry);
        _fixture.TypeText(ticketEntry, "PROJ-77");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));

        window.SetForeground();
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        _fixture.EnsureRunning();
        var restored = _fixture.GetMainWindow(TimeSpan.FromSeconds(10));

        var restoredTicket = _fixture.WaitForElement(restored, AutomationIds.TicketEntry, TimeSpan.FromSeconds(5));
        Assert.NotNull(restoredTicket);
    }
}
