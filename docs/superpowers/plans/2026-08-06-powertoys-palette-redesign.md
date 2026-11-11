# PowerToys Palette Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Restyle BeNGy as a PowerToys-style launcher palette (borderless rounded acrylic window, Fluent tokens, settings cards) without changing behavior or breaking the E2E suite.

**Architecture:** Pure presentation-layer overhaul: rewrite the two resource dictionaries into a Fluent token set, rewrite both pages' XAML, and do window chrome (borderless/rounded/backdrop/position) natively in `Platforms/Windows/WindowService.cs`. ViewModels are untouched except that `IWindowService` gains one method. Spec: `docs/superpowers/specs/2026-08-06-powertoys-fluent-redesign-design.md`.

**Tech Stack:** .NET MAUI 8 (Windows/WinUI 3), WinAppSDK `SystemBackdrop`, DWM corner preference, Segoe UI Variable / Segoe Fluent Icons / Cascadia Mono (all ship with Windows 11), xUnit + FlaUI for verification.

## Global Constraints

- **Every existing AutomationId survives verbatim** (full inventory in spec §6). E2E tests find elements by these IDs.
- MAUI `Window.Title` stays `"BeNGy"` (E2E fixture matches window by title).
- SettingsPage must keep a `Label` whose `Text="Settings"` (an E2E test finds the header by Name).
- Unit suite must stay 205/205 after every task that touches `src/`.
- Window sizes: main **560×400**, settings **560×620** physical px (same convention as today's 440×640).
- Use `DynamicResource` (not `StaticResource`) wherever a style consumes `Accent*` resources — they are replaced at runtime with the Windows accent color.
- Do not run the E2E suite on the host desktop; use `tests/BeNGy.E2ETests/run-e2e-sandbox.ps1` (user requirement). Brief app-launch screenshots on the host are acceptable.
- All work on branch `redesign/powertoys-palette`; do not push to `main`.
- E2E baseline to hold: **48/65 passed**, with exactly these 17 known order-dependent failures (any OTHER failure is a regression):
  Epic1 `TrayIcon_ContextMenu_HasExpectedMenuItems`, Epic1 `TrayIcon_ExitClicked_TerminatesProcess`,
  Epic2 `Preview_AllFieldsFilled_ShowsFullBranchFormat`, Epic2 `Preview_TicketEntered_UpdatesInRealTime`, Epic2 `TicketEntry_OnWindowOpen_HasFocus`, Epic2 `TicketEntry_TypeText_AcceptsInput`, Epic2 `TitleEntry_TypeText_AcceptsInput`, Epic2 `Preview_TitleEntered_ShowsKebabCasedTitle`, Epic2 `PrefixPicker_SelectDifferentPrefix_SelectionChanges`, Epic2 `PrefixPicker_Loaded_ContainsExpectedPrefixes`, Epic2 `PrefixPicker_Default_IsFeature`, Epic2 `Preview_PrefixChanged_UpdatesImmediately`,
  Epic4 `CopyButton_Clicked_CopiesBranchNameToClipboard`, Epic4 `Copy_Successful_FormIsCleared`, Epic4 `EnterKey_InTitleField_TriggersCopy`,
  Epic7 `ThemeChange_UIReflectsChange_WindowRemainsUsable`, Epic7 `ThemeChange_ToDark_AppDoesNotCrash`.

---

### Task 1: Branch + committed screenshot helper

**Files:**
- Create: `tools/dev/capture-bengy.ps1`

**Interfaces:**
- Produces: `tools/dev/capture-bengy.ps1 -Out <png>` — relaunches the Debug BeNGy.exe, summons it (handles start-minimized), screenshots the window. Every later task's visual check uses this.

- [x] **Step 1: Create the branch**

```bash
git -C D:/projects/bengy checkout -b redesign/powertoys-palette
```

- [x] **Step 2: Write the helper**

Create `tools/dev/capture-bengy.ps1`:

```powershell
param(
    [string]$Exe = (Join-Path (git rev-parse --show-toplevel) "src\BeNGy\bin\Debug\net8.0-windows10.0.19041.0\win-x64\BeNGy.exe"),
    [Parameter(Mandatory = $true)][string]$Out
)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class K {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int c);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    public const byte VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_B = 0x42;
    public const uint KEYUP = 0x0002;
}
"@
[void][K]::SetProcessDPIAware()
Get-Process BeNGy -ErrorAction SilentlyContinue | ForEach-Object { $_.Kill(); $_.WaitForExit(3000) }
Start-Sleep -Milliseconds 1500
Start-Process -FilePath $Exe -WorkingDirectory (Split-Path $Exe)
Start-Sleep -Seconds 5
function Get-Bengy { Get-Process BeNGy -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1 }
$p = Get-Bengy
if (-not $p) {
    # Start-minimized default: summon via the global hotkey.
    [K]::keybd_event([K]::VK_CONTROL,0,0,[UIntPtr]::Zero); [K]::keybd_event([K]::VK_SHIFT,0,0,[UIntPtr]::Zero); [K]::keybd_event([K]::VK_B,0,0,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [K]::keybd_event([K]::VK_B,0,[K]::KEYUP,[UIntPtr]::Zero); [K]::keybd_event([K]::VK_SHIFT,0,[K]::KEYUP,[UIntPtr]::Zero); [K]::keybd_event([K]::VK_CONTROL,0,[K]::KEYUP,[UIntPtr]::Zero)
    Start-Sleep -Seconds 3
    $p = Get-Bengy
}
if (-not $p) { Write-Error "No visible BeNGy window"; exit 1 }
[void][K]::SetForegroundWindow($p.MainWindowHandle)
Start-Sleep -Milliseconds 900
$r = New-Object K+RECT
if ([K]::DwmGetWindowAttribute($p.MainWindowHandle, 9, [ref]$r, [System.Runtime.InteropServices.Marshal]::SizeOf($r)) -ne 0) { [void][K]::GetWindowRect($p.MainWindowHandle, [ref]$r) }
$w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$g.Dispose(); $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
"captured ${w}x${h} -> $Out"
```

- [x] **Step 3: Verify it works against the current build**

Run: `powershell -ExecutionPolicy Bypass -File tools/dev/capture-bengy.ps1 -Out $env:TEMP\bengy-baseline.png`
Expected: `captured ...` and a PNG showing the current (pre-redesign) window. Keep this PNG as the before shot.

- [x] **Step 4: Commit**

```bash
git add tools/dev/capture-bengy.ps1
git commit -m "chore: add dev screenshot helper for design iteration"
```

---

### Task 2: Fluent color tokens (Colors.xaml)

**Files:**
- Modify: `src/BeNGy/Resources/Styles/Colors.xaml` (append tokens; KEEP all existing keys — legacy keys stay until both pages are rewritten)

**Interfaces:**
- Produces resource keys consumed by Tasks 3–8: `Accent`, `AccentHover`, `AccentPressed`, `AccentForeground`, `TextPrimary`, `TextPrimaryDark`, `TextSecondary`, `TextSecondaryDark`, `CardBackground`, `CardBackgroundDark`, `CardStroke`, `CardStrokeDark`, `InputBackground`, `InputBackgroundDark`, `WarningBackground`, `WarningBackgroundDark`, `WarningText`, `WarningTextDark`, `SuccessText`, `SuccessTextDark`, `KeycapFill`, `KeycapFillDark`, `PageFallback`, `PageFallbackDark`.

- [x] **Step 1: Append the Fluent token block**

Add before the closing `</ResourceDictionary>` of `Colors.xaml`:

```xml
	<!-- ── Fluent tokens (PowerToys palette redesign) ─────────────────
	     Accent* are FALLBACKS: SystemAccent.Apply() replaces them at
	     startup with the Windows accent color. Consume via DynamicResource. -->
	<Color x:Key="Accent">#0067C0</Color>
	<Color x:Key="AccentHover">#1975C5</Color>
	<Color x:Key="AccentPressed">#005FB8</Color>
	<Color x:Key="AccentForeground">#FFFFFF</Color>

	<Color x:Key="TextPrimary">#1B1B1B</Color>
	<Color x:Key="TextPrimaryDark">#FFFFFF</Color>
	<Color x:Key="TextSecondary">#5D5D5D</Color>
	<Color x:Key="TextSecondaryDark">#C5C5C5</Color>

	<Color x:Key="CardBackground">#B3FFFFFF</Color>
	<Color x:Key="CardBackgroundDark">#0DFFFFFF</Color>
	<Color x:Key="CardStroke">#0F000000</Color>
	<Color x:Key="CardStrokeDark">#14FFFFFF</Color>
	<Color x:Key="InputBackground">#CCFFFFFF</Color>
	<Color x:Key="InputBackgroundDark">#14FFFFFF</Color>

	<Color x:Key="WarningBackground">#FFF4CE</Color>
	<Color x:Key="WarningBackgroundDark">#433519</Color>
	<Color x:Key="WarningText">#9D5D00</Color>
	<Color x:Key="WarningTextDark">#FCE100</Color>
	<Color x:Key="SuccessText">#0F7B0F</Color>
	<Color x:Key="SuccessTextDark">#6CCB5F</Color>

	<Color x:Key="KeycapFill">#F3F4F6</Color>
	<Color x:Key="KeycapFillDark">#374151</Color>

	<!-- Used when the composition backdrop is unavailable -->
	<Color x:Key="PageFallback">#F3F3F3</Color>
	<Color x:Key="PageFallbackDark">#202020</Color>
```

- [x] **Step 2: Build + unit tests**

Run: `dotnet build D:/projects/bengy/BeNGy.sln -v minimal` then `dotnet test D:/projects/bengy/tests/BeNGy.Tests/BeNGy.Tests.csproj --no-build -v minimal`
Expected: 0 errors; 205/205.

- [x] **Step 3: Commit**

```bash
git add src/BeNGy/Resources/Styles/Colors.xaml
git commit -m "feat(design): add Fluent color tokens with system-accent fallbacks"
```

---

### Task 3: System accent color (Windows)

**Files:**
- Create: `src/BeNGy/Platforms/Windows/SystemAccent.cs`
- Modify: `src/BeNGy/App.xaml.cs` (constructor, after `InitializeComponent()`)

**Interfaces:**
- Produces: `static void SystemAccent.Apply(ResourceDictionary resources)` — overwrites `Accent`, `AccentHover`, `AccentPressed` with the user's Windows accent color. Silent no-op on failure (XAML fallbacks remain).

- [x] **Step 1: Write SystemAccent.cs**

```csharp
using Windows.UI.ViewManagement;

namespace BeNGy.Platforms.Windows;

/// <summary>
/// Reads the user's Windows accent color and injects it into the app's
/// resource dictionary. PowerToys never ships a brand color; neither do we.
/// Styles consume these via DynamicResource, so replacement propagates.
/// </summary>
public static class SystemAccent
{
    public static void Apply(ResourceDictionary resources)
    {
        try
        {
            var ui = new UISettings();
            var accent = ui.GetColorValue(UIColorType.Accent);
            var hover = ui.GetColorValue(UIColorType.AccentLight1);
            var pressed = ui.GetColorValue(UIColorType.AccentDark1);

            resources["Accent"] = ToMaui(accent);
            resources["AccentHover"] = ToMaui(hover);
            resources["AccentPressed"] = ToMaui(pressed);
        }
        catch
        {
            // Keep the XAML fallback values; a missing accent is cosmetic.
        }
    }

    private static Color ToMaui(global::Windows.UI.Color c) =>
        Color.FromRgba(c.R, c.G, c.B, c.A);
}
```

- [x] **Step 2: Wire it in App.xaml.cs**

In the `App` constructor, immediately after `InitializeComponent();`:

```csharp
#if WINDOWS
		BeNGy.Platforms.Windows.SystemAccent.Apply(Resources);
#endif
```

- [x] **Step 3: Build + unit tests**

Run: `dotnet build D:/projects/bengy/BeNGy.sln -v minimal` then `dotnet test D:/projects/bengy/tests/BeNGy.Tests/BeNGy.Tests.csproj --no-build -v minimal`
Expected: 0 errors; 205/205 (unit project doesn't compile Platforms/Windows; the `#if WINDOWS` guard keeps App portable).

- [x] **Step 4: Commit**

```bash
git add src/BeNGy/Platforms/Windows/SystemAccent.cs src/BeNGy/App.xaml.cs
git commit -m "feat(design): use Windows system accent color"
```

---

### Task 4: Styles.xaml overhaul

**Files:**
- Modify: `src/BeNGy/Resources/Styles/Styles.xaml` — replace the template's `<Style>` blocks for Label, Entry, Picker, Switch, Button, Border, and Page; append the named styles below. Leave all other template styles (ActivityIndicator, etc.) untouched.

**Interfaces:**
- Produces named styles consumed by Tasks 6–7: `PaletteTitle`, `Caption`, `SectionHeader`, `GlyphButton`, `AccentButton`, `PreviewCard`, `WarningInfoBar`, `SettingsCard`, `SettingsRowSeparator`, `KeycapChip`, `MonoText`.
- Fonts used directly by name (no registration needed — system fonts): `Segoe UI Variable Text`, `Segoe Fluent Icons`, `Cascadia Mono`.

- [x] **Step 1: Replace the implicit control styles**

Replace the existing `<Style TargetType="Label">`, `Entry`, `Picker`, `Switch`, `Button`, `Border`, and `ApplyToDerivedTypes Page` styles with:

```xml
	<Style TargetType="Label">
		<Setter Property="FontFamily" Value="Segoe UI Variable Text" />
		<Setter Property="FontSize" Value="14" />
		<Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextPrimary}, Dark={StaticResource TextPrimaryDark}}" />
	</Style>

	<Style TargetType="Entry">
		<Setter Property="FontFamily" Value="Segoe UI Variable Text" />
		<Setter Property="FontSize" Value="14" />
		<Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextPrimary}, Dark={StaticResource TextPrimaryDark}}" />
		<Setter Property="PlaceholderColor" Value="{AppThemeBinding Light={StaticResource TextSecondary}, Dark={StaticResource TextSecondaryDark}}" />
		<Setter Property="BackgroundColor" Value="{AppThemeBinding Light={StaticResource InputBackground}, Dark={StaticResource InputBackgroundDark}}" />
		<Setter Property="MinimumHeightRequest" Value="36" />
	</Style>

	<Style TargetType="Picker">
		<Setter Property="FontFamily" Value="Segoe UI Variable Text" />
		<Setter Property="FontSize" Value="14" />
		<Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextPrimary}, Dark={StaticResource TextPrimaryDark}}" />
		<Setter Property="BackgroundColor" Value="{AppThemeBinding Light={StaticResource InputBackground}, Dark={StaticResource InputBackgroundDark}}" />
		<Setter Property="MinimumHeightRequest" Value="36" />
	</Style>

	<Style TargetType="Switch">
		<Setter Property="OnColor" Value="{DynamicResource Accent}" />
	</Style>

	<Style TargetType="Button">
		<Setter Property="FontFamily" Value="Segoe UI Variable Text" />
		<Setter Property="FontSize" Value="14" />
		<Setter Property="CornerRadius" Value="4" />
		<Setter Property="Padding" Value="12,6" />
		<Setter Property="BackgroundColor" Value="{AppThemeBinding Light={StaticResource CardBackground}, Dark={StaticResource CardBackgroundDark}}" />
		<Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextPrimary}, Dark={StaticResource TextPrimaryDark}}" />
	</Style>

	<Style TargetType="Border">
		<Setter Property="StrokeThickness" Value="1" />
		<Setter Property="Stroke" Value="{AppThemeBinding Light={StaticResource CardStroke}, Dark={StaticResource CardStrokeDark}}" />
	</Style>

	<Style ApplyToDerivedTypes="True" TargetType="Page">
		<Setter Property="Padding" Value="0" />
		<Setter Property="BackgroundColor" Value="Transparent" />
	</Style>
```

- [x] **Step 2: Append the named styles**

```xml
	<!-- ── PowerToys palette styles ─────────────────────────────────── -->
	<Style x:Key="PaletteTitle" TargetType="Label">
		<Setter Property="FontSize" Value="14" />
		<Setter Property="FontAttributes" Value="Bold" />
		<Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextSecondary}, Dark={StaticResource TextSecondaryDark}}" />
	</Style>

	<Style x:Key="Caption" TargetType="Label">
		<Setter Property="FontSize" Value="12" />
		<Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextSecondary}, Dark={StaticResource TextSecondaryDark}}" />
	</Style>

	<Style x:Key="SectionHeader" TargetType="Label">
		<Setter Property="FontSize" Value="12" />
		<Setter Property="FontAttributes" Value="Bold" />
		<Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextSecondary}, Dark={StaticResource TextSecondaryDark}}" />
		<Setter Property="Margin" Value="4,12,0,4" />
	</Style>

	<Style x:Key="MonoText" TargetType="Label">
		<Setter Property="FontFamily" Value="Cascadia Mono" />
		<Setter Property="FontSize" Value="14" />
	</Style>

	<!-- Icon-only button (gear, back chevron). Text = Segoe Fluent Icons glyph. -->
	<Style x:Key="GlyphButton" TargetType="Button">
		<Setter Property="FontFamily" Value="Segoe Fluent Icons" />
		<Setter Property="FontSize" Value="16" />
		<Setter Property="BackgroundColor" Value="Transparent" />
		<Setter Property="TextColor" Value="{AppThemeBinding Light={StaticResource TextPrimary}, Dark={StaticResource TextPrimaryDark}}" />
		<Setter Property="WidthRequest" Value="36" />
		<Setter Property="HeightRequest" Value="36" />
		<Setter Property="Padding" Value="0" />
		<Setter Property="CornerRadius" Value="4" />
	</Style>

	<Style x:Key="AccentButton" TargetType="Button">
		<Setter Property="BackgroundColor" Value="{DynamicResource Accent}" />
		<Setter Property="TextColor" Value="{StaticResource AccentForeground}" />
		<Setter Property="FontAttributes" Value="Bold" />
		<Setter Property="HeightRequest" Value="36" />
		<Setter Property="MinimumWidthRequest" Value="120" />
		<Setter Property="VisualStateManager.VisualStateGroups">
			<VisualStateGroupList>
				<VisualStateGroup x:Name="CommonStates">
					<VisualState x:Name="Normal">
						<VisualState.Setters>
							<Setter Property="BackgroundColor" Value="{DynamicResource Accent}" />
						</VisualState.Setters>
					</VisualState>
					<VisualState x:Name="PointerOver">
						<VisualState.Setters>
							<Setter Property="BackgroundColor" Value="{DynamicResource AccentHover}" />
						</VisualState.Setters>
					</VisualState>
					<VisualState x:Name="Pressed">
						<VisualState.Setters>
							<Setter Property="BackgroundColor" Value="{DynamicResource AccentPressed}" />
						</VisualState.Setters>
					</VisualState>
					<VisualState x:Name="Disabled">
						<VisualState.Setters>
							<Setter Property="BackgroundColor" Value="{AppThemeBinding Light={StaticResource Gray300}, Dark={StaticResource Gray600}}" />
						</VisualState.Setters>
					</VisualState>
				</VisualStateGroup>
			</VisualStateGroupList>
		</Setter>
	</Style>

	<Style x:Key="PreviewCard" TargetType="Border">
		<Setter Property="BackgroundColor" Value="{AppThemeBinding Light={StaticResource CardBackground}, Dark={StaticResource CardBackgroundDark}}" />
		<Setter Property="StrokeShape" Value="RoundRectangle 8" />
		<Setter Property="Padding" Value="12,10" />
	</Style>

	<Style x:Key="WarningInfoBar" TargetType="Border">
		<Setter Property="BackgroundColor" Value="{AppThemeBinding Light={StaticResource WarningBackground}, Dark={StaticResource WarningBackgroundDark}}" />
		<Setter Property="Stroke" Value="Transparent" />
		<Setter Property="StrokeShape" Value="RoundRectangle 8" />
		<Setter Property="Padding" Value="12,10" />
	</Style>

	<Style x:Key="SettingsCard" TargetType="Border">
		<Setter Property="BackgroundColor" Value="{AppThemeBinding Light={StaticResource CardBackground}, Dark={StaticResource CardBackgroundDark}}" />
		<Setter Property="StrokeShape" Value="RoundRectangle 8" />
		<Setter Property="Padding" Value="12,4" />
	</Style>

	<Style x:Key="SettingsRowSeparator" TargetType="BoxView">
		<Setter Property="HeightRequest" Value="1" />
		<Setter Property="Color" Value="{AppThemeBinding Light={StaticResource CardStroke}, Dark={StaticResource CardStrokeDark}}" />
	</Style>

	<Style x:Key="KeycapChip" TargetType="Border">
		<Setter Property="BackgroundColor" Value="{AppThemeBinding Light={StaticResource KeycapFill}, Dark={StaticResource KeycapFillDark}}" />
		<Setter Property="StrokeShape" Value="RoundRectangle 4" />
		<Setter Property="Padding" Value="8,4" />
	</Style>
```

- [x] **Step 3: Build + unit tests + screenshot sanity**

Run: `dotnet build D:/projects/bengy/BeNGy.sln -v minimal`, unit tests as before, then
`powershell -ExecutionPolicy Bypass -File tools/dev/capture-bengy.ps1 -Out $env:TEMP\bengy-task4.png`
Expected: builds clean, 205/205, app launches; typography/colors changed, layout still the old one (pages come later). A transparent-looking or oddly dark page background at this stage is EXPECTED (backdrop arrives in Task 5).

- [x] **Step 4: Commit**

```bash
git add src/BeNGy/Resources/Styles/Styles.xaml
git commit -m "feat(design): Fluent implicit styles and palette component styles"
```

---

### Task 5: Window chrome — borderless, rounded, acrylic, positioned

**Files:**
- Modify: `src/BeNGy/Platforms/Windows/WindowService.cs`
- Modify: `src/BeNGy/Services/Interfaces/IWindowService.cs`
- Modify: `src/BeNGy/App.xaml.cs` (`CreateWindow` — hook `Popped`; `OpenSettings` — resize)

**Interfaces:**
- Produces: `IWindowService.SetPaletteSize(bool settings)` — resizes to 560×400 (main) / 560×620 (settings) and repositions. NSubstitute mocks in unit tests auto-stub the new member; no test edits needed.
- Consumes: nothing new.

- [x] **Step 1: Add the interface member**

In `IWindowService.cs` add:

```csharp
    /// <summary>
    /// Switches the palette between its two fixed sizes: the compact main
    /// form and the taller settings page. Also recenters (PowerToys Run
    /// position: horizontally centered, top at ~25% of the work area).
    /// </summary>
    void SetPaletteSize(bool settings);
```

- [x] **Step 2: Rework WindowService.Initialize + sizing**

In `WindowService.cs`: replace the constants and `Initialize` body; add `SetPaletteSize` and `PositionPalette`; make `ShowWindow` call `PositionPalette()` before activating. Keep `OnWindowClosing`, `HideWindow`, `IsVisible` logic as-is.

```csharp
    private const int PaletteWidth = 560;
    private const int MainHeight = 400;
    private const int SettingsHeight = 620;

    // DWM window corner preference (Win11): 33 = DWMWA_WINDOW_CORNER_PREFERENCE, 2 = DWMWCP_ROUND
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [global::System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public void Initialize(Microsoft.UI.Xaml.Window nativeWindow)
    {
        _nativeWindow = nativeWindow;
        var hwnd = WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // Palette chrome: no border, no title bar, no resize/minimize/maximize.
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        // Win11 rounded corners on a borderless window need an explicit opt-in.
        var corner = DWMWCP_ROUND;
        _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        // Acrylic like PowerToys Run; Mica fallback; null (solid PageFallback
        // color from tokens) when composition is unavailable.
        nativeWindow.SystemBackdrop =
            Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported()
                ? new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop()
                : Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported()
                    ? new Microsoft.UI.Xaml.Media.MicaBackdrop()
                    : null;

        SetPaletteSize(settings: false);
        _appWindow.Closing += OnWindowClosing;
    }

    public void SetPaletteSize(bool settings)
    {
        if (_appWindow is null) return;
        _appWindow.Resize(new SizeInt32(PaletteWidth, settings ? SettingsHeight : MainHeight));
        PositionPalette();
    }

    /// <summary>PowerToys Run position: centered horizontally, top at 25% of the work area.</summary>
    private void PositionPalette()
    {
        if (_appWindow is null) return;
        var area = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var x = area.X + (area.Width - _appWindow.Size.Width) / 2;
        var y = area.Y + area.Height / 4;
        _appWindow.Move(new PointInt32(x, y));
    }
```

Delete the old `WindowWidth`/`WindowHeight` constants, the old `Resize` call, and the old center-both-axes math (`WindowService.cs:80-81` area); `PositionPalette` replaces them.

- [x] **Step 3: Wire resize into navigation (App.xaml.cs)**

In `CreateWindow`, after constructing the NavigationPage:

```csharp
		var nav = new NavigationPage(mainPage);
		nav.Popped += (_, _) => _windowService?.SetPaletteSize(settings: false);
		_mainWindow = new Window(nav) { Title = "BeNGy" };
```

In `OpenSettings()`, before `PushAsync`:

```csharp
				_windowService?.SetPaletteSize(settings: true);
```

- [x] **Step 4: Build, unit tests, screenshot**

Build + 205/205 as before, then capture `$env:TEMP\bengy-task5.png`.
Expected: borderless rounded floating window at upper-center, 560×400-ish. **Inspect the screenshot for the acrylic risk (spec §8):** if the content area is opaque white/black instead of showing backdrop material, note it and continue — Task 6 sets page backgrounds transparent; re-check then. If after Task 6 it is STILL opaque, fall back per spec: set page `BackgroundColor` to `{AppThemeBinding Light={StaticResource PageFallback}, Dark={StaticResource PageFallbackDark}}` and keep Mica/solid instead of acrylic.

- [x] **Step 5: Commit**

```bash
git add src/BeNGy/Platforms/Windows/WindowService.cs src/BeNGy/Services/Interfaces/IWindowService.cs src/BeNGy/App.xaml.cs
git commit -m "feat(design): borderless rounded acrylic palette window, Run-style positioning"
```

---

### Task 6: MainPage palette layout

**Files:**
- Modify: `src/BeNGy/Views/MainPage.xaml` (full replacement of content below the ContentPage tag)
- Modify: `src/BeNGy/Views/MainPage.xaml.cs` (`OnAppearing` entrance animation only; keep Escape handler, focus logic, `OnSettingsClicked`, Completed wiring)

**Interfaces:**
- Consumes: styles from Task 4, tokens from Task 2. Bindings unchanged: `PrefixOptions`, `Prefix`, `TicketNumber`, `Title`, `GeneratedBranchName`, `HasBranchName`, `IsValid`, `ValidationMessage`, `HasInvalidCharacters`, `InvalidCharactersDisplay`, `CopyButtonText`, `CopyCommand`, `CopySuccess` (no longer bound to button color — AccentButton VSM owns color now).

- [x] **Step 1: Replace MainPage.xaml**

Everything between `<ContentPage ...>` and `</ContentPage>` becomes (attributes on the root: add `BackgroundColor="Transparent"` and `NavigationPage.HasNavigationBar="False"`; DELETE the `<ContentPage.ToolbarItems>` block — the gear button replaces it):

```xml
	<ScrollView>
		<VerticalStackLayout Spacing="12" Padding="20,14,20,20">

			<!-- Header: quiet wordmark left, gear right (replaces the ToolbarItem) -->
			<Grid ColumnDefinitions="*,Auto">
				<Label Text="BeNGy" Style="{StaticResource PaletteTitle}" VerticalOptions="Center" />
				<Button Grid.Column="1"
					AutomationId="SettingsButton"
					Style="{StaticResource GlyphButton}"
					Text="&#xE713;"
					Clicked="OnSettingsClicked"
					SemanticProperties.Description="Settings" />
			</Grid>

			<!-- Input row: prefix + ticket side by side -->
			<Grid ColumnDefinitions="150,*" ColumnSpacing="12">
				<Picker
					x:Name="PrefixPicker"
					AutomationId="PrefixPicker"
					SemanticProperties.Description="Branch prefix"
					ItemsSource="{Binding PrefixOptions}"
					SelectedItem="{Binding Prefix}" />
				<Entry Grid.Column="1"
					x:Name="TicketEntry"
					AutomationId="TicketEntry"
					SemanticProperties.Description="Ticket number"
					Text="{Binding TicketNumber}"
					Placeholder="Ticket / issue — PROJ-1234"
					Keyboard="Plain"
					ReturnType="Next" />
			</Grid>

			<Entry
				x:Name="TitleEntry"
				AutomationId="TitleEntry"
				SemanticProperties.Description="Branch title"
				Text="{Binding Title}"
				Placeholder="Title — Add OAuth login flow"
				Keyboard="Plain"
				ReturnType="Done"
				ReturnCommand="{Binding CopyCommand}" />

			<!-- Preview card -->
			<Border Style="{StaticResource PreviewCard}">
				<Label
					x:Name="PreviewLabel"
					AutomationId="PreviewLabel"
					SemanticProperties.Description="Generated branch name preview"
					Style="{StaticResource MonoText}"
					Text="{Binding GeneratedBranchName}"
					TextColor="{AppThemeBinding Light={StaticResource TextPrimary}, Dark={StaticResource TextPrimaryDark}}"
					LineBreakMode="TailTruncation">
					<Label.GestureRecognizers>
						<TapGestureRecognizer Command="{Binding CopyCommand}" />
					</Label.GestureRecognizers>
				</Label>
			</Border>

			<!-- Status left, primary action right -->
			<Grid ColumnDefinitions="*,Auto" ColumnSpacing="12">
				<HorizontalStackLayout
					AutomationId="ValidationStatusRow"
					IsVisible="{Binding HasBranchName}"
					Spacing="6"
					VerticalOptions="Center">
					<Label
						AutomationId="ValidIndicator"
						SemanticProperties.Description="Valid branch name"
						Text="&#xE73E;"
						FontFamily="Segoe Fluent Icons"
						FontSize="14"
						TextColor="{AppThemeBinding Light={StaticResource SuccessText}, Dark={StaticResource SuccessTextDark}}"
						IsVisible="{Binding IsValid}"
						VerticalOptions="Center" />
					<Label
						AutomationId="InvalidIndicator"
						SemanticProperties.Description="Invalid branch name"
						Text="&#xE7BA;"
						FontFamily="Segoe Fluent Icons"
						FontSize="14"
						TextColor="{AppThemeBinding Light={StaticResource WarningText}, Dark={StaticResource WarningTextDark}}"
						IsVisible="{Binding IsValid, Converter={toolkit:InvertedBoolConverter}}"
						VerticalOptions="Center" />
					<Label
						AutomationId="ValidationStatusLabel"
						SemanticProperties.Description="Validation status"
						Style="{StaticResource Caption}"
						Text="{Binding IsValid, Converter={StaticResource ValidationStatusConverter}}"
						TextColor="{Binding IsValid, Converter={StaticResource ValidationColorConverter}}"
						VerticalOptions="Center" />
				</HorizontalStackLayout>

				<Button Grid.Column="1"
					x:Name="CopyButton"
					AutomationId="CopyButton"
					SemanticProperties.Description="Copy branch name to clipboard"
					Style="{StaticResource AccentButton}"
					Text="{Binding CopyButtonText}"
					Command="{Binding CopyCommand}" />
			</Grid>

			<!-- One amber InfoBar replaces the loose warning labels; every
			     AutomationId inside survives for the E2E assertions. -->
			<Border
				Style="{StaticResource WarningInfoBar}"
				AutomationId="ValidationWarningSection"
				IsVisible="{Binding IsValid, Converter={toolkit:InvertedBoolConverter}}">
				<Grid ColumnDefinitions="Auto,*" ColumnSpacing="10">
					<Label Text="&#xE7BA;"
						FontFamily="Segoe Fluent Icons"
						FontSize="16"
						TextColor="{AppThemeBinding Light={StaticResource WarningText}, Dark={StaticResource WarningTextDark}}"
						VerticalOptions="Start" />
					<VerticalStackLayout Grid.Column="1" Spacing="2">
						<Label
							AutomationId="ValidationMessageLabel"
							Style="{StaticResource Caption}"
							TextColor="{AppThemeBinding Light={StaticResource WarningText}, Dark={StaticResource WarningTextDark}}"
							Text="{Binding ValidationMessage}"
							IsVisible="{Binding ValidationMessage, Converter={StaticResource StringNotEmptyConverter}}"
							LineBreakMode="WordWrap" />
						<HorizontalStackLayout AutomationId="InvalidCharsRow" IsVisible="{Binding HasInvalidCharacters}" Spacing="4">
							<Label
								AutomationId="InvalidCharsPrefix"
								Style="{StaticResource Caption}"
								TextColor="{AppThemeBinding Light={StaticResource WarningText}, Dark={StaticResource WarningTextDark}}"
								Text="Characters not allowed:"
								VerticalOptions="Center" />
							<Label
								AutomationId="InvalidCharsLabel"
								Style="{StaticResource MonoText}"
								FontSize="12"
								FontAttributes="Bold"
								TextColor="{AppThemeBinding Light={StaticResource WarningText}, Dark={StaticResource WarningTextDark}}"
								Text="{Binding InvalidCharactersDisplay}"
								VerticalOptions="Center" />
						</HorizontalStackLayout>
						<Label
							AutomationId="CopyBlockedLabel"
							Style="{StaticResource Caption}"
							TextColor="{AppThemeBinding Light={StaticResource WarningText}, Dark={StaticResource WarningTextDark}}"
							Text="Fix validation issues above to copy" />
					</VerticalStackLayout>
				</Grid>
			</Border>

		</VerticalStackLayout>
	</ScrollView>
```

Note: the old `Text="Prefix"` / `Text="Ticket / Issue #"` / `Text="Title / Description"` field labels are deliberately gone — palette idiom uses placeholders. `CopySuccess`/`CopySuccessColorConverter` binding on the button is dropped (AccentButton VSM owns background); the converter class stays (unit-tested), just unused by this page. Spec deviation, accepted: the "subtle ↵ hint" on the Copy button is omitted — `CopyButtonText` is dynamic ("Copy" → "Copied! ✓"), and a composite glyph+text button would break that binding; `ReturnType="Done"` on the title entry keeps Enter-to-copy discoverable.

- [x] **Step 2: Entrance animation in MainPage.xaml.cs**

Replace the fade block at `OnAppearing` (currently `this.Opacity = 0; this.FadeTo(1, 250, ...)`) with:

```csharp
		// Palette entrance: fade + rise + settle, PowerToys Run-style.
		this.Opacity = 0;
		this.TranslationY = 8;
		this.Scale = 0.98;
		_ = Task.WhenAll(
			this.FadeTo(1, 120, Easing.CubicOut),
			this.TranslateTo(0, 0, 120, Easing.CubicOut),
			this.ScaleTo(1, 120, Easing.CubicOut));
```

Everything else in the code-behind stays (Escape handler, `TicketEntry.Focus()`, `TicketEntry.Completed`).

- [x] **Step 3: Build, unit tests, screenshots (empty + valid + invalid)**

Build + 205/205. Capture `$env:TEMP\bengy-task6-empty.png`. Then manually drive (or reuse the session's ux-tour pattern): type ticket `PROJ-1234` + title, capture valid; add `?`, capture invalid.
Expected: palette layout per spec §3; acrylic visible behind content now that the page is transparent (if still opaque → apply the spec §8 fallback and note it).

- [x] **Step 4: Commit**

```bash
git add src/BeNGy/Views/MainPage.xaml src/BeNGy/Views/MainPage.xaml.cs
git commit -m "feat(design): palette-style main page layout"
```

---

### Task 7: SettingsPage cards layout

**Files:**
- Modify: `src/BeNGy/Views/SettingsPage.xaml` (full replacement of content; root gains `BackgroundColor="Transparent"` and `NavigationPage.HasNavigationBar="False"`, DELETE the ToolbarItems block)
- Verify (no expected change): `src/BeNGy/Views/SettingsPage.xaml.cs` — `OnDoneClicked` already pops; window resize on pop is handled by the `nav.Popped` hook from Task 5.

**Interfaces:**
- Consumes: `SettingsCard`, `SettingsRowSeparator`, `SectionHeader`, `GlyphButton`, `KeycapChip`, `Caption` styles; all existing `SettingsViewModel` bindings unchanged (`AvailablePrefixes`, `DefaultPrefix`, `AvailableTitleFormats`, `SelectedTitleFormat`, `AvailableSeparators`, `PrefixTicketSeparator`, `TicketTitleSeparator`, `HotkeyDisplay`, `ChangeButtonText`, `ChangeHotkeyCommand`, `HotkeyError`, `IsCapturingHotkey`, `AvailableThemes`, `SelectedThemeString`, `StartWithWindows`, `StartMinimized`, `DelayOptions`, `SelectedDelayOption`, `AutoCloseDelayLabel`).

- [x] **Step 1: Replace SettingsPage.xaml content**

```xml
	<Grid>
		<!-- Capture overlay retained from the original page (M1 fix) -->
		<BoxView IsVisible="{Binding IsCapturingHotkey}"
			BackgroundColor="{AppThemeBinding Light={StaticResource Gray200}, Dark={StaticResource Gray900}}"
			Opacity="0.5"
			InputTransparent="False"
			ZIndex="10">
			<BoxView.GestureRecognizers>
				<TapGestureRecognizer Command="{Binding ChangeHotkeyCommand}" />
			</BoxView.GestureRecognizers>
		</BoxView>

		<ScrollView Padding="20,14,20,20">
			<VerticalStackLayout Spacing="4">

				<!-- Header: back chevron (same handler as Done) + title -->
				<Grid ColumnDefinitions="Auto,*" ColumnSpacing="8" Margin="0,0,0,8">
					<Button
						AutomationId="DoneButton"
						Style="{StaticResource GlyphButton}"
						Text="&#xE72B;"
						Clicked="OnDoneClicked"
						SemanticProperties.Description="Done" />
					<Label Grid.Column="1" Text="Settings" FontSize="20" FontAttributes="Bold" VerticalOptions="Center" />
				</Grid>

				<VerticalStackLayout Spacing="4" IsEnabled="{Binding IsCapturingHotkey, Converter={StaticResource InvertedBoolConverter}}">
					<Label Text="BRANCH GENERATION" Style="{StaticResource SectionHeader}" />
					<Border Style="{StaticResource SettingsCard}">
						<VerticalStackLayout>
							<Grid ColumnDefinitions="Auto,*,180" ColumnSpacing="10" HeightRequest="52">
								<Label Text="&#xE8AC;" FontFamily="Segoe Fluent Icons" VerticalOptions="Center" />
								<Label Grid.Column="1" Text="Default prefix" VerticalOptions="Center" />
								<Picker Grid.Column="2" AutomationId="DefaultPrefixPicker"
									SemanticProperties.Description="Default prefix"
									ItemsSource="{Binding AvailablePrefixes}" SelectedItem="{Binding DefaultPrefix}"
									VerticalOptions="Center" />
							</Grid>
							<BoxView Style="{StaticResource SettingsRowSeparator}" />
							<Grid ColumnDefinitions="Auto,*,180" ColumnSpacing="10" HeightRequest="52">
								<Label Text="&#xE8D2;" FontFamily="Segoe Fluent Icons" VerticalOptions="Center" />
								<Label Grid.Column="1" Text="Title format" VerticalOptions="Center" />
								<Picker Grid.Column="2" AutomationId="TitleFormatPicker"
									SemanticProperties.Description="Title format"
									ItemsSource="{Binding AvailableTitleFormats}" SelectedItem="{Binding SelectedTitleFormat}"
									VerticalOptions="Center" />
							</Grid>
							<BoxView Style="{StaticResource SettingsRowSeparator}" />
							<Grid ColumnDefinitions="Auto,*,180" ColumnSpacing="10" HeightRequest="52">
								<Label Text="&#xE8A4;" FontFamily="Segoe Fluent Icons" VerticalOptions="Center" />
								<Label Grid.Column="1" Text="Prefix / ticket separator" VerticalOptions="Center" />
								<Picker Grid.Column="2" AutomationId="PrefixSeparatorPicker"
									SemanticProperties.Description="Prefix ticket separator"
									ItemsSource="{Binding AvailableSeparators}" SelectedItem="{Binding PrefixTicketSeparator}"
									VerticalOptions="Center" />
							</Grid>
							<BoxView Style="{StaticResource SettingsRowSeparator}" />
							<Grid ColumnDefinitions="Auto,*,180" ColumnSpacing="10" HeightRequest="52">
								<Label Text="&#xE8A4;" FontFamily="Segoe Fluent Icons" VerticalOptions="Center" />
								<Label Grid.Column="1" Text="Ticket / title separator" VerticalOptions="Center" />
								<Picker Grid.Column="2" AutomationId="TicketSeparatorPicker"
									SemanticProperties.Description="Ticket title separator"
									ItemsSource="{Binding AvailableSeparators}" SelectedItem="{Binding TicketTitleSeparator}"
									VerticalOptions="Center" />
							</Grid>
						</VerticalStackLayout>
					</Border>
				</VerticalStackLayout>

				<Label Text="GLOBAL HOTKEY" Style="{StaticResource SectionHeader}" />
				<Border Style="{StaticResource SettingsCard}">
					<VerticalStackLayout>
						<Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="10" HeightRequest="52">
							<Label Text="&#xE765;" FontFamily="Segoe Fluent Icons" VerticalOptions="Center" />
							<Label Grid.Column="1" Text="Summon hotkey" VerticalOptions="Center" />
							<HorizontalStackLayout Grid.Column="2" Spacing="8" VerticalOptions="Center">
								<Border Style="{StaticResource KeycapChip}">
									<Label Text="{Binding HotkeyDisplay}" Style="{StaticResource MonoText}" FontSize="12" />
								</Border>
								<Button Text="{Binding ChangeButtonText}"
									AutomationId="ChangeHotkeyButton"
									SemanticProperties.Description="Change hotkey"
									Command="{Binding ChangeHotkeyCommand}"
									FontSize="12" />
							</HorizontalStackLayout>
						</Grid>
						<Label Text="{Binding HotkeyError}"
							Style="{StaticResource Caption}"
							TextColor="#EF4444"
							IsVisible="{Binding HotkeyError, Converter={StaticResource StringNotEmptyConverter}}"
							Margin="0,0,0,8" />
					</VerticalStackLayout>
				</Border>

				<VerticalStackLayout Spacing="4" IsEnabled="{Binding IsCapturingHotkey, Converter={StaticResource InvertedBoolConverter}}">
					<Label Text="APPEARANCE" Style="{StaticResource SectionHeader}" />
					<Border Style="{StaticResource SettingsCard}">
						<Grid ColumnDefinitions="Auto,*,180" ColumnSpacing="10" HeightRequest="52">
							<Label Text="&#xE790;" FontFamily="Segoe Fluent Icons" VerticalOptions="Center" />
							<VerticalStackLayout Grid.Column="1" VerticalOptions="Center" Spacing="0">
								<Label Text="Theme" />
								<Label Text="'System' follows your Windows theme" Style="{StaticResource Caption}" FontSize="11" />
							</VerticalStackLayout>
							<Picker Grid.Column="2" AutomationId="ThemePicker"
								SemanticProperties.Description="Theme"
								ItemsSource="{Binding AvailableThemes}" SelectedItem="{Binding SelectedThemeString}"
								VerticalOptions="Center" />
						</Grid>
					</Border>

					<Label Text="STARTUP" Style="{StaticResource SectionHeader}" />
					<Border Style="{StaticResource SettingsCard}">
						<VerticalStackLayout>
							<Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="10" HeightRequest="52">
								<Label Text="&#xE7E8;" FontFamily="Segoe Fluent Icons" VerticalOptions="Center" />
								<VerticalStackLayout Grid.Column="1" VerticalOptions="Center" Spacing="0">
									<Label Text="Start with Windows" />
									<Label Text="Launch BeNGy when Windows starts" Style="{StaticResource Caption}" FontSize="11" />
								</VerticalStackLayout>
								<Switch Grid.Column="2" AutomationId="StartWithWindowsSwitch"
									SemanticProperties.Description="Start with Windows"
									IsToggled="{Binding StartWithWindows}" VerticalOptions="Center" />
							</Grid>
							<BoxView Style="{StaticResource SettingsRowSeparator}" />
							<Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="10" HeightRequest="52">
								<Label Text="&#xE8A0;" FontFamily="Segoe Fluent Icons" VerticalOptions="Center" />
								<VerticalStackLayout Grid.Column="1" VerticalOptions="Center" Spacing="0">
									<Label Text="Start minimized to tray" />
									<Label Text="No window on launch; summon with the hotkey" Style="{StaticResource Caption}" FontSize="11" />
								</VerticalStackLayout>
								<Switch Grid.Column="2" AutomationId="StartMinimizedSwitch"
									SemanticProperties.Description="Start minimized to tray"
									IsToggled="{Binding StartMinimized}" VerticalOptions="Center" />
							</Grid>
						</VerticalStackLayout>
					</Border>

					<Label Text="BEHAVIOR" Style="{StaticResource SectionHeader}" />
					<Border Style="{StaticResource SettingsCard}">
						<Grid ColumnDefinitions="Auto,*,180" ColumnSpacing="10" HeightRequest="52">
							<Label Text="&#xE916;" FontFamily="Segoe Fluent Icons" VerticalOptions="Center" />
							<VerticalStackLayout Grid.Column="1" VerticalOptions="Center" Spacing="0">
								<Label Text="Auto-close delay" />
								<Label Text="{Binding AutoCloseDelayLabel}" Style="{StaticResource Caption}" FontSize="11" />
							</VerticalStackLayout>
							<Picker Grid.Column="2" AutomationId="AutoCloseDelayPicker"
								SemanticProperties.Description="Auto-close delay"
								ItemsSource="{Binding DelayOptions}" SelectedItem="{Binding SelectedDelayOption}"
								VerticalOptions="Center" />
						</Grid>
					</Border>
				</VerticalStackLayout>

			</VerticalStackLayout>
		</ScrollView>
	</Grid>
```

- [x] **Step 2: Build, unit tests, screenshot of settings**

Build + 205/205; capture main, click the gear (or navigate via tray Settings), capture `$env:TEMP\bengy-task7-settings.png`.
Expected: card groups per spec §4, window grew to 560×620 on navigation, back chevron returns and shrinks the window.

- [x] **Step 3: Commit**

```bash
git add src/BeNGy/Views/SettingsPage.xaml
git commit -m "feat(design): PowerToys-style settings cards"
```

---

### Task 8: WinUI handler polish (chosen depth: control templates)

**Files:**
- Create: `src/BeNGy/Platforms/Windows/FluentHandlers.cs`
- Modify: `src/BeNGy/MauiProgram.cs` (call `FluentHandlers.Register()` inside a `#if WINDOWS` block in `CreateMauiApp`)

**Interfaces:**
- Produces: `static void FluentHandlers.Register()` — per-control WinUI adjustments MAUI styles can't reach: 4px corner radius on TextBox/ComboBox and removal of the WinUI focus underline in favor of an even 1px border.

- [x] **Step 1: Write FluentHandlers.cs**

```csharp
namespace BeNGy.Platforms.Windows;

/// <summary>
/// WinUI-level control adjustments that MAUI styles cannot express.
/// Scoped deliberately small (spec §8): anything that fights back gets
/// dropped to token-level styling rather than blocking the redesign.
/// </summary>
public static class FluentHandlers
{
    public static void Register()
    {
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("Fluent", (handler, _) =>
        {
            var box = handler.PlatformView;
            box.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(4);
            // Replace the accent bottom-underline focus style with an even border.
            box.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(1);
        });

        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("Fluent", (handler, _) =>
        {
            handler.PlatformView.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(4);
        });
    }
}
```

- [x] **Step 2: Register in MauiProgram.cs**

Inside `CreateMauiApp()`, before `return builder.Build();`:

```csharp
#if WINDOWS
		BeNGy.Platforms.Windows.FluentHandlers.Register();
#endif
```

- [x] **Step 3: Build, unit tests, screenshot**

Build + 205/205; capture and compare input corners/focus behavior (click into ticket field first).
Expected: inputs have 4px radii and no fat accent underline on focus. If a handler tweak crashes or has no effect, delete that single mapping, note it in the commit message, and move on (spec §8 fallback).

- [x] **Step 4: Commit**

```bash
git add src/BeNGy/Platforms/Windows/FluentHandlers.cs src/BeNGy/MauiProgram.cs
git commit -m "feat(design): WinUI handler polish for inputs"
```

---

### Task 9: Verification sweep + user approval gate

**Files:**
- No source changes expected (fixes only if verification fails).

- [x] **Step 1: Full build + unit suite**

`dotnet build D:/projects/bengy/BeNGy.sln -v minimal` → 0 warnings/errors; unit 205/205.

> Done 2026-11-11: 0 warnings/errors; unit 209/209.

- [x] **Step 2: Screenshot set for approval**

Using `tools/dev/capture-bengy.ps1` + manual driving, produce: main empty, main valid, main invalid, settings — in BOTH light and dark (flip via Settings → Theme; restore to System afterwards). Save to the scratchpad and show all eight to the user.

> Done 2026-11-11 at 1920x1080 125%, driven through UIA patterns (no synthesized input). A theme change made through UIA while the dropdown is open leaves the window's UIA tree stuck on "PopupHost", so the app was relaunched after each theme switch, as the E2E fixture does. Theme restored to System.

- [x] **Step 3: Sandbox E2E run**

`powershell -File tests/BeNGy.E2ETests/run-e2e-sandbox.ps1`
Expected: **48/65**, failure list identical to the 17 in Global Constraints. Diff the TRX against that list; any new failure = regression → fix before proceeding (LayoutFitTests and HiddenWindowRecoveryTests in particular must pass).

> Done 2026-11-11: **65/65** (the baseline became 65/65 in commit 5768219, so the 17-failure list above is obsolete). This was the first sandbox run at 125% (the guest now follows the host's scale). It exposed an intermittent drag-test failure, fixed in 6df6188.

- [x] **Step 4: Restore user's desktop state**

The sandbox leaves the host untouched, but Step 2's manual driving ran on the host: verify Default Prefix = `feature`, Theme = `System` in the app's Settings afterwards.

> Done 2026-11-11: Theme = System, read back after a relaunch. The capture never touches Default Prefix; it stayed at the user's own value (`hotfix`, not `feature`). The form was left empty and BeNGy closed, since it was not running before.

- [x] **Step 5: User approval gate**

> Approved by the user on 2026-11-11, after live-testing pass five.

Present the screenshots and the E2E comparison. **Do not merge without explicit user approval of the look.**

- [x] **Step 6: Merge and push (only after approval)**

> Merged and pushed on 2026-11-11 as release v1.1.0.

```bash
git checkout main
git merge --no-ff redesign/powertoys-palette -m "Merge PowerToys-style palette redesign"
git push origin main
```
