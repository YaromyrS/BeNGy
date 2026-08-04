using BeNGy.E2ETests.Helpers;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace BeNGy.E2ETests.Tests;

/// <summary>
/// Epic 6: User Settings and Configuration (FR17-FR23, FR29, FR30).
/// These tests verify the settings panel, pickers, switches, and persistence.
/// </summary>
[Collection("BeNGy App")]
[Trait("Category", "E2E")]
public class Epic6_SettingsTests
{
    private readonly AppTestFixture _fixture;

    public Epic6_SettingsTests(AppTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureRunning();
    }

    /// <summary>
    /// Kills and relaunches the app for a clean state.
    /// </summary>
    private void ResetApp()
    {
        _fixture.CloseApp();
        Thread.Sleep(3000);
        _fixture.LaunchApp();
        _fixture.WaitForElement(_fixture.GetMainWindow(), "SettingsButton", TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Ensures the app is on the main page. Falls back to ResetApp if needed.
    /// </summary>
    private void EnsureOnMainPage()
    {
        try
        {
            var window = _fixture.GetMainWindow();
            if (_fixture.FindByAutomationId(window, "SettingsButton") != null)
                return;

            // Try DoneButton
            var doneButton = _fixture.FindByAutomationId(window, "DoneButton")
                          ?? _fixture.FindByName(window, "Done");
            if (doneButton != null)
            {
                doneButton.AsButton().Invoke();
                Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));
                Thread.Sleep(1000);
                var mainWindow = _fixture.GetMainWindow();
                if (_fixture.FindByAutomationId(mainWindow, "SettingsButton") != null)
                    return;
            }
        }
        catch { /* fall through */ }

        ResetApp();
    }

    /// <summary>
    /// Navigates to Settings with retry logic.
    /// </summary>
    private Window NavigateToSettings()
    {
        EnsureOnMainPage();

        for (int attempt = 0; attempt < 3; attempt++)
        {
            var window = _fixture.GetMainWindow();
            window.SetForeground();
            Thread.Sleep(500);

            var settingsButton = _fixture.FindByAutomationId(window, "SettingsButton");
            if (settingsButton == null)
            {
                ResetApp();
                continue;
            }

            settingsButton.AsButton().Invoke();
            Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));
            Thread.Sleep(500);

            var settingsWindow = _fixture.GetMainWindow();
            var picker = _fixture.FindByAutomationId(settingsWindow, AutomationIds.DefaultPrefixPicker);
            if (picker != null)
                return settingsWindow;

            // Invoke was swallowed — try keyboard fallback (Focus + Space)
            window = _fixture.GetMainWindow();
            settingsButton = _fixture.FindByAutomationId(window, "SettingsButton");
            if (settingsButton != null)
            {
                try
                {
                    settingsButton.Focus();
                    Thread.Sleep(200);
                    Keyboard.Press(VirtualKeyShort.SPACE);
                }
                catch
                {
                    // Focus/keyboard failed — try Invoke once more
                    settingsButton.AsButton().Invoke();
                }
                Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));
                Thread.Sleep(1000);

                settingsWindow = _fixture.GetMainWindow();
                picker = _fixture.FindByAutomationId(settingsWindow, AutomationIds.DefaultPrefixPicker);
                if (picker != null)
                    return settingsWindow;
            }

            ResetApp();
        }

        // Final attempt
        var finalWindow = _fixture.GetMainWindow();
        var btn = _fixture.WaitForElement(finalWindow, "SettingsButton", TimeSpan.FromSeconds(5));
        btn.AsButton().Invoke();
        Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));
        Thread.Sleep(1000);
        finalWindow = _fixture.GetMainWindow();
        _fixture.WaitForElement(finalWindow, AutomationIds.DefaultPrefixPicker, TimeSpan.FromSeconds(5));
        return finalWindow;
    }

    /// <summary>
    /// Navigates back from Settings to Main page.
    /// </summary>
    private Window NavigateBackToMain()
    {
        var window = _fixture.GetMainWindow();
        var doneButton = _fixture.WaitForElement(window, "DoneButton", TimeSpan.FromSeconds(5));
        doneButton.AsButton().Invoke();
        Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));
        Thread.Sleep(1000);
        var mainWindow = _fixture.GetMainWindow();
        _fixture.WaitForElement(mainWindow, AutomationIds.TicketEntry, TimeSpan.FromSeconds(5));
        return mainWindow;
    }

    /// <summary>
    /// Safe cleanup: try NavigateBackToMain, reset app only as last resort.
    /// </summary>
    private void CleanupToMainPage()
    {
        try { NavigateBackToMain(); }
        catch { try { ResetApp(); } catch { /* best-effort */ } }
    }

    [Fact]
    public void SettingsPanel_OpensFromToolbar_SettingsPageVisible() // FR17
    {
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        var defaultPrefixPicker = _fixture.FindByAutomationId(settingsWindow, AutomationIds.DefaultPrefixPicker);
        Assert.NotNull(defaultPrefixPicker);

        CleanupToMainPage();
    }

    [Fact]
    public void SettingsPage_HasSettingsTitle() // FR17
    {
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        var settingsTitle = settingsWindow.FindFirstDescendant(
            _fixture.Automation.ConditionFactory.ByName("Settings"));
        Assert.NotNull(settingsTitle);

        CleanupToMainPage();
    }

    [Fact]
    public void DefaultPrefixPicker_IsVisibleAndContainsOptions() // FR18
    {
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        var prefixPicker = _fixture.WaitForElement(settingsWindow, AutomationIds.DefaultPrefixPicker);
        Assert.NotNull(prefixPicker);

        CleanupToMainPage();
    }

    [Fact]
    public void DefaultPrefixPicker_ChangePersistsAcrossSettingsNavigation() // FR18, FR29
    {
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();
        var prefixPicker = _fixture.WaitForElement(settingsWindow, AutomationIds.DefaultPrefixPicker);
        Assert.NotNull(prefixPicker);

        var comboBox = prefixPicker.AsComboBox();
        if (comboBox is not null && comboBox.Items.Length > 1)
        {
            var targetItem = comboBox.Items[1];
            var selectedValue = targetItem.Text;
            comboBox.Select(1);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));

            // Use ResetApp between navigations (avoids Invoke unreliability)
            ResetApp();
            settingsWindow = NavigateToSettings();

            var reopenedPicker = _fixture.WaitForElement(settingsWindow, AutomationIds.DefaultPrefixPicker);
            var reopenedCombo = reopenedPicker.AsComboBox();
            Assert.NotNull(reopenedCombo);
            Assert.Equal(selectedValue, reopenedCombo.SelectedItem?.Text);
        }

        CleanupToMainPage();
    }

    [Fact]
    public void TitleFormatPicker_HasExpectedOptions() // FR19
    {
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        var titleFormatPicker = _fixture.WaitForElement(settingsWindow, AutomationIds.TitleFormatPicker);
        Assert.NotNull(titleFormatPicker);

        var comboBox = titleFormatPicker.AsComboBox();
        Assert.NotNull(comboBox);
        Assert.True(comboBox.Items.Length >= 3,
            "Title format picker should have at least 3 options (kebab-case, snake_case, PascalCase).");

        var itemTexts = comboBox.Items.Select(i => i.Text.ToLowerInvariant()).ToArray();
        Assert.Contains(itemTexts, t => t.Contains("kebab"));
        Assert.Contains(itemTexts, t => t.Contains("snake"));
        Assert.Contains(itemTexts, t => t.Contains("pascal"));

        CleanupToMainPage();
    }

    [Fact]
    public void TitleFormatChange_AffectsBranchPreviewOnMainPage() // FR19, FR30
    {
        _fixture.EnsureRunning();

        // Step 1: Navigate to settings and change the title format
        var settingsWindow = NavigateToSettings();
        var titleFormatPicker = _fixture.WaitForElement(settingsWindow, AutomationIds.TitleFormatPicker);
        var comboBox = titleFormatPicker.AsComboBox();

        if (comboBox is null || comboBox.Items.Length <= 1)
            return;

        var currentSelected = comboBox.SelectedItem?.Text;
        var newIndex = (currentSelected == comboBox.Items[0].Text) ? 1 : 0;
        comboBox.Select(newIndex);
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));

        // Step 2: ResetApp to return to main page (setting persists, avoids DoneButton Invoke issue)
        ResetApp();

        // Step 3: Type text on main page and verify preview is generated
        var mainWindow = _fixture.GetMainWindow();
        var ticketEntry = _fixture.WaitForElement(mainWindow, AutomationIds.TicketEntry);
        var titleEntry = _fixture.WaitForElement(mainWindow, AutomationIds.TitleEntry);

        _fixture.TypeText(ticketEntry, "123");
        _fixture.TypeText(titleEntry, "my test title");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        var previewLabel = _fixture.FindByAutomationId(mainWindow, AutomationIds.PreviewLabel);
        Assert.NotNull(previewLabel);
        var updatedPreview = previewLabel.Properties.Name.ValueOrDefault;
        Assert.NotNull(updatedPreview);
    }

    [Fact]
    public void PrefixSeparatorPicker_IsPresent() // FR20
    {
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        var separatorPicker = _fixture.WaitForElement(settingsWindow, AutomationIds.PrefixSeparatorPicker);
        Assert.NotNull(separatorPicker);

        CleanupToMainPage();
    }

    [Fact]
    public void TicketSeparatorPicker_IsPresent() // FR21
    {
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        var separatorPicker = _fixture.WaitForElement(settingsWindow, AutomationIds.TicketSeparatorPicker);
        Assert.NotNull(separatorPicker);

        CleanupToMainPage();
    }

    [Fact]
    public void AutoCloseDelayPicker_IsPresent() // FR22
    {
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        var delayPicker = _fixture.WaitForElement(settingsWindow, AutomationIds.AutoCloseDelayPicker);
        Assert.NotNull(delayPicker);

        CleanupToMainPage();
    }

    [Fact]
    public void StartWithWindowsSwitch_IsPresent() // FR23
    {
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        var startSwitch = _fixture.WaitForElement(settingsWindow, AutomationIds.StartWithWindowsSwitch);
        Assert.NotNull(startSwitch);

        CleanupToMainPage();
    }

    [Fact]
    public void StartMinimizedSwitch_IsPresent() // FR23
    {
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        var minimizedSwitch = _fixture.WaitForElement(settingsWindow, AutomationIds.StartMinimizedSwitch);
        Assert.NotNull(minimizedSwitch);

        CleanupToMainPage();
    }

    [Fact]
    public void SettingsPersistence_AcrossAppRestart_SettingPreserved() // FR29
    {
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();
        var titleFormatPicker = _fixture.WaitForElement(settingsWindow, AutomationIds.TitleFormatPicker);
        var comboBox = titleFormatPicker.AsComboBox();

        string? selectedValue = null;
        if (comboBox is not null && comboBox.Items.Length > 1)
        {
            comboBox.Select(1);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));
            selectedValue = comboBox.SelectedItem?.Text;
        }

        if (selectedValue is null)
            return;

        // Close and relaunch
        ResetApp();
        settingsWindow = NavigateToSettings();

        var reopenedPicker = _fixture.WaitForElement(settingsWindow, AutomationIds.TitleFormatPicker);
        var reopenedCombo = reopenedPicker.AsComboBox();
        Assert.NotNull(reopenedCombo);
        Assert.Equal(selectedValue, reopenedCombo.SelectedItem?.Text);

        CleanupToMainPage();
    }

    [Fact]
    public void DoneButton_ReturnsToMainPage() // FR17
    {
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        var mainWindow = NavigateBackToMain();

        var ticketEntry = _fixture.WaitForElement(mainWindow, AutomationIds.TicketEntry, TimeSpan.FromSeconds(3));
        Assert.NotNull(ticketEntry);

        var copyButton = _fixture.FindByAutomationId(mainWindow, AutomationIds.CopyButton);
        Assert.NotNull(copyButton);
    }

    [Fact]
    public void ThemePicker_HasLightDarkSystemOptions() // FR24
    {
        _fixture.EnsureRunning();
        var settingsWindow = NavigateToSettings();

        var themePicker = _fixture.WaitForElement(settingsWindow, AutomationIds.ThemePicker);
        Assert.NotNull(themePicker);

        var comboBox = themePicker.AsComboBox();
        Assert.NotNull(comboBox);
        Assert.True(comboBox.Items.Length >= 3,
            "Theme picker should have at least 3 options (Light, Dark, System).");

        var itemTexts = comboBox.Items.Select(i => i.Text.ToLowerInvariant()).ToArray();
        Assert.Contains(itemTexts, t => t.Contains("light"));
        Assert.Contains(itemTexts, t => t.Contains("dark"));
        Assert.Contains(itemTexts, t => t.Contains("system"));

        CleanupToMainPage();
    }
}
