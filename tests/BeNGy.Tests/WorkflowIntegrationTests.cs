using BeNGy.Models;
using BeNGy.Services;
using BeNGy.Services.Interfaces;
using BeNGy.ViewModels;
using NSubstitute;

namespace BeNGy.Tests;

/// <summary>
/// End-to-end workflow tests that exercise complete user scenarios through
/// real service chains (BranchNameGenerator, GitValidator) with only
/// external boundaries (clipboard, window, preferences) substituted.
/// </summary>
public class WorkflowIntegrationTests
{
    private readonly IBranchNameGenerator _generator = new BranchNameGenerator();
    private readonly IGitValidator _validator = new GitValidator();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly IWindowService _windowService = Substitute.For<IWindowService>();
    private readonly IPreferencesService _preferences = Substitute.For<IPreferencesService>();

    public WorkflowIntegrationTests()
    {
        _clipboard.CopyToClipboardAsync(Arg.Any<string>()).Returns(true);

        // Wire up default preference values
        _preferences.Get(PreferenceKeys.DefaultPrefix, PreferenceKeys.DefaultPrefixValue)
            .Returns(PreferenceKeys.DefaultPrefixValue);
        _preferences.Get(PreferenceKeys.AutoCloseDelayMs, PreferenceKeys.AutoCloseDelayMsDefault)
            .Returns(0); // instant for fast tests
        _preferences.Get(PreferenceKeys.TitleFormat, PreferenceKeys.TitleFormatDefault)
            .Returns(PreferenceKeys.TitleFormatDefault);
        _preferences.Get(PreferenceKeys.PrefixTicketSeparator, PreferenceKeys.PrefixTicketSeparatorDefault)
            .Returns(PreferenceKeys.PrefixTicketSeparatorDefault);
        _preferences.Get(PreferenceKeys.TicketTitleSeparator, PreferenceKeys.TicketTitleSeparatorDefault)
            .Returns(PreferenceKeys.TicketTitleSeparatorDefault);
        _preferences.Get(PreferenceKeys.StartMinimized, PreferenceKeys.StartMinimizedDefault)
            .Returns(PreferenceKeys.StartMinimizedDefault);
        _preferences.Get(PreferenceKeys.HotkeyModifiers, PreferenceKeys.HotkeyModifiersDefault)
            .Returns(PreferenceKeys.HotkeyModifiersDefault);
        _preferences.Get(PreferenceKeys.HotkeyKey, PreferenceKeys.HotkeyKeyDefault)
            .Returns(PreferenceKeys.HotkeyKeyDefault);
    }

    private MainViewModel CreateMainVm() =>
        new(_generator, _validator, _clipboard, _windowService, _preferences);

    private SettingsViewModel CreateSettingsVm()
    {
        var themeService = Substitute.For<IThemeService>();
        themeService.CurrentTheme.Returns(BeNGyTheme.System);
        var hotkeyService = Substitute.For<IHotkeyService>();
        hotkeyService.IsRegistered.Returns(true);
        var startupService = Substitute.For<IStartupService>();
        return new SettingsViewModel(_preferences, themeService, hotkeyService, startupService);
    }

    // ── 1. Complete Generate-Copy Workflow ────────────────────────────

    [Fact]
    public async Task GenerateCopyWorkflow_SetsInputs_CopiesCorrectValue_HidesWindow_ClearsForm()
    {
        var vm = CreateMainVm();

        vm.Prefix = "feature";
        vm.TicketNumber = "PROJ-123";
        vm.Title = "Add User Login";

        Assert.Equal("feature/PROJ-123_add-user-login", vm.GeneratedBranchName);
        Assert.True(vm.IsValid);
        Assert.True(vm.CopyCommand.CanExecute(null));

        await vm.CopyCommand.ExecuteAsync(null);

        await _clipboard.Received(1).CopyToClipboardAsync("feature/PROJ-123_add-user-login");
        _windowService.Received(1).HideWindow();
        Assert.Equal(string.Empty, vm.TicketNumber);
        Assert.Equal(string.Empty, vm.Title);
        Assert.Equal("feature", vm.Prefix);
    }

    // ── 2. Settings Affect Generation ────────────────────────────────

    [Fact]
    public void SettingsDefaultPrefix_AffectsNewMainViewModel()
    {
        // Change default prefix via settings
        _preferences.Get(PreferenceKeys.DefaultPrefix, PreferenceKeys.DefaultPrefixValue)
            .Returns("hotfix");

        var settingsVm = CreateSettingsVm();
        Assert.Equal("hotfix", settingsVm.DefaultPrefix);

        // New MainViewModel reads the same preferences
        var mainVm = CreateMainVm();
        Assert.Equal("hotfix", mainVm.Prefix);

        mainVm.TicketNumber = "BUG-42";
        Assert.StartsWith("hotfix/", mainVm.GeneratedBranchName);
    }

    // ── 3. Custom Separators Workflow ────────────────────────────────

    [Fact]
    public void CustomSeparators_ChangesBranchNameFormat()
    {
        // Configure custom separators
        _preferences.Get(PreferenceKeys.PrefixTicketSeparator, PreferenceKeys.PrefixTicketSeparatorDefault)
            .Returns("-");
        _preferences.Get(PreferenceKeys.TicketTitleSeparator, PreferenceKeys.TicketTitleSeparatorDefault)
            .Returns("-");

        var vm = CreateMainVm();
        vm.Prefix = "feature";
        vm.TicketNumber = "PROJ-99";
        vm.Title = "Fix Bug";

        // With "-" separators: feature-PROJ-99-fix-bug
        Assert.Equal("feature-PROJ-99-fix-bug", vm.GeneratedBranchName);
    }

    [Fact]
    public void CustomSeparators_DotAndUnderscore()
    {
        _preferences.Get(PreferenceKeys.PrefixTicketSeparator, PreferenceKeys.PrefixTicketSeparatorDefault)
            .Returns(".");
        _preferences.Get(PreferenceKeys.TicketTitleSeparator, PreferenceKeys.TicketTitleSeparatorDefault)
            .Returns(".");

        var vm = CreateMainVm();
        vm.Prefix = "release";
        vm.TicketNumber = "v1.2";
        vm.Title = "Final Release";

        Assert.Equal("release.v1.2.final-release", vm.GeneratedBranchName);
    }

    // ── 4. Title Format Workflow ─────────────────────────────────────

    [Theory]
    [InlineData("kebab-case", "feature/PROJ-1_add-user-login")]
    [InlineData("snake_case", "feature/PROJ-1_add_user_login")]
    [InlineData("PascalCase", "feature/PROJ-1_AddUserLogin")]
    public void TitleFormat_TransformsBranchNameCorrectly(string format, string expected)
    {
        _preferences.Get(PreferenceKeys.TitleFormat, PreferenceKeys.TitleFormatDefault)
            .Returns(format);

        var vm = CreateMainVm();
        vm.Prefix = "feature";
        vm.TicketNumber = "PROJ-1";
        vm.Title = "Add User Login";

        Assert.Equal(expected, vm.GeneratedBranchName);
    }

    // ── 5. Validation Blocks Copy ────────────────────────────────────

    [Fact]
    public void ValidationBlocksCopy_InvalidCharsThenFixed()
    {
        var vm = CreateMainVm();

        // Invalid input
        vm.Prefix = "feature";
        vm.TicketNumber = "PROJ?123";

        Assert.False(vm.IsValid);
        Assert.False(vm.CopyCommand.CanExecute(null));
        Assert.True(vm.HasInvalidCharacters);
        Assert.Contains("'?'", vm.InvalidCharactersDisplay);

        // Fix input
        vm.TicketNumber = "PROJ-123";

        Assert.True(vm.IsValid);
        Assert.True(vm.CopyCommand.CanExecute(null));
        Assert.False(vm.HasInvalidCharacters);
        Assert.Equal(string.Empty, vm.InvalidCharactersDisplay);
    }

    [Fact]
    public void EmptyBranchName_BlocksCopy()
    {
        var vm = CreateMainVm();

        // No ticket or title → empty branch name
        Assert.False(vm.CopyCommand.CanExecute(null));

        // Add ticket → can copy
        vm.TicketNumber = "T-1";
        Assert.True(vm.CopyCommand.CanExecute(null));
    }

    // ── 6. Auto-Close Delay Workflow ─────────────────────────────────

    [Fact]
    public async Task AutoCloseDelay_ZeroMs_StillHidesWindow()
    {
        _preferences.Get(PreferenceKeys.AutoCloseDelayMs, PreferenceKeys.AutoCloseDelayMsDefault)
            .Returns(0);

        var vm = CreateMainVm();
        vm.Prefix = "feature";
        vm.TicketNumber = "T-1";

        await vm.CopyCommand.ExecuteAsync(null);

        _windowService.Received(1).HideWindow();
    }

    [Fact]
    public void SettingsViewModel_DelayOption_PersistsViaPreferences()
    {
        // Start with default 500ms so changing to 0 actually triggers OnChanged
        _preferences.Get(PreferenceKeys.AutoCloseDelayMs, PreferenceKeys.AutoCloseDelayMsDefault)
            .Returns(500);

        var settingsVm = CreateSettingsVm();
        Assert.Equal(500, settingsVm.SelectedDelayOption.Value);

        var instantOption = settingsVm.DelayOptions.First(d => d.Value == 0);
        settingsVm.SelectedDelayOption = instantOption;

        _preferences.Received().Set(PreferenceKeys.AutoCloseDelayMs, 0);
        Assert.Equal("Window closes instantly", settingsVm.AutoCloseDelayLabel);
    }

    [Fact]
    public void SettingsViewModel_DelayOption_NonZero_ShowsMs()
    {
        _preferences.Get(PreferenceKeys.AutoCloseDelayMs, PreferenceKeys.AutoCloseDelayMsDefault)
            .Returns(1000);

        var settingsVm = CreateSettingsVm();

        Assert.Equal(1000, settingsVm.SelectedDelayOption.Value);
        Assert.Contains("1000ms", settingsVm.AutoCloseDelayLabel);
    }

    // ── 7. Settings Persistence Round-Trip ───────────────────────────

    [Fact]
    public void SettingsPersistenceRoundTrip_AllSettings()
    {
        // First session: user changes settings
        var settingsVm1 = CreateSettingsVm();
        settingsVm1.DefaultPrefix = "bugfix";
        settingsVm1.SelectedTitleFormat = "snake_case";
        settingsVm1.PrefixTicketSeparator = "-";
        settingsVm1.TicketTitleSeparator = ".";

        // Verify saves were called
        _preferences.Received(1).Set(PreferenceKeys.DefaultPrefix, "bugfix");
        _preferences.Received(1).Set(PreferenceKeys.TitleFormat, "snake_case");
        _preferences.Received(1).Set(PreferenceKeys.PrefixTicketSeparator, "-");
        _preferences.Received(1).Set(PreferenceKeys.TicketTitleSeparator, ".");

        // Second session: preferences return the saved values
        _preferences.Get(PreferenceKeys.DefaultPrefix, PreferenceKeys.DefaultPrefixValue)
            .Returns("bugfix");
        _preferences.Get(PreferenceKeys.TitleFormat, PreferenceKeys.TitleFormatDefault)
            .Returns("snake_case");
        _preferences.Get(PreferenceKeys.PrefixTicketSeparator, PreferenceKeys.PrefixTicketSeparatorDefault)
            .Returns("-");
        _preferences.Get(PreferenceKeys.TicketTitleSeparator, PreferenceKeys.TicketTitleSeparatorDefault)
            .Returns(".");

        var settingsVm2 = CreateSettingsVm();
        Assert.Equal("bugfix", settingsVm2.DefaultPrefix);
        Assert.Equal("snake_case", settingsVm2.SelectedTitleFormat);
        Assert.Equal("-", settingsVm2.PrefixTicketSeparator);
        Assert.Equal(".", settingsVm2.TicketTitleSeparator);
    }

    [Fact]
    public void SettingsPersistenceRoundTrip_MainViewModelPicksUpSavedSettings()
    {
        // Simulate saved settings from a previous session
        _preferences.Get(PreferenceKeys.DefaultPrefix, PreferenceKeys.DefaultPrefixValue)
            .Returns("bugfix");
        _preferences.Get(PreferenceKeys.TitleFormat, PreferenceKeys.TitleFormatDefault)
            .Returns("snake_case");
        _preferences.Get(PreferenceKeys.PrefixTicketSeparator, PreferenceKeys.PrefixTicketSeparatorDefault)
            .Returns("-");
        _preferences.Get(PreferenceKeys.TicketTitleSeparator, PreferenceKeys.TicketTitleSeparatorDefault)
            .Returns(".");

        var vm = CreateMainVm();
        Assert.Equal("bugfix", vm.Prefix);

        vm.TicketNumber = "PROJ-1";
        vm.Title = "Fix Login";

        // bugfix-PROJ-1.fix_login (prefix sep: "-", ticket sep: ".", format: snake_case)
        Assert.Equal("bugfix-PROJ-1.fix_login", vm.GeneratedBranchName);
    }

    // ── Cross-cutting: Full workflow with custom settings ────────────

    [Fact]
    public async Task FullWorkflow_WithCustomSettings_GeneratesCopiesAndResets()
    {
        // Configure custom settings
        _preferences.Get(PreferenceKeys.DefaultPrefix, PreferenceKeys.DefaultPrefixValue)
            .Returns("hotfix");
        _preferences.Get(PreferenceKeys.TitleFormat, PreferenceKeys.TitleFormatDefault)
            .Returns("PascalCase");
        _preferences.Get(PreferenceKeys.PrefixTicketSeparator, PreferenceKeys.PrefixTicketSeparatorDefault)
            .Returns("/");
        _preferences.Get(PreferenceKeys.TicketTitleSeparator, PreferenceKeys.TicketTitleSeparatorDefault)
            .Returns("_");
        _preferences.Get(PreferenceKeys.AutoCloseDelayMs, PreferenceKeys.AutoCloseDelayMsDefault)
            .Returns(0);

        var vm = CreateMainVm();
        Assert.Equal("hotfix", vm.Prefix);

        vm.TicketNumber = "SEC-999";
        vm.Title = "patch vulnerability";

        var expected = "hotfix/SEC-999_PatchVulnerability";
        Assert.Equal(expected, vm.GeneratedBranchName);
        Assert.True(vm.IsValid);

        await vm.CopyCommand.ExecuteAsync(null);

        await _clipboard.Received(1).CopyToClipboardAsync(expected);
        _windowService.Received(1).HideWindow();

        // Form cleared, prefix preserved
        Assert.Equal(string.Empty, vm.TicketNumber);
        Assert.Equal(string.Empty, vm.Title);
        Assert.Equal("hotfix", vm.Prefix);
        Assert.Equal("Copy", vm.CopyButtonText);
        Assert.False(vm.CopySuccess);
    }
}
