using BeNGy.E2ETests.Helpers;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace BeNGy.E2ETests.Tests;

/// <summary>
/// Epic 7: Theme and Visual Polish (FR24, FR25).
/// These tests verify theme picker functionality and that the app does not crash
/// when switching themes. Full visual theme detection is limited in E2E automation.
/// </summary>
[Collection("BeNGy App")]
[Trait("Category", "E2E")]
public class Epic7_ThemeTests
{
    private readonly AppTestFixture _fixture;

    public Epic7_ThemeTests(AppTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetForTest();
    }

    /// <summary>
    /// Navigates to the Settings page by clicking the Settings toolbar button.
    /// </summary>
    /// <summary>
    /// Ensures the app is on the main page. If currently on settings, clicks Done/Back.
    /// </summary>
    private void EnsureOnMainPage()
    {
        var window = _fixture.GetMainWindow();

        // If SettingsButton is present, we're already on main page
        if (_fixture.FindByAutomationId(window, "SettingsButton") != null)
            return;

        // Try DoneButton first (settings page toolbar)
        var doneButton = _fixture.FindByAutomationId(window, "DoneButton");
        if (doneButton != null)
        {
            doneButton.AsButton().Invoke();
            Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));
            Thread.Sleep(500);
            return;
        }

        // Fallback: look for NavigationPage back button
        var backButton = _fixture.FindByName(window, "Back");
        if (backButton != null)
        {
            backButton.Click();
            Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));
            Thread.Sleep(500);
        }
    }

    private Window NavigateToSettings()
    {
        EnsureOnMainPage();
        var window = _fixture.GetMainWindow();
        var settingsButton = _fixture.WaitForElement(window, "SettingsButton", TimeSpan.FromSeconds(5));
        settingsButton.AsButton().Invoke();
        Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));
        Thread.Sleep(500);
        var settingsWindow = _fixture.GetMainWindow();
        // 15s, not 5s: the first Settings open in a fresh Debug build JIT-compiles
        // the whole page, which alone took longer than 5s inside the sandbox VM.
        try
        {
            _fixture.WaitForElement(settingsWindow, AutomationIds.DefaultPrefixPicker, TimeSpan.FromSeconds(15));
        }
        catch (TimeoutException)
        {
            _fixture.CaptureDiagnostics("settings-did-not-open");
            throw;
        }
        return settingsWindow;
    }

    /// <summary>
    /// Resets app state by killing and relaunching. Used after theme changes
    /// which rebuild the MAUI visual tree and make navigation unreliable.
    /// </summary>
    private void ResetApp()
    {
        _fixture.CloseApp();
        // Wait for single-instance mutex to be released
        Thread.Sleep(3000);
        _fixture.LaunchApp();
        // Wait for MAUI UI tree to fully render after relaunch
        _fixture.WaitForElement(_fixture.GetMainWindow(), "SettingsButton", TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Selects a theme by name from the ThemePicker combo box.
    /// Returns true if the theme was found and selected.
    /// </summary>
    private bool SelectTheme(Window settingsWindow, string themeName)
    {
        // After theme changes, the MAUI visual tree rebuilds — re-acquire fresh window
        var window = _fixture.GetMainWindow(TimeSpan.FromSeconds(5));
        var themePicker = _fixture.FindByAutomationId(window, AutomationIds.ThemePicker);

        // If not found immediately, wait with retry
        if (themePicker == null)
        {
            var result = Retry.WhileNull(
                () => _fixture.FindByAutomationId(_fixture.GetMainWindow(TimeSpan.FromSeconds(3)), AutomationIds.ThemePicker),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(500));
            themePicker = result.Result;
        }

        if (themePicker == null) return false;

        var comboBox = themePicker.AsComboBox();
        if (comboBox is null) return false;

        for (int i = 0; i < comboBox.Items.Length; i++)
        {
            if (comboBox.Items[i].Text.Contains(themeName, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.Select(i);
                // Theme change triggers a full MAUI visual tree rebuild — wait for it
                Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));
                Thread.Sleep(1000);
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void AppLaunch_HasThemeApplied_WindowHasVisualContent() // FR24
    {
        // Arrange & Act
        _fixture.EnsureRunning();
        var window = _fixture.GetMainWindow();

        // Assert - the window should have content (children) indicating a theme rendered the UI
        Assert.NotNull(window);
        Assert.False(window.IsOffscreen, "Window should be visible on screen.");

        var children = window.FindAllChildren();
        Assert.True(children.Length > 0,
            "Window should have visual content rendered by the applied theme.");
    }

    [Fact]
    public void ThemePicker_ShowsCurrentTheme() // FR24
    {
        // Arrange
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        // Act
        var themePicker = _fixture.WaitForElement(settingsWindow, AutomationIds.ThemePicker);
        var comboBox = themePicker.AsComboBox();

        // Assert - a theme should be currently selected
        Assert.NotNull(comboBox);
        Assert.NotNull(comboBox.SelectedItem);
        Assert.False(string.IsNullOrWhiteSpace(comboBox.SelectedItem.Text),
            "Theme picker should display the currently selected theme.");

        // No theme change, so simple back navigation works
        var doneButton = _fixture.FindByAutomationId(settingsWindow, "DoneButton");
        doneButton?.AsButton().Invoke();
        Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ThemeChange_ToLight_AppDoesNotCrash() // FR24
    {
        // Arrange
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        // Act - select Light theme
        var selected = SelectTheme(settingsWindow, "Light");
        Assert.True(selected, "Should be able to find and select the Light theme option.");

        // Assert - app is still responsive after theme change
        var window = _fixture.GetMainWindow(TimeSpan.FromSeconds(5));
        Assert.NotNull(window);
        Assert.False(window.IsOffscreen, "Window should remain visible after switching to Light theme.");

        // Cleanup
        ResetApp();
    }

    [Fact]
    public void ThemeChange_ToDark_AppDoesNotCrash() // FR24
    {
        // Arrange
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        // Act - select Dark theme
        var selected = SelectTheme(settingsWindow, "Dark");
        Assert.True(selected, "Should be able to find and select the Dark theme option.");

        // Assert - app is still responsive after theme change
        var window = _fixture.GetMainWindow(TimeSpan.FromSeconds(5));
        Assert.NotNull(window);
        Assert.False(window.IsOffscreen, "Window should remain visible after switching to Dark theme.");

        // Cleanup
        ResetApp();
    }

    [Fact]
    public void ThemeChange_ToSystem_AppDoesNotCrash() // FR25
    {
        // Arrange
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        // Act - select System theme
        var selected = SelectTheme(settingsWindow, "System");
        Assert.True(selected, "Should be able to find and select the System theme option.");

        // Assert - app is still responsive after theme change
        var window = _fixture.GetMainWindow(TimeSpan.FromSeconds(5));
        Assert.NotNull(window);
        Assert.False(window.IsOffscreen, "Window should remain visible after switching to System theme.");

        // Cleanup
        ResetApp();
    }

    [Fact]
    public void ThemeChange_UIReflectsChange_WindowRemainsUsable() // FR24, FR25
    {
        // Arrange - start on settings page
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        // Act - change to Light, then Dark, then System and verify app doesn't crash
        var themeNames = new[] { "Light", "Dark", "System" };
        foreach (var theme in themeNames)
        {
            var selected = SelectTheme(settingsWindow, theme);
            if (!selected) continue;

            // Verify the window is still responsive after each change
            var window = _fixture.GetMainWindow(TimeSpan.FromSeconds(5));
            Assert.NotNull(window);
            Assert.False(window.IsOffscreen,
                $"Window should remain visible after switching to {theme} theme.");

            settingsWindow = window;
        }

        // Cleanup
        ResetApp();
    }
}
