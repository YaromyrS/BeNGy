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
        _fixture.EnsureRunning();
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

    // Note: Tray icon tests (TrayIcon_ContextMenu, TrayIcon_ExitClicked) are
    // environment-dependent and may not work in all CI configurations.
    // They are kept but may need [Trait("Requirement", "TrayIcon")] for selective runs.

    [Fact]
    [Trait("Requirement", "TrayIcon")]
    public void TrayIcon_ContextMenu_HasExpectedMenuItems() // FR5, FR27
    {
        var app = _fixture.EnsureRunning();
        var window = _fixture.GetMainWindow();

        // Hide window to tray
        window.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
        _fixture.WaitForWindowClose(TimeSpan.FromSeconds(5));

        var desktop = _fixture.Automation.GetDesktop();
        var notifyIcon = Retry.WhileNull(
            () =>
            {
                var candidates = desktop.FindAllDescendants(
                    _fixture.Automation.ConditionFactory.ByName("BeNGy"));
                return candidates.FirstOrDefault();
            },
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(500));

        if (notifyIcon.Result is not null)
        {
            notifyIcon.Result.RightClick();
            Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));

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
        else
        {
            // Tray icon may be in overflow area
            _fixture.EnsureRunning();
            Assert.Fail("Could not locate BeNGy tray icon in the notification area.");
        }
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

        var desktop = _fixture.Automation.GetDesktop();
        var notifyIcon = Retry.WhileNull(
            () =>
            {
                var candidates = desktop.FindAllDescendants(
                    _fixture.Automation.ConditionFactory.ByName("BeNGy"));
                return candidates.FirstOrDefault();
            },
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(500));

        if (notifyIcon.Result is not null)
        {
            notifyIcon.Result.RightClick();
            Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));

            var exitItem = desktop.FindFirstDescendant(
                _fixture.Automation.ConditionFactory.ByName("Exit"));
            Assert.NotNull(exitItem);
            exitItem.Click();

            // Process should terminate
            Thread.Sleep(2000);
            Assert.True(app.HasExited, "Process should have terminated after Exit.");
        }
        else
        {
            Assert.Fail("Could not locate BeNGy tray icon in the notification area.");
        }

        // Relaunch for subsequent tests (this is the only test that truly kills the app)
        _fixture.LaunchApp();
    }
}
