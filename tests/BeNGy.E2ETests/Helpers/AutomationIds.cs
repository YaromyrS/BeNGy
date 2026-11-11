namespace BeNGy.E2ETests.Helpers;

/// <summary>
/// Constants matching the AutomationId values defined in the XAML views.
/// </summary>
public static class AutomationIds
{
    // ── MainPage ────────────────────────────────────────────────
    public const string PrefixPicker = "PrefixPicker";
    public const string TicketEntry = "TicketEntry";
    public const string TitleEntry = "TitleEntry";
    public const string PreviewLabel = "PreviewLabel";
    public const string CopyButton = "CopyButton";
    public const string CloseButton = "CloseButton";
    public const string SettingsButton = "SettingsButton";
    // ValidIndicator / InvalidIndicator / ValidationStatusLabel were removed
    // with the palette redesign: valid input shows no verdict UI at all.
    public const string ValidationMessageLabel = "ValidationMessageLabel";
    public const string ValidationWarningSection = "ValidationWarningSection";
    // InvalidCharsRow / InvalidCharsLabel / InvalidCharsPrefix were removed: they
    // repeated what ValidationMessageLabel already says ("Invalid characters: '?'").

    // ── SettingsPage ────────────────────────────────────────────
    public const string DoneButton = "DoneButton";
    public const string DefaultPrefixPicker = "DefaultPrefixPicker";
    public const string TitleFormatPicker = "TitleFormatPicker";
    public const string PrefixSeparatorPicker = "PrefixSeparatorPicker";
    public const string TicketSeparatorPicker = "TicketSeparatorPicker";
    public const string ThemePicker = "ThemePicker";
    public const string StartWithWindowsSwitch = "StartWithWindowsSwitch";
    public const string StartMinimizedSwitch = "StartMinimizedSwitch";
    public const string AutoCloseDelayPicker = "AutoCloseDelayPicker";
    public const string ChangeHotkeyButton = "ChangeHotkeyButton";
}
