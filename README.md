# BeNGy - Branch Name Generator

A lightweight Windows desktop utility that helps developers generate consistently formatted Git branch names instantly. Press **Ctrl+Shift+B** from any application, enter your ticket number and description, and get a ready-to-use branch name copied to your clipboard.

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

Publish a self-contained single-file executable:

```
dotnet publish src/BeNGy/BeNGy.csproj -c Release
```

The project is preconfigured (`PublishSingleFile`, `SelfContained`, `IncludeNativeLibrariesForSelfExtract`) so the output is a standalone `.exe` that requires no separate .NET runtime installation. Published output lands in:

```
src/BeNGy/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/
```

To target ARM64 instead:

```
dotnet publish src/BeNGy/BeNGy.csproj -c Release -r win-arm64
```

## Running Tests

Tests use xUnit with NSubstitute for mocking:

```
dotnet test BeNGy.sln
```

Or run tests for the test project directly:

```
dotnet test tests/BeNGy.Tests/BeNGy.Tests.csproj
```

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
