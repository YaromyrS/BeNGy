using BeNGy.Models;
using BeNGy.Services.Interfaces;
using BeNGy.ViewModels;
using NSubstitute;

namespace BeNGy.Tests;

/// <summary>
/// Tests for SettingsViewModel — default prefix, theme, startup, auto-close delay,
/// hotkey display, and auto-save behavior.
/// </summary>
public class SettingsViewModelTests
{
    private readonly IPreferencesService _preferences;
    private readonly IThemeService _themeService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IStartupService _startupService;

    public SettingsViewModelTests()
    {
        _preferences = Substitute.For<IPreferencesService>();
        _themeService = Substitute.For<IThemeService>();
        _hotkeyService = Substitute.For<IHotkeyService>();
        _startupService = Substitute.For<IStartupService>();

        // Defaults
        _preferences.Get(PreferenceKeys.DefaultPrefix, PreferenceKeys.DefaultPrefixValue)
            .Returns(PreferenceKeys.DefaultPrefixValue);
        _preferences.Get(PreferenceKeys.StartMinimized, PreferenceKeys.StartMinimizedDefault)
            .Returns(PreferenceKeys.StartMinimizedDefault);
        _preferences.Get(PreferenceKeys.AutoCloseDelayMs, PreferenceKeys.AutoCloseDelayMsDefault)
            .Returns(PreferenceKeys.AutoCloseDelayMsDefault);
        _preferences.Get(PreferenceKeys.HotkeyModifiers, PreferenceKeys.HotkeyModifiersDefault)
            .Returns(PreferenceKeys.HotkeyModifiersDefault);
        _preferences.Get(PreferenceKeys.HotkeyKey, PreferenceKeys.HotkeyKeyDefault)
            .Returns(PreferenceKeys.HotkeyKeyDefault);

        _themeService.CurrentTheme.Returns(BeNGyTheme.System);
        _startupService.IsStartWithWindows.Returns(false);
        _hotkeyService.IsRegistered.Returns(true); // Default to successful registration
    }

    private SettingsViewModel CreateVm() =>
        new(_preferences, _themeService, _hotkeyService, _startupService);

    // ── Story 6.2: Default Prefix ─────────────────────────────────

    [Fact]
    public void DefaultPrefix_LoadsFromPreferences()
    {
        _preferences.Get(PreferenceKeys.DefaultPrefix, PreferenceKeys.DefaultPrefixValue)
            .Returns("bugfix");

        var vm = CreateVm();

        Assert.Equal("bugfix", vm.DefaultPrefix);
    }

    [Fact]
    public void DefaultPrefix_FallsBackToFeature()
    {
        var vm = CreateVm();

        Assert.Equal("feature", vm.DefaultPrefix);
    }

    [Fact]
    public void DefaultPrefix_SavesOnChange()
    {
        var vm = CreateVm();

        vm.DefaultPrefix = "hotfix";

        _preferences.Received(1).Set(PreferenceKeys.DefaultPrefix, "hotfix");
    }

    [Fact]
    public void AvailablePrefixes_ContainsExpectedItems()
    {
        var vm = CreateVm();

        var expected = new[] { "feature", "bugfix", "hotfix", "release", "task" };
        Assert.Equal(expected, vm.AvailablePrefixes);
    }

    [Fact]
    public void AddPrefix_AppendsAndPersists()
    {
        var vm = CreateVm();
        vm.NewPrefix = "spike";

        vm.AddPrefixCommand.Execute(null);

        Assert.Contains("spike", vm.AvailablePrefixes);
        Assert.Equal(string.Empty, vm.NewPrefix);
        _preferences.Received().Set(PreferenceKeys.BranchPrefixes,
            "feature,bugfix,hotfix,release,task,spike");
    }

    [Fact]
    public void AddPrefix_IgnoresEmptyAndDuplicates()
    {
        var vm = CreateVm();

        vm.NewPrefix = "   ";
        vm.AddPrefixCommand.Execute(null);
        vm.NewPrefix = "feature";
        vm.AddPrefixCommand.Execute(null);

        Assert.Equal(5, vm.AvailablePrefixes.Count);
        _preferences.DidNotReceive().Set(PreferenceKeys.BranchPrefixes, Arg.Any<string>());
    }

    [Fact]
    public void RemovePrefix_RemovesAndPersists_AndReassignsDefault()
    {
        var vm = CreateVm();
        vm.DefaultPrefix = "task";
        _preferences.ClearReceivedCalls();

        vm.RemovePrefixCommand.Execute("task");

        Assert.DoesNotContain("task", vm.AvailablePrefixes);
        Assert.Equal("feature", vm.DefaultPrefix);
        _preferences.Received().Set(PreferenceKeys.BranchPrefixes,
            "feature,bugfix,hotfix,release");
    }

    // ── Story 6.3: Hotkey Display ─────────────────────────────────

    [Fact]
    public void HotkeyDisplay_ShowsDefaultCtrlShiftB()
    {
        var vm = CreateVm();

        Assert.Equal("Ctrl+Shift+B", vm.HotkeyDisplay);
    }

    [Fact]
    public void HotkeyError_DefaultsToEmpty()
    {
        var vm = CreateVm();

        Assert.Equal(string.Empty, vm.HotkeyError);
    }

    [Fact]
    public void HotkeyError_ShowsErrorIfServiceNotRegistered()
    {
        _hotkeyService.IsRegistered.Returns(false);

        var vm = CreateVm();

        Assert.Contains("unavailable", vm.HotkeyError);
    }

    [Fact]
    public void ChangeHotkeyCommand_StartsCapture_PromptIsNotAnError()
    {
        var vm = CreateVm();

        vm.ChangeHotkeyCommand.Execute(null);

        Assert.True(vm.IsCapturingHotkey);
        Assert.Equal("Cancel", vm.ChangeButtonText);
        Assert.Empty(vm.HotkeyError);   // the view shows the prompt; HotkeyError is red
    }

    [Fact]
    public void Capture_EscapeCancels_WithoutRegistering()
    {
        var vm = CreateVm();
        vm.ChangeHotkeyCommand.Execute(null);

        vm.HandleCaptureKeyDownCommand.Execute((HotkeyModifiers.None, HotkeyKey.Escape));

        Assert.False(vm.IsCapturingHotkey);
        Assert.Equal("Change", vm.ChangeButtonText);
        Assert.Empty(vm.HotkeyError);
        _hotkeyService.DidNotReceive().RegisterHotkey(Arg.Any<HotkeyModifiers>(), Arg.Any<HotkeyKey>());
    }

    [Fact]
    public void Capture_CurrentHotkeyPressed_Cancels()
    {
        // The registered hotkey is swallowed globally and arrives as HotkeyPressed,
        // never as a key-down on the page.
        var vm = CreateVm();
        vm.ChangeHotkeyCommand.Execute(null);

        _hotkeyService.HotkeyPressed += Raise.Event();

        Assert.False(vm.IsCapturingHotkey);
        _hotkeyService.DidNotReceive().RegisterHotkey(Arg.Any<HotkeyModifiers>(), Arg.Any<HotkeyKey>());
    }

    [Fact]
    public void Capture_ListensForHotkeyPressedOnlyWhileCapturing()
    {
        var vm = CreateVm();
        _hotkeyService.DidNotReceive().HotkeyPressed += Arg.Any<EventHandler>();

        vm.ChangeHotkeyCommand.Execute(null);
        _hotkeyService.Received(1).HotkeyPressed += Arg.Any<EventHandler>();

        vm.CancelHotkeyCapture();
        _hotkeyService.Received(1).HotkeyPressed -= Arg.Any<EventHandler>();
    }

    [Fact]
    public void Capture_SuccessfulRegistration_StopsListeningForHotkeyPressed()
    {
        _hotkeyService.RegisterHotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, HotkeyKey.K)
            .Returns(HotkeyRegistrationResult.Success());
        var vm = CreateVm();
        vm.ChangeHotkeyCommand.Execute(null);

        vm.HandleCaptureKeyDownCommand.Execute((HotkeyModifiers.Control | HotkeyModifiers.Alt, HotkeyKey.K));

        Assert.False(vm.IsCapturingHotkey);
        _hotkeyService.Received(1).HotkeyPressed -= Arg.Any<EventHandler>();
    }

    [Fact]
    public void CancelHotkeyCapture_RestoresUnavailableMessage_WhenNotRegistered()
    {
        _hotkeyService.IsRegistered.Returns(false);
        var vm = CreateVm();
        vm.ChangeHotkeyCommand.Execute(null);
        Assert.Empty(vm.HotkeyError);

        vm.CancelHotkeyCapture();

        Assert.Contains("unavailable", vm.HotkeyError);
    }

    [Fact]
    public void CancelHotkeyCapture_WhenNotCapturing_IsANoOp()
    {
        var vm = CreateVm();

        vm.CancelHotkeyCapture();

        Assert.False(vm.IsCapturingHotkey);
        _hotkeyService.DidNotReceive().HotkeyPressed -= Arg.Any<EventHandler>();
    }

    // ── Story 6.4: Theme Selection ────────────────────────────────

    [Fact]
    public void SelectedThemeString_LoadsFromThemeService()
    {
        _themeService.CurrentTheme.Returns(BeNGyTheme.Dark);

        var vm = CreateVm();

        Assert.Equal("Dark", vm.SelectedThemeString);
    }

    [Fact]
    public void SelectedThemeString_DefaultsToSystem()
    {
        var vm = CreateVm();

        Assert.Equal("System", vm.SelectedThemeString);
    }

    [Fact]
    public void SelectedThemeString_Change_CallsThemeService()
    {
        var vm = CreateVm();

        vm.SelectedThemeString = "Dark";

        _themeService.Received(1).SetTheme(BeNGyTheme.Dark);
    }

    [Fact]
    public void AvailableThemes_ContainsLightDarkSystem()
    {
        var vm = CreateVm();

        var expected = new[] { "Light", "Dark", "System" };
        Assert.Equal(expected, vm.AvailableThemes);
    }

    // ── Story 6.5: Start with Windows ─────────────────────────────

    [Fact]
    public void StartWithWindows_LoadsFromStartupService()
    {
        _startupService.IsStartWithWindows.Returns(true);

        var vm = CreateVm();

        Assert.True(vm.StartWithWindows);
    }

    [Fact]
    public void StartWithWindows_DefaultsFalse()
    {
        var vm = CreateVm();

        Assert.False(vm.StartWithWindows);
    }

    [Fact]
    public void StartWithWindows_Change_CallsStartupService()
    {
        var vm = CreateVm();

        vm.StartWithWindows = true;

        _startupService.Received(1).SetStartWithWindows(true);
    }

    [Fact]
    public void StartWithWindows_AppliedByWindows_StaysOn()
    {
        var vm = CreateVm();
        _startupService.When(s => s.SetStartWithWindows(true))
            .Do(_ => _startupService.IsStartWithWindows.Returns(true));

        vm.StartWithWindows = true;

        Assert.True(vm.StartWithWindows);
    }

    [Fact]
    public void StartWithWindows_RefusedByWindows_ShowsTheRealState()
    {
        // Store build: the user disabled BeNGy in Task Manager's startup apps,
        // so enabling it from the app has no effect.
        var vm = CreateVm();   // IsStartWithWindows stays false

        vm.StartWithWindows = true;

        Assert.False(vm.StartWithWindows);
    }

    // ── Story 6.6: Start Minimized ────────────────────────────────

    [Fact]
    public void StartMinimized_LoadsFromPreferences()
    {
        _preferences.Get(PreferenceKeys.StartMinimized, PreferenceKeys.StartMinimizedDefault)
            .Returns(false);

        var vm = CreateVm();

        Assert.False(vm.StartMinimized);
    }

    [Fact]
    public void StartMinimized_DefaultsTrue()
    {
        var vm = CreateVm();

        Assert.True(vm.StartMinimized);
    }

    [Fact]
    public void StartMinimized_Change_SavesPreference()
    {
        var vm = CreateVm();

        vm.StartMinimized = false;

        _preferences.Received(1).Set(PreferenceKeys.StartMinimized, false);
    }

    // ── Story 6.7: Auto-Close Delay ───────────────────────────────

    [Fact]
    public void SelectedDelayOption_DefaultsToFast200()
    {
        // Palette redesign feedback: auto-close should be fast by default.
        var vm = CreateVm();

        Assert.Equal(200, vm.SelectedDelayOption.Value);
    }

    [Fact]
    public void SelectedDelayOption_LoadsFromPreferences()
    {
        _preferences.Get(PreferenceKeys.AutoCloseDelayMs, PreferenceKeys.AutoCloseDelayMsDefault)
            .Returns(200);

        var vm = CreateVm();

        Assert.Equal(200, vm.SelectedDelayOption.Value);
    }

    [Fact]
    public void SelectedDelayOption_Change_SavesPreference()
    {
        var vm = CreateVm();
        var instantOption = vm.DelayOptions.First(d => d.Value == 0);

        vm.SelectedDelayOption = instantOption;

        _preferences.Received(1).Set(PreferenceKeys.AutoCloseDelayMs, 0);
    }

    [Fact]
    public void AutoCloseDelayLabel_ShowsInstant_WhenZero()
    {
        var vm = CreateVm();
        var instantOption = vm.DelayOptions.First(d => d.Value == 0);

        vm.SelectedDelayOption = instantOption;

        Assert.Equal("Window closes instantly", vm.AutoCloseDelayLabel);
    }

    [Fact]
    public void AutoCloseDelayLabel_ShowsMs_WhenNonZero()
    {
        var vm = CreateVm();

        Assert.Contains("200ms", vm.AutoCloseDelayLabel);
    }

    [Fact]
    public void DelayOptions_ContainsFivePresets()
    {
        var vm = CreateVm();

        Assert.Equal(5, vm.DelayOptions.Count);
    }

    // ── MainViewModel Integration ─────────────────────────────────

    [Fact]
    public void MainViewModel_LoadsDefaultPrefix_FromPreferences()
    {
        _preferences.Get(PreferenceKeys.DefaultPrefix, PreferenceKeys.DefaultPrefixValue)
            .Returns("hotfix");

        var generator = new BeNGy.Services.BranchNameGenerator();
        var validator = new BeNGy.Services.GitValidator();
        var clipboard = Substitute.For<IClipboardService>();
        var windowService = Substitute.For<IWindowService>();

        var vm = new MainViewModel(generator, validator, clipboard, windowService, _preferences);

        Assert.Equal("hotfix", vm.Prefix);
    }

    [Fact]
    public void MainViewModel_FallsBackToFeature_WhenNoPreference()
    {
        var generator = new BeNGy.Services.BranchNameGenerator();
        var validator = new BeNGy.Services.GitValidator();
        var clipboard = Substitute.For<IClipboardService>();
        var windowService = Substitute.For<IWindowService>();

        var vm = new MainViewModel(generator, validator, clipboard, windowService);

        Assert.Equal("feature", vm.Prefix);
    }

    [Fact]
    public async Task MainViewModel_CopyCommand_UsesConfigurableDelay()
    {
        // Set a 0ms delay for fast test
        _preferences.Get(PreferenceKeys.AutoCloseDelayMs, PreferenceKeys.AutoCloseDelayMsDefault)
            .Returns(0);

        var generator = new BeNGy.Services.BranchNameGenerator();
        var validator = new BeNGy.Services.GitValidator();
        var clipboard = Substitute.For<IClipboardService>();
        clipboard.CopyToClipboardAsync(Arg.Any<string>()).Returns(true);
        var windowService = Substitute.For<IWindowService>();

        var vm = new MainViewModel(generator, validator, clipboard, windowService, _preferences);
        vm.Prefix = "feature";
        vm.TicketNumber = "T-1";

        await vm.CopyCommand.ExecuteAsync(null);

        // Even with 0ms delay, window should still hide
        windowService.Received(1).HideWindow();
    }

    // ── Formatting Options Verification (H1) ───────────────────────

    [Fact]
    public void TitleFormat_LoadsFromPreferences()
    {
        _preferences.Get(PreferenceKeys.TitleFormat, PreferenceKeys.TitleFormatDefault)
            .Returns("snake_case");

        var vm = CreateVm();

        Assert.Equal("snake_case", vm.SelectedTitleFormat);
    }

    [Fact]
    public void TitleFormat_SavesOnChange()
    {
        var vm = CreateVm();

        vm.SelectedTitleFormat = "PascalCase";

        _preferences.Received(1).Set(PreferenceKeys.TitleFormat, "PascalCase");
    }

    [Fact]
    public void PrefixSeparator_SavesOnChange()
    {
        var vm = CreateVm();

        vm.PrefixTicketSeparator = "-";

        _preferences.Received(1).Set(PreferenceKeys.PrefixTicketSeparator, "-");
    }

    [Fact]
    public void TicketSeparator_SavesOnChange()
    {
        var vm = CreateVm();

        vm.TicketTitleSeparator = ".";

        _preferences.Received(1).Set(PreferenceKeys.TicketTitleSeparator, ".");
    }
}
