# Changelog

All notable changes to BeNGy will be documented in this file.

## [1.1.0] - 2026-11-11

### Added

- **Ctrl+C copies the branch name**: It keeps BeNGy open and the form filled, unlike Enter or the Copy button. With text selected in a field, Ctrl+C copies that selection as usual.
- **Installers**: `BeNGy-Setup-x64.exe` and `BeNGy-x64.msi` install per user to `%LocalAppData%\Programs\BeNGy` without an admin prompt. A portable zip is also available.
- **Microsoft Store packaging**: An MSIX build where "Start with Windows" uses a Windows startup task. Turning BeNGy off in Task Manager's startup apps is respected.

### Changed

- **PowerToys-style palette window**: The main window is now a borderless, rounded launcher palette with an acrylic backdrop (Mica, then a solid fill, when acrylic is unavailable). It opens horizontally centered at a quarter of the screen height, like PowerToys Run, with a short fade-and-rise entrance. Drag it by its header strip to move it.
- **Recording a new summon hotkey**: Settings no longer greys out. The hotkey card is outlined while recording, and Esc or the current hotkey cancels.
- **Fluent design tokens**: Colors, type, and icons follow Fluent: the Windows system accent color, Segoe UI Variable, Segoe Fluent Icons, and Cascadia Mono for the branch preview. Inputs and dropdowns are restyled at the WinUI level.
- **Main page layout**: Prefix and ticket share a row, the branch name sits in a preview card with a copy button, and validation problems appear as a one-line warning bar. The palette grows and shrinks to fit its content.
- **Settings as cards**: Settings opens inside the same palette and is laid out as PowerToys-style settings cards.

### Fixed

- Branch names without a ticket now use the prefix separator after the prefix (`feature/add-user-login`, not `feature_add-user-login`), so git tools group them under the prefix folder.
- The palette is sized in device-independent pixels, so it fits on displays scaled above 100%.
- The ticket field gets keyboard focus every time BeNGy is summoned.
- The Settings page no longer has an invisible back button that took keyboard focus, showed a stray "Back" tooltip, and added a hidden Tab stop.
- Screen readers announce the branch preview by the branch name itself.
- Switching between the form and Settings is immediate. It used to fade through an empty or partly black palette for up to half a second.
- Dragging the palette works again after the Windows updates of November 2026.

## [1.0.0] - 2026-03-15

### Added

- **Project Foundation & App Shell**: Launch from Windows Start Menu, system tray icon with context menu, single-instance enforcement, main window with hide/show pattern, and app lifecycle management.
- **Branch Name Generation Core**: MVVM-based branch generator with prefix dropdown (feature, bugfix, hotfix, release), ticket number input, title/description input, and real-time branch name preview with automatic title transformation (kebab-case, snake_case, PascalCase).
- **Validation & Error Prevention**: Real-time git branch name validation against git naming rules, visual validation status indicators, and specific invalid character feedback with helpful guidance.
- **Clipboard & Completion Flow**: Copy to clipboard via button or Enter key, auto-close after copy, and Escape key to dismiss without copying.
- **Global Hotkey Activation**: Summon BeNGy from any application using Ctrl+Shift+B, hotkey conflict detection with user warnings, and tray menu fallback access.
- **User Settings & Configuration**: Settings panel accessible from main window and tray, configurable default prefix, title format, prefix-ticket and ticket-title separators, auto-close toggle, and persistent settings across sessions.
- **Theme & Visual Polish**: Automatic light/dark theme detection following Windows system preference, real-time theme switching without restart, and PowerToys-inspired Fluent Design styling.
