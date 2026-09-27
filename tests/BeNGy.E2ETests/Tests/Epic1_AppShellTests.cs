using System.Diagnostics;
using BeNGy.E2ETests.Helpers;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace BeNGy.E2ETests.Tests;

[Collection("BeNGy App")]
[Trait("Category", "E2E")]
public class Epic1_AppShellTests
{
    private readonly AppTestFixture _fixture;

    public Epic1_AppShellTests(AppTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetForTest();
    }

    [Fact]
    public void AppLaunch_StartsSuccessfully_MainWindowAppears() // FR1
    {
        var window = _fixture.GetMainWindow();
        Assert.NotNull(window);
    }

    [Fact]
    public void AppLaunch_WindowTitle_ContainsBeNGy() // FR2
    {
        var window = _fixture.GetMainWindow();
        Assert.Contains("BeNGy", window.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CloseButton_Clicked_HidesWindowButProcessStillRunning() // FR4, FR26
    {
        var app = _fixture.EnsureRunning();
        var window = _fixture.GetMainWindow();

        // Act — Alt+F4 simulates close
        window.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
        var windowClosed = _fixture.WaitForWindowClose(TimeSpan.FromSeconds(5));

        // Assert — window hidden but process alive (minimized to tray)
        Assert.True(windowClosed, "Window should have closed/hidden.");
        Assert.False(app.HasExited, "Process should still be running (hidden to tray).");

        // Restore for subsequent tests
        _fixture.EnsureRunning();
    }

    [Fact]
    public void SingleInstance_SecondLaunch_ActivatesExistingWindow() // FR6
    {
        _fixture.EnsureRunning();
        var firstWindow = _fixture.GetMainWindow();
        Assert.NotNull(firstWindow);

        // Attempt to launch a second instance
        var secondProcess = Process.Start(new ProcessStartInfo
        {
            FileName = _fixture.ExePath,
            UseShellExecute = false
        });

        secondProcess?.WaitForExit(5000);

        // Only one BeNGy process should be running
        var runningProcesses = Process.GetProcessesByName("BeNGy");
        try
        {
            Assert.Single(runningProcesses);
        }
        finally
        {
            foreach (var p in runningProcesses)
                p.Dispose();
            secondProcess?.Dispose();
        }
    }

    // Tray tests find the icon through AppTestFixture.FindTrayIcon, which opens
    // Windows 11's "Show Hidden Icons" flyout when the icon is not promoted to
    // the taskbar (the default for a new icon, and why these tests used to fail).

    [Fact]
    [Trait("Requirement", "TrayIcon")]
    public void TrayIcon_ContextMenu_HasExpectedMenuItems() // FR5, FR27
    {
        _fixture.EnsureRunning();
        var window = _fixture.GetMainWindow();

        // Hide window to tray
        window.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
        _fixture.WaitForWindowClose(TimeSpan.FromSeconds(5));

        var notifyIcon = _fixture.FindTrayIcon();
        if (notifyIcon is null)
            Assert.Fail("Could not locate the BeNGy tray icon, even in the overflow flyout.");

        notifyIcon.RightClick();
        Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));

        var desktop = _fixture.Automation.GetDesktop();
        var openItem = desktop.FindFirstDescendant(
            _fixture.Automation.ConditionFactory.ByName("Open"));
        var settingsItem = desktop.FindFirstDescendant(
            _fixture.Automation.ConditionFactory.ByName("Settings"));
        var exitItem = desktop.FindFirstDescendant(
            _fixture.Automation.ConditionFactory.ByName("Exit"));

        Assert.NotNull(openItem);
        Assert.NotNull(settingsItem);
        Assert.NotNull(exitItem);

        // Click Open to restore window for subsequent tests
        openItem.Click();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    [Trait("Requirement", "TrayIcon")]
    public void TrayIcon_ExitClicked_TerminatesProcess() // FR5, FR27
    {
        var app = _fixture.EnsureRunning();
        var window = _fixture.GetMainWindow();

        // Hide to tray
        window.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
        _fixture.WaitForWindowClose(TimeSpan.FromSeconds(5));

        try
        {
            var notifyIcon = _fixture.FindTrayIcon();
            if (notifyIcon is null)
                Assert.Fail("Could not locate the BeNGy tray icon, even in the overflow flyout.");

            notifyIcon.RightClick();
            Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));

            var exitItem = _fixture.Automation.GetDesktop().FindFirstDescendant(
                _fixture.Automation.ConditionFactory.ByName("Exit"));
            Assert.NotNull(exitItem);
            exitItem.Click();

            // Process should terminate
            var exited = Retry.WhileFalse(() => app.HasExited, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(250));
            Assert.True(exited.Result, "Process should have terminated after Exit.");
        }
        finally
        {
            // Relaunch for subsequent tests (the only test that truly kills the
            // app); in finally so a failed assertion can't leave the suite
            // without an app.
            _fixture.LaunchApp();
        }
    }
}
