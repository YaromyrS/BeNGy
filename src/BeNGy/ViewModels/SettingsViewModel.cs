using BeNGy.Helpers;
using BeNGy.Models;
using BeNGy.Services.Interfaces;

namespace BeNGy.ViewModels;

/// <summary>
/// ViewModel for the Settings page. All settings auto-save on change.
/// Covers Stories 6.2 (prefix), 6.3 (hotkey), 6.4 (theme), 6.5 (startup),
/// 6.6 (start minimized), 6.7 (auto-close delay).
/// </summary>
public partial class SettingsViewModel : BaseViewModel
{
    private readonly IPreferencesService _preferences;
    private readonly IHotkeyService? _hotkeyService;
    private readonly IThemeService _themeService;
    private readonly IStartupService? _startupService;

    public SettingsViewModel(
        IPreferencesService preferences,
        IThemeService themeService,
        IHotkeyService? hotkeyService = null,
        IStartupService? startupService = null)
    {
        _preferences = preferences;
        _themeService = themeService;
        _hotkeyService = hotkeyService;
        _startupService = startupService;

        // Load saved values
        AvailablePrefixes = new ObservableCollection<string>(Services.PrefixCatalog.Load(_preferences));
        _defaultPrefix = _preferences.Get(PreferenceKeys.DefaultPrefix, PreferenceKeys.DefaultPrefixValue);
        _selectedThemeString = _themeService.CurrentTheme.ToString();
        _startWithWindows = _startupService?.IsStartWithWindows ?? false;
        _startMinimized = _preferences.Get(PreferenceKeys.StartMinimized, PreferenceKeys.StartMinimizedDefault);

        var savedDelay = _preferences.Get(PreferenceKeys.AutoCloseDelayMs, PreferenceKeys.AutoCloseDelayMsDefault);
        _selectedDelayOption = DelayOptions.FirstOrDefault(d => d.Value == savedDelay) ?? DelayOptions[2]; // "Normal"

        // Hotkey display
        var mods = (HotkeyModifiers)_preferences.Get(PreferenceKeys.HotkeyModifiers, PreferenceKeys.HotkeyModifiersDefault);
        var key = (HotkeyKey)_preferences.Get(PreferenceKeys.HotkeyKey, PreferenceKeys.HotkeyKeyDefault);
        _hotkeyDisplay = BeNGy.Helpers.HotkeyFormattingHelper.FormatHotkey(mods, key);

        // Story M2: If hotkey failed during startup, show error in VM immediately
        if (_hotkeyService != null && !_hotkeyService.IsRegistered)
        {
            HotkeyError = "Global hotkey is currently unavailable. It may be in use by another application.";
        }

        // Formatting
        _selectedTitleFormat = _preferences.Get(PreferenceKeys.TitleFormat, PreferenceKeys.TitleFormatDefault);
        _prefixTicketSeparator = _preferences.Get(PreferenceKeys.PrefixTicketSeparator, PreferenceKeys.PrefixTicketSeparatorDefault);
        _ticketTitleSeparator = _preferences.Get(PreferenceKeys.TicketTitleSeparator, PreferenceKeys.TicketTitleSeparatorDefault);

        UpdateDelayLabel();
    }

    // ── Story 6.2: Default Prefix + user-managed prefix catalog ───

    public ObservableCollection<string> AvailablePrefixes { get; }

    [ObservableProperty]
    private string _defaultPrefix;

    partial void OnDefaultPrefixChanged(string value)
    {
        _preferences.Set(PreferenceKeys.DefaultPrefix, value);
    }

    [ObservableProperty]
    private string _newPrefix = string.Empty;

    /// <summary>Prefix manager is collapsed by default; a chevron row expands it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrefixChevron))]
    private bool _isPrefixListExpanded;

    /// <summary>Chevron glyph for the prefixes row: down = collapsed, up = expanded.</summary>
    public string PrefixChevron => IsPrefixListExpanded ? "" : "";

    [RelayCommand]
    private void TogglePrefixList() => IsPrefixListExpanded = !IsPrefixListExpanded;

    /// <summary>
    /// Adds the typed prefix to the catalog. Rejects empties and duplicates;
    /// git-rule validation happens naturally in the branch preview.
    /// </summary>
    [RelayCommand]
    private void AddPrefix()
    {
        var candidate = NewPrefix.Trim();
        if (candidate.Length == 0 || AvailablePrefixes.Contains(candidate))
            return;

        AvailablePrefixes.Add(candidate);
        Services.PrefixCatalog.Save(_preferences, AvailablePrefixes);
        NewPrefix = string.Empty;
    }

    /// <summary>
    /// Removes a prefix from the catalog. The last remaining prefix cannot be
    /// removed; removing the current default reassigns it to the first entry.
    /// </summary>
    [RelayCommand]
    private void RemovePrefix(string prefix)
    {
        if (AvailablePrefixes.Count <= 1) return;
        if (!AvailablePrefixes.Remove(prefix)) return;

        Services.PrefixCatalog.Save(_preferences, AvailablePrefixes);

        if (DefaultPrefix == prefix)
            DefaultPrefix = AvailablePrefixes[0];
    }

    // ── Story 6.3: Hotkey Configuration ───────────────────────────

    [ObservableProperty]
    private string _hotkeyDisplay;

    [ObservableProperty]
    private string _hotkeyError = string.Empty;

    [ObservableProperty]
    private bool _isCapturingHotkey;

    [ObservableProperty]
    private string _changeButtonText = "Change";

    /// <summary>
    /// Toggles the hotkey capture mode.
    /// </summary>
    [RelayCommand]
    private void ChangeHotkey()
    {
        if (IsCapturingHotkey)
        {
            // Cancel capture
            IsCapturingHotkey = false;
            ChangeButtonText = "Change";
            HotkeyError = string.Empty;
        }
        else
        {
            // Start capture
            IsCapturingHotkey = true;
            ChangeButtonText = "Cancel";
            HotkeyError = "Press the new hotkey combination...";
        }
    }

    /// <summary>
    /// Processes a key down event from the view during capture mode.
    /// </summary>
    [RelayCommand]
    private void HandleCaptureKeyDown(object? e)
    {
        if (!IsCapturingHotkey || e is not (HotkeyModifiers mods, HotkeyKey key))
            return;

        // Validation: Must have at least one modifier
        if (mods == HotkeyModifiers.None)
        {
            HotkeyError = "Hotkey must include at least one modifier (Ctrl, Alt, Shift).";
            return;
        }

        // Attempt to register new hotkey
        var result = _hotkeyService?.RegisterHotkey(mods, key);

        if (result?.IsSuccess == true)
        {
            // Save to preferences
            _preferences.Set(PreferenceKeys.HotkeyModifiers, (int)mods);
            _preferences.Set(PreferenceKeys.HotkeyKey, (int)key);
            
            // Update UI
            HotkeyDisplay = BeNGy.Helpers.HotkeyFormattingHelper.FormatHotkey(mods, key);
            IsCapturingHotkey = false;
            ChangeButtonText = "Change";
            HotkeyError = string.Empty;
        }
        else
        {
            HotkeyError = result?.ErrorMessage ?? "Failed to register hotkey.";
        }
    }

    // ── Story 6.4: Theme Selection ────────────────────────────────

    public ObservableCollection<string> AvailableThemes { get; } = new()
    {
        "Light", "Dark", "System"
    };

    [ObservableProperty]
    private string _selectedThemeString;

    partial void OnSelectedThemeStringChanged(string value)
    {
        if (Enum.TryParse<BeNGyTheme>(value, true, out var theme))
        {
            _themeService.SetTheme(theme);
        }
    }

    // ── Formatting Options (FR19, FR20, FR21) ──────────────────────

    public ObservableCollection<string> AvailableTitleFormats { get; } = new()
    {
        "kebab-case", "snake_case", "PascalCase"
    };

    public ObservableCollection<string> AvailableSeparators { get; } = new()
    {
        "-", "_", "/", "."
    };

    [ObservableProperty]
    private string _selectedTitleFormat;

    [ObservableProperty]
    private string _prefixTicketSeparator;

    [ObservableProperty]
    private string _ticketTitleSeparator;

    partial void OnSelectedTitleFormatChanged(string value) => _preferences.Set(PreferenceKeys.TitleFormat, value);
    partial void OnPrefixTicketSeparatorChanged(string value) => _preferences.Set(PreferenceKeys.PrefixTicketSeparator, value);
    partial void OnTicketTitleSeparatorChanged(string value) => _preferences.Set(PreferenceKeys.TicketTitleSeparator, value);

    // ── Story 6.5: Start with Windows ─────────────────────────────

    [ObservableProperty]
    private bool _startWithWindows;

    partial void OnStartWithWindowsChanged(bool value)
    {
        _startupService?.SetStartWithWindows(value);
    }

    // ── Story 6.6: Start Minimized ────────────────────────────────

    [ObservableProperty]
    private bool _startMinimized;

    partial void OnStartMinimizedChanged(bool value)
    {
        _preferences.Set(PreferenceKeys.StartMinimized, value);
    }

    // ── Story 6.7: Auto-Close Delay ───────────────────────────────

    public ObservableCollection<DelayOption> DelayOptions { get; } = new()
    {
        new DelayOption(0, "Instant (0ms)"),
        new DelayOption(200, "Fast (200ms)"),
        new DelayOption(500, "Normal (500ms)"),
        new DelayOption(1000, "Slow (1000ms)"),
        new DelayOption(1500, "Very Slow (1500ms)")
    };

    [ObservableProperty]
    private DelayOption _selectedDelayOption;

    [ObservableProperty]
    private string _autoCloseDelayLabel = string.Empty;

    partial void OnSelectedDelayOptionChanged(DelayOption value)
    {
        _preferences.Set(PreferenceKeys.AutoCloseDelayMs, value.Value);
        UpdateDelayLabel();
    }

    private void UpdateDelayLabel()
    {
        AutoCloseDelayLabel = SelectedDelayOption.Value == 0
            ? "Window closes instantly"
            : $"Window closes after {SelectedDelayOption.Value}ms";
    }
}
