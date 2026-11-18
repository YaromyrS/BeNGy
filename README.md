# BeNGy - Branch Name Generator

A lightweight Windows desktop utility that helps developers generate consistently formatted Git branch names instantly. Press **Ctrl+Shift+B** from any application, enter your ticket number and description, and get a ready-to-use branch name copied to your clipboard.

**Download:** [getbengy.vercel.app](https://getbengy.vercel.app), or grab `BeNGy-Setup-x64.exe`, the MSI, or the portable zip from [the latest release](https://github.com/YaromyrS/BeNGy/releases/latest). It runs on Windows 10 (version 2004 or later) and Windows 11, x64, and needs nothing else installed.

## Features

- **Global Hotkey** -- Summon BeNGy from anywhere with Ctrl+Shift+B
- **Real-time Branch Name Preview** -- See the generated branch name update as you type
- **Git Validation** -- Real-time validation against Git branch naming rules with specific feedback on invalid characters
- **Customizable Formats** -- Configure branch prefixes (feature, bugfix, hotfix, release), title formats (kebab-case, snake_case, PascalCase), and separators
- **One-Click Copy** -- Branch name copied to clipboard via button or Enter key, with optional auto-close
- **System Tray** -- Runs quietly in the system tray with a context menu for quick access
- **Single Instance** -- Enforces only one running instance; re-activation signals the existing window
- **Theme Support** -- Follows Windows light/dark theme preference automatically, with real-time switching
- **Persistent Settings** -- User preferences saved across sessions

## Prerequisites

- **Windows 10** (build 19041 or later) or **Windows 11**
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) with the MAUI workload installed

To install the MAUI workload:

```
dotnet workload install maui
```

## Building

Clone the repository and restore dependencies:

```
dotnet restore BeNGy.sln
```

Build in Debug mode:

```
dotnet build BeNGy.sln
```

Build in Release mode:

```
dotnet build BeNGy.sln -c Release
```

## Running

Run the application directly:

```
dotnet run --project src/BeNGy/BeNGy.csproj
```

Or, after building, run the compiled executable from `src/BeNGy/bin/Debug/net8.0-windows10.0.19041.0/win-x64/`.

## Publishing

Release builds come from one script:

```
powershell -File tools\packaging\build-release.ps1        # Setup.exe, MSI, portable zip
powershell -File tools\packaging\build-release.ps1 -Msix  # ...plus the Microsoft Store package
```

It publishes a self-contained, ReadyToRun folder build of the app and wraps it
into `BeNGy-Setup-x64.exe` (WiX bundle), `BeNGy-x64.msi` (per-user, installs to
`%LocalAppData%\Programs\BeNGy`, no admin prompt) and `BeNGy-x64-portable.zip`,
all under `artifacts\<version>\`. The installers use the WiX Toolset 5 MSBuild
SDK (`installer\`), which NuGet restores. Nothing needs to be installed first.

The Store build is the same app packaged as MSIX. The release process and the
Microsoft Store checklist are in [docs/store-release.md](docs/store-release.md).

Publishing as a single-file exe is not supported: it crashed at start
(`DllNotFoundException` in `Program.Main`), and MSIX can't use it either.

## Running Tests

### Unit tests

Fast, headless, safe to run anywhere. xUnit with NSubstitute for mocking:

```
dotnet test tests/BeNGy.Tests/BeNGy.Tests.csproj
```

### UI end-to-end tests

**Recommended: run them in Windows Sandbox** so they cannot steal focus,
keystrokes, or your clipboard while you work:

```
powershell -File tests/BeNGy.E2ETests/run-e2e-sandbox.ps1
```

This launches a disposable VM (Windows Pro/Enterprise feature) with the repo and
the host's .NET install mapped in read-only, clipboard redirection disabled, and
networking off. Minimize the sandbox window and keep working; results are copied
back to `tests/BeNGy.E2ETests/TestResults/sandbox.trx`. The sandbox produces the
same pass/fail set as a host run, and the suite's settings mutations (see Known
gaps) stay in the VM's throwaway profile instead of your real settings.

To run directly on your own desktop instead:

```
dotnet test tests/BeNGy.E2ETests/BeNGy.E2ETests.csproj
```

**These are not headless.** FlaUI (UIA3) drives the real desktop: it opens real
windows, synthesizes real keystrokes including the global <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>B</kbd>
hotkey, and reads the real system clipboard. While a run is in progress:

- focus will be stolen periodically,
- **your clipboard will be overwritten**,
- typing can corrupt a test, since your keystrokes interleave with synthesized ones.

They cannot run on a locked workstation or in a container, and they currently
write to the real user preference store (see Known Gaps below). Build the app
first, or set `BENGY_EXE_PATH` to point at an existing `BeNGy.exe`.

Runs are guarded against hangs by `tests/BeNGy.E2ETests/e2e.runsettings`, applied
automatically via `RunSettingsFilePath`. A single test that stops making progress
for 120s aborts the run and writes a hang dump to `TestResults/`; the whole
session is capped at 900s. For reference, the slowest legitimate test is ~25s and
a full run takes ~4 minutes.

### Known gaps in the E2E suite

The suite shares one app instance across all test classes and has no state
isolation, so some tests fail depending on execution order:

- Epic 6 changes the default prefix and Epic 7 changes the theme; both **persist
  to your real settings** after the run and break later assertions.
- Tests inherit whatever page the previous test left open.

Fixing this needs a test-scoped preference store plus a per-test reset to the
main page. Until then, expect ~17 order-dependent failures and check your
Settings afterwards.

## Project Structure

```
BeNGy.sln                          Solution file
global.json                        Pins .NET SDK 8.0
src/
  BeNGy/
    BeNGy.csproj                   Main app - .NET MAUI (Windows)
    MauiProgram.cs                 App bootstrap, DI registration
    App.xaml.cs                    Application lifecycle
    Views/
      MainPage.xaml(.cs)           Branch name generator UI
      SettingsPage.xaml(.cs)       User settings UI
    ViewModels/
      BaseViewModel.cs             Shared ViewModel base
      MainViewModel.cs             Branch generation logic + UI state
      SettingsViewModel.cs         Settings management
    Models/
      BranchInput.cs               Input model for branch generation
      ValidationResult.cs          Git validation result
      PreferenceKeys.cs            Constants for preference storage keys
      DelayOption.cs               Delay option model
    Services/
      BranchNameGenerator.cs       Builds branch names from structured input
      GitValidator.cs              Validates branch names against Git rules
      ClipboardService.cs          Clipboard access
      PreferencesService.cs        Persistent user preferences
      ThemeService.cs              Windows theme detection
      Interfaces/                  Service abstractions (ITrayService, etc.)
    Helpers/
      StringTransformers.cs        kebab-case, snake_case, PascalCase transforms
      SingleInstanceManager.cs     Mutex-based single instance enforcement
      HotkeyFormattingHelper.cs    Hotkey display formatting
      NativeMethods.cs             P/Invoke declarations
      *Converter.cs                XAML value converters
    Platforms/
      Windows/
        TrayService.cs             System tray icon (H.NotifyIcon.WinUI)
        WindowService.cs           Window show/hide management
        HotkeyService.cs           Global hotkey registration
        StartupService.cs          Windows startup integration
        App.xaml.cs                WinUI app entry point
    Resources/                     Icons, splash screen, fonts, images
tests/
  BeNGy.E2ETests/
    BeNGy.E2ETests.csproj          UI end-to-end tests (FlaUI / UIA3)
    e2e.runsettings                Hang timebomb + session cap (auto-applied)
    Helpers/
      AppTestFixture.cs            Launch/attach, window lookup, clipboard, input
      AutomationIds.cs             Constants mirroring the XAML AutomationIds
    Tests/
      Epic{1-7}_*.cs               Functional coverage per epic
      HiddenWindowRecoveryTests.cs Harness regression: hidden window must not hang
      LayoutFitTests.cs            Geometry: controls must fit the fixed window
  BeNGy.Tests/
    BeNGy.Tests.csproj             xUnit + NSubstitute
    BranchNameGeneratorTests.cs    Branch generation unit tests
    GitValidatorTests.cs           Git validation unit tests
    MainViewModelTests.cs          ViewModel unit tests
    SettingsViewModelTests.cs      Settings VM tests
    StringTransformersTests.cs     String transform tests
    SingleInstanceManagerTests.cs  Single instance tests
    WorkflowIntegrationTests.cs    End-to-end workflow tests
```

## Key Dependencies

| Package | Purpose |
|---|---|
| Microsoft.Maui.Controls 8.0 | UI framework |
| CommunityToolkit.Mvvm | MVVM source generators and base classes |
| CommunityToolkit.Maui | MAUI community extensions |
| H.NotifyIcon.WinUI | System tray icon support |
| xUnit | Test framework |
| NSubstitute | Mocking library |

## License

MIT -- see [LICENSE](LICENSE) for details.
