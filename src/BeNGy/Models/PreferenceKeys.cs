namespace BeNGy.Models;

/// <summary>
/// Central registry of all preference keys and their defaults.
/// Used by IPreferencesService consumers to avoid magic strings.
/// </summary>
public static class PreferenceKeys
{
    // ── Branch Generation ─────────────────────────────────────────
    public const string DefaultPrefix = "default_prefix";
    public const string DefaultPrefixValue = "feature";

    public const string TitleFormat = "title_format";
    public const string TitleFormatDefault = "kebab-case";

    public const string PrefixTicketSeparator = "prefix_ticket_separator";
    public const string PrefixTicketSeparatorDefault = "/";

    public const string TicketTitleSeparator = "ticket_title_separator";
    public const string TicketTitleSeparatorDefault = "_";

    // ── Behavior ──────────────────────────────────────────────────
    public const string AutoCloseAfterCopy = "auto_close_after_copy";
    public const bool AutoCloseAfterCopyDefault = true;

    public const string AutoCloseDelayMs = "auto_close_delay_ms";
    // "Fast" — the palette should get out of the way the moment it has done
    // its job. (Was 500/"Normal"; user feedback wanted fast by default.)
    public const int AutoCloseDelayMsDefault = 200;

    // ── Hotkey ────────────────────────────────────────────────────
    public const string HotkeyModifiers = "hotkey_modifiers";
    public const int HotkeyModifiersDefault = 6; // Ctrl(2) + Shift(4)

    public const string HotkeyKey = "hotkey_key";
    public const int HotkeyKeyDefault = 0x42; // 'B'

    // ── Appearance ────────────────────────────────────────────────
    public const string Theme = "theme";
    public const string ThemeDefault = "system"; // "light", "dark", "system"

    // ── Startup ───────────────────────────────────────────────────
    public const string StartWithWindows = "start_with_windows";
    public const bool StartWithWindowsDefault = false;

    public const string StartMinimized = "start_minimized";
    public const bool StartMinimizedDefault = true;
}
