# BeNGy PowerToys-Style Fluent Redesign — Design Spec

**Date:** 2026-08-06
**Status:** Approved by user (direction + approach + design sections)
**Depth chosen:** Full control-template overhaul (user explicitly picked the deepest of three offered options)

## Goal

Make BeNGy look and feel like a first-party PowerToys utility. The main window
becomes a **launcher palette** in the idiom of PowerToys Run / Command Palette;
Settings becomes an in-palette page styled as **PowerToys settings cards**.

## Decisions already made (user-approved)

| Decision | Choice |
|---|---|
| Main window idiom | Launcher palette: borderless, rounded, acrylic, floats upper-center |
| Settings | In-palette page swap (same window), settings-card styling |
| Light-dismiss on focus loss | **No** — palette stays open when focus moves elsewhere (protects half-typed input; Esc/Copy/tray still dismiss) |
| Implementation depth | Full overhaul: tokens + native chrome + control templates + entrance animation |

## 1. Window & chrome (`Platforms/Windows/WindowService.cs`)

- Borderless: `OverlappedPresenter.SetBorderAndTitleBar(false, false)`;
  keep `IsResizable = false`, `IsMaximizable = false`.
- Rounded corners: `DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND)`.
- Backdrop: `DesktopAcrylicController` (Microsoft.UI.Composition.SystemBackdrops)
  with `MicaController` → solid themed fill as the fallback chain when
  composition is unavailable (energy saver, remote session).
- Sizes: main page **560×400**, settings page **560×620**. `WindowService`
  gains a method to switch between the two on navigation (animated if cheap,
  instant is acceptable).
- Position: horizontally centered, top edge at ~25% of work-area height
  (PowerToys Run position), on every Show.
- `Window.Title` stays `"BeNGy"` — invisible without chrome but required by
  UIA: the E2E fixture matches windows by title.
- Entrance on Show: ~120ms fade-in + TranslationY 8→0 + scale 0.98→1.
- MAUI's in-window navigation bar is hidden on both pages
  (`NavigationPage.SetHasNavigationBar(false)`).
- No minimize/maximize/close buttons. Dismiss paths (all already exist):
  Esc → hide + clear; Copy → hide after delay; tray Exit → quit.

## 2. Design tokens (`Resources/Styles/Colors.xaml`, `Styles.xaml` — rewritten)

- **Accent:** Windows system accent color, read at startup (WinUI
  `UISettings.GetColorValue(UIColorType.Accent)` + hover/pressed variants),
  exposed as MAUI resources. Used for: Copy button fill, switch on-state,
  focus accents. No hardcoded brand color.
- **Type:** Segoe UI Variable Text (fallback Segoe UI). Body 14; captions/labels
  12 in secondary text color; page titles 20 semibold. Branch preview:
  **Cascadia Mono**, fallback Consolas, 14.
- **Icons:** Segoe Fluent Icons glyph font. Gear (settings), copy, back chevron,
  warning, check.
- **Surfaces:** cards with 8px corner radius, 1px hairline strokes; inputs 4px
  radius. Light: near-white translucent card fills over acrylic, `#1B1B1B`
  primary / `#5D5D5D` secondary text. Dark: white-alpha card fills
  (~5-8% white), `#FFFFFF` primary / `#C5C5C5` secondary.
- **Control restyling:** via MAUI handler mappings to the underlying WinUI
  controls (TextBox/ComboBox): quiet borders, no accent underline, card-matched
  corner radius, proper hover/pressed/focused visual states.

## 3. Main page (`Views/MainPage.xaml`)

Layout, top to bottom, 20px padding, 12px gaps:

1. Header row: (no visible title) — gear icon button top-right,
   `AutomationId="SettingsButton"` (moves from ToolbarItem to in-content button).
2. Input row: prefix Picker (fixed ~150px) + ticket Entry (fills) side by side.
3. Title Entry, full width.
4. Preview card: mono branch name, tap-to-copy retained,
   `AutomationId="PreviewLabel"` on the text.
5. Status/action row: ✓ or ⚠ glyph + status text left
   (`ValidIndicator`/`InvalidIndicator`/`ValidationStatusLabel`); right-aligned
   accent-filled **Copy** button with copy glyph + "Copy" + subtle `↵` hint
   (`CopyButton`). Status row stays bound to `HasBranchName` (no verdict on an
   empty form).
6. Validation InfoBar (visible only when invalid): one amber tinted card,
   warning glyph, containing the existing message + invalid-character rows.
   **Visually one unit; internally preserves** `ValidationWarningSection`,
   `ValidationMessageLabel`, `InvalidCharsRow`, `InvalidCharsPrefix`,
   `InvalidCharsLabel`, and `CopyBlockedLabel` (blocked-hint text moves inside
   the InfoBar).
7. Content remains inside a ScrollView (DPI/text-scale safety net), as today.

## 4. Settings page (`Views/SettingsPage.xaml`)

- Header: back-chevron icon button (`AutomationId="DoneButton"`, same handler
  as today's Done) + "Settings" title (a Label named "Settings" — an E2E test
  finds the page header by Name).
- Sections, each a card group with 52px rows, glyph + label left, control
  right, hairline separators between rows:
  - **Branch generation**: Default prefix, Title format, Prefix/Ticket
    separator, Ticket/Title separator
  - **Global hotkey**: current combo in a keycap-styled chip + Change button
    (existing capture flow and overlay retained)
  - **Appearance**: Theme
  - **Startup**: Start with Windows, Start minimized to tray
  - **Behavior**: Auto-close delay
- All existing AutomationIds and helper descriptions retained.

## 5. Out of scope

- No new features, no new settings, no behavior changes beyond the dismiss/
  chrome model described above.
- No separate settings window, no light-dismiss, no drag-to-move.
- Tray icon and hotkey plumbing untouched.

## 6. Constraints & invariants

- **Every existing AutomationId survives** (inventory: SettingsButton,
  DoneButton, PrefixPicker, TicketEntry, TitleEntry, PreviewLabel, CopyButton,
  ValidIndicator, InvalidIndicator, ValidationStatusLabel,
  ValidationStatusRow, ValidationWarningSection, ValidationMessageLabel,
  InvalidCharsRow, InvalidCharsPrefix, InvalidCharsLabel, CopyBlockedLabel,
  DefaultPrefixPicker, TitleFormatPicker, PrefixSeparatorPicker,
  TicketSeparatorPicker, ThemePicker, StartWithWindowsSwitch,
  StartMinimizedSwitch, AutoCloseDelayPicker, ChangeHotkeyButton).
- UIA window title remains "BeNGy".
- Unit suite (205) must stay green; ViewModels are not restructured (only
  additive properties if strictly needed).
- LayoutFitTests geometry assertions must pass at the new sizes, in both the
  default and invalid states.

## 7. Verification

1. `dotnet build` clean, unit suite 205/205.
2. Screenshot review: main empty / valid / invalid, settings, light + dark —
   compared against the PowerToys Run / Settings idiom by eye.
3. Full E2E via the sandbox runner (`run-e2e-sandbox.ps1`): baseline is
   **48/65**; the same 17 known order-dependent failures are acceptable, any
   new failure is a regression to fix before merge.
4. Work happens on a branch (`redesign/powertoys-palette`); merge only after
   the user approves the screenshots.

## 8. Risks & fallbacks

- **Acrylic vs MAUI backgrounds:** MAUI paints page/layout backgrounds that can
  occlude the composition backdrop. Mitigation: set page + root layout
  backgrounds transparent on Windows; if transparency propagation fails,
  fall back to Mica (still authentically PowerToys — the Settings app is Mica)
  or a near-opaque tinted fill. Decide by screenshot, not by hope.
- **Handler-level control restyling in MAUI 8 is the fragile part** (the user
  chose this depth knowingly). Contained per-control; any control that fights
  back falls back to token-level styling (colors/radius only) rather than
  blocking the redesign.
- **Window resize on page navigation** may flicker; if it does, resize before
  the page transition rather than animating.
