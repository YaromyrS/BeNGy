# Changelog

All notable changes to BeNGy will be documented in this file.

## [1.0.0] - 2026-03-15

### Added

- **Project Foundation & App Shell**: Launch from Windows Start Menu, system tray icon with context menu, single-instance enforcement, main window with hide/show pattern, and app lifecycle management.
- **Branch Name Generation Core**: MVVM-based branch generator with prefix dropdown (feature, bugfix, hotfix, release), ticket number input, title/description input, and real-time branch name preview with automatic title transformation (kebab-case, snake_case, PascalCase).
- **Validation & Error Prevention**: Real-time git branch name validation against git naming rules, visual validation status indicators, and specific invalid character feedback with helpful guidance.
- **Clipboard & Completion Flow**: Copy to clipboard via button or Enter key, auto-close after copy, and Escape key to dismiss without copying.
- **Global Hotkey Activation**: Summon BeNGy from any application using Ctrl+Shift+B, hotkey conflict detection with user warnings, and tray menu fallback access.
- **User Settings & Configuration**: Settings panel accessible from main window and tray, configurable default prefix, title format, prefix-ticket and ticket-title separators, auto-close toggle, and persistent settings across sessions.
- **Theme & Visual Polish**: Automatic light/dark theme detection following Windows system preference, real-time theme switching without restart, and PowerToys-inspired Fluent Design styling.
