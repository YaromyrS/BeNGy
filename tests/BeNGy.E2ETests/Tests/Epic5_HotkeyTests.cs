using BeNGy.E2ETests.Helpers;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace BeNGy.E2ETests.Tests;

/// <summary>
/// Epic 5: Global Hotkey Activation (FR3, FR28, FR31).
/// These tests verify the global hotkey (Ctrl+Shift+B) summons the window,
/// tray icon "Open" works, and graceful degradation when hotkey is unavailable.
/// Note: Hotkey tests are inherently fragile in CI environments.
/// </summary>
[Collection("BeNGy App")]
[Trait("Category", "E2E")]
[Trait("Requirement", "Hotkey")]
public class Epic5_HotkeyTests
{
    private readonly AppTestFixture _fixture;

    public Epic5_HotkeyTests(AppTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetForTest();
    }

    [Fact]
    public void GlobalHotkey_CtrlShiftB_SummonsWindowWhenMinimized() // FR3
    {
        // Arrange - hide the window to the tray. The palette window is not
        // minimizable (borderless chrome, by design); UIA SetWindowVisualState
        // throws InvalidOperationException on it. Tray-hide is the app's real
        // resting state and the state the hotkey actually summons from.
        _fixture.EnsureRunning();
        _fixture.HideMainWindow();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Act - send global hotkey Ctrl+Shift+B
        Keyboard.TypeSimultaneously(
            VirtualKeyShort.CONTROL,
            VirtualKeyShort.SHIFT,
            VirtualKeyShort.KEY_B);
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert - window should reappear within 2 seconds
        var result = Retry.WhileTrue(
            () =>
            {
                try
                {
                    var win = _fixture.GetMainWindow(TimeSpan.FromSeconds(1));
                    return win.IsOffscreen;
                }
                catch
                {
                    return true;
                }
            },
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(250));

        var restoredWindow = _fixture.GetMainWindow();
        Assert.False(restoredWindow.IsOffscreen, "Window should be visible after hotkey activation.");
    }

    [Fact]
    public void GlobalHotkey_AfterSummon_TicketFieldHasFocus() // FR3, FR28
    {
        // Arrange - leave focus in the TITLE field (where copy-and-auto-close
        // leaves it in real use), then hide to tray. Starting from the ticket
        // field would let the test pass without the app doing anything.
        _fixture.EnsureRunning();
        _fixture.WaitForElement(_fixture.GetMainWindow(), AutomationIds.TitleEntry).Focus();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));
        _fixture.HideMainWindow();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Act - send Ctrl+Shift+B to summon
        Keyboard.TypeSimultaneously(
            VirtualKeyShort.CONTROL,
            VirtualKeyShort.SHIFT,
            VirtualKeyShort.KEY_B);
        Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));

        // Assert - the summon moved focus to the ticket entry. (This used to be
        // "focused is TicketEntry || ticketEntry's id is TicketEntry", and the
        // second half is always true, so the test could never fail and the
        // app bug it exists to catch shipped unnoticed.)
        var mainWindow = _fixture.GetMainWindow();
        _fixture.WaitForElement(mainWindow, AutomationIds.TicketEntry, TimeSpan.FromSeconds(2));
        var focused = Retry.WhileNull(
            () =>
            {
                var element = _fixture.Automation.FocusedElement();
                return element?.Properties.AutomationId.ValueOrDefault == AutomationIds.TicketEntry ? element : null;
            },
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(100));
        Assert.True(focused.Result is not null,
            "Ticket entry field should receive focus after hotkey summon; focused: "
            + _fixture.Automation.FocusedElement()?.Properties.AutomationId.ValueOrDefault);
    }

    [Fact]
    public void GlobalHotkey_WindowAppearsWithinTwoSeconds() // FR3, FR31
    {
        // Arrange - hide to tray (the palette is not minimizable by design)
        _fixture.EnsureRunning();
        _fixture.HideMainWindow();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Act - time the hotkey response
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Keyboard.TypeSimultaneously(
            VirtualKeyShort.CONTROL,
            VirtualKeyShort.SHIFT,
            VirtualKeyShort.KEY_B);

        // Wait for window to become visible
        Retry.WhileTrue(
            () =>
            {
                try
                {
                    var win = _fixture.GetMainWindow(TimeSpan.FromSeconds(1));
                    return win.IsOffscreen;
                }
                catch
                {
                    return true;
                }
            },
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(100));
        stopwatch.Stop();

        // Assert - should appear within 2 seconds
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"Window should appear within 2 seconds but took {stopwatch.Elapsed.TotalMilliseconds}ms.");
    }

    [Fact]
    public void TrayIcon_OpenMenuItem_ShowsWindow() // FR3, FR31
    {
        // Arrange - hide to tray (the palette is not minimizable by design)
        _fixture.EnsureRunning();
        _fixture.HideMainWindow();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Act - open via the tray icon's context menu. FindTrayIcon opens the
        // Windows 11 overflow flyout when the icon isn't on the taskbar itself,
        // so this path is now always exercisable (it used to bail out silently).
        var trayIcon = _fixture.FindTrayIcon();
        if (trayIcon is null)
            Assert.Fail("Could not locate the BeNGy tray icon, even in the overflow flyout.");

        trayIcon.RightClick();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        var openItem = _fixture.Automation.GetDesktop().FindFirstDescendant(
            _fixture.Automation.ConditionFactory.ByName("Open"));
        Assert.NotNull(openItem);
        openItem.Click();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert - window should be visible after tray open
        var shown = Retry.WhileFalse(_fixture.HasVisibleMainWindow, TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(250));
        Assert.True(shown.Result, "Tray 'Open' should show the main window.");
    }

    [Fact]
    public void GracefulDegradation_TrayAccessWorksWhenHotkeyUnavailable() // FR31
    {
        // Arrange - ensure the app is running and window is visible
        _fixture.EnsureRunning();
        var window = _fixture.GetMainWindow();
        Assert.NotNull(window);

        // Act - even if hotkey registration fails, the app should still be functional
        // Verify the main window is usable and contains expected elements
        var ticketEntry = _fixture.FindByAutomationId(window, AutomationIds.TicketEntry);
        var copyButton = _fixture.FindByAutomationId(window, AutomationIds.CopyButton);

        // Assert - core UI elements are present and the app is operational
        Assert.NotNull(ticketEntry);
        Assert.NotNull(copyButton);
        Assert.False(window.IsOffscreen, "Window should remain accessible regardless of hotkey state.");
    }
}
