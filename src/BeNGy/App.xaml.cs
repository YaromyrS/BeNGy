using BeNGy.Helpers;
using BeNGy.Models;
using BeNGy.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BeNGy;

public partial class App : Application
{
	private readonly ITrayService? _trayService;
	private readonly IWindowService? _windowService;
	private readonly IHotkeyService? _hotkeyService;
	private readonly IPreferencesService? _preferences;
	private readonly IThemeService? _themeService;
	private readonly SingleInstanceManager? _singleInstance;
	private readonly ILogger<App>? _logger;
	private Window? _mainWindow;
	private bool _isExiting;

	public App(
		ITrayService? trayService = null,
		IWindowService? windowService = null,
		IHotkeyService? hotkeyService = null,
		IPreferencesService? preferences = null,
		IThemeService? themeService = null,
		SingleInstanceManager? singleInstance = null,
		ILogger<App>? logger = null)
	{
		InitializeComponent();
#if WINDOWS
		BeNGy.Platforms.Windows.SystemAccent.Apply(Resources);
#endif
		_trayService = trayService;
		_windowService = windowService;
		_hotkeyService = hotkeyService;
		_preferences = preferences;
		_themeService = themeService;
		_singleInstance = singleInstance;
		_logger = logger;

		if (_singleInstance != null)
		{
			_singleInstance.ActivationRequested += OnActivationRequested;
		}
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var services = Handler?.MauiContext?.Services;
		var viewModel = services?.GetService<MainViewModel>()
			?? new MainViewModel(
				new BeNGy.Services.BranchNameGenerator(),
				new BeNGy.Services.GitValidator(),
				services?.GetService<IClipboardService>()!,
				services?.GetService<IWindowService>()!);

		var mainPage = new MainPage(viewModel);
		_mainWindow = new Window(new NavigationPage(mainPage)) { Title = "BeNGy" };

		_mainWindow.Created += (_, _) => OnWindowCreated();

		return _mainWindow;
	}

	private void OnWindowCreated()
	{
		_logger?.LogInformation("BeNGy window created, initializing services...");

		InitializeWindowService();
		InitializeTray();
		InitializeHotkey();
		InitializeTheme();

		// Story 6.6: Start minimized check
		if (ShouldStartMinimized())
		{
			_windowService?.HideWindow();
		}
		else
		{
			_windowService?.ShowWindow();
		}

		_logger?.LogInformation("BeNGy startup complete");
	}

	private void InitializeWindowService()
	{
#if WINDOWS
		if (_windowService is Platforms.Windows.WindowService winSvc && _mainWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
		{
			winSvc.Initialize(nativeWindow);
			_logger?.LogDebug("WindowService initialized with native window");
		}
#endif
	}

	private void InitializeTray()
	{
		if (_trayService == null) return;

		_trayService.Initialize();
		_logger?.LogDebug("TrayService initialized, icon visible");

		_trayService.OpenRequested += (_, _) => _windowService?.ShowWindow();

		_trayService.SettingsRequested += (_, _) => OpenSettings();

		_trayService.ExitRequested += (_, _) => ExitApplication();
	}

	/// <summary>
	/// Registers the global hotkey and wires HotkeyPressed event.
	/// Reads hotkey from preferences (Story 6.3) or falls back to default Ctrl+Shift+B.
	/// On failure, shows balloon notification (Story 5.2).
	/// </summary>
	private void InitializeHotkey()
	{
		if (_hotkeyService == null) return;

		// Story 6.3: Load hotkey from preferences
		var modifiers = (HotkeyModifiers)(_preferences?.Get(
			PreferenceKeys.HotkeyModifiers,
			PreferenceKeys.HotkeyModifiersDefault) ?? PreferenceKeys.HotkeyModifiersDefault);

		var key = (HotkeyKey)(_preferences?.Get(
			PreferenceKeys.HotkeyKey,
			PreferenceKeys.HotkeyKeyDefault) ?? PreferenceKeys.HotkeyKeyDefault);

		var result = _hotkeyService.RegisterHotkey(modifiers, key);

		if (result.IsSuccess)
		{
			_hotkeyService.HotkeyPressed += OnHotkeyPressed;
			_logger?.LogInformation("Hotkey {Modifiers}+{Key} registered successfully", modifiers, key);
		}
		else
		{
			_logger?.LogWarning("Failed to register hotkey: {Error}", result.ErrorMessage);

			_trayService?.ShowBalloon(
				"Hotkey Unavailable",
				$"{FormatHotkey(modifiers, key)} is used by another app. You can open BeNGy from the system tray menu.");
		}
	}

	private void OnHotkeyPressed(object? sender, EventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			_windowService?.ShowWindow();
		});
	}

	/// <summary>
	/// Story 6.4: Apply saved theme on startup.
	/// </summary>
	private void InitializeTheme()
	{
		if (_themeService is BeNGy.Services.ThemeService ts)
		{
			ts.ApplyCurrentTheme();
			_logger?.LogDebug("Theme applied: {Theme}", _themeService.CurrentTheme);
		}
	}

	/// <summary>
	/// Story 6.6: Decides whether to start minimized based on preferences and command line.
	/// </summary>
	private bool ShouldStartMinimized()
	{
		// Command line flag takes precedence (for "Start with Windows" registry entry)
		var args = Environment.GetCommandLineArgs();
		if (args.Contains("--minimized"))
			return true;

		// Otherwise use setting (default: true)
		return _preferences?.Get(PreferenceKeys.StartMinimized, PreferenceKeys.StartMinimizedDefault)
			   ?? PreferenceKeys.StartMinimizedDefault;
	}

	/// <summary>
	/// Opens the Settings page in the current window.
	/// </summary>
	internal void OpenSettings()
	{
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			var services = Handler?.MauiContext?.Services;
			var settingsVm = services?.GetService<SettingsViewModel>();
			
			if (settingsVm != null && _mainWindow?.Page != null)
			{
				// Push instead of swap to allow "Back" button (C1 Fix)
				await _mainWindow.Page.Navigation.PushAsync(new SettingsPage(settingsVm));
				_windowService?.ShowWindow();
			}
		});
	}

	private void OnActivationRequested(object? sender, EventArgs e)
	{
		_logger?.LogDebug("Activation requested from second instance");
		_windowService?.ShowWindow();
	}

	/// <summary>
	/// Performs a clean application shutdown with proper disposal order.
	/// </summary>
	internal void ExitApplication()
	{
		if (_isExiting) return;
		_isExiting = true;

		_logger?.LogInformation("BeNGy exiting...");

		try
		{
			if (_hotkeyService != null)
			{
				_hotkeyService.HotkeyPressed -= OnHotkeyPressed;
				_hotkeyService.Dispose();
			}
			_windowService?.HideWindow();
			_trayService?.Dispose();
			_singleInstance?.Dispose();
		}
		catch (Exception ex)
		{
			_logger?.LogError(ex, "Error during shutdown cleanup");
		}

		_logger?.LogInformation("BeNGy exit complete");
		Current?.Quit();
	}

	private static string FormatHotkey(HotkeyModifiers modifiers, HotkeyKey key)
	{
		return BeNGy.Helpers.HotkeyFormattingHelper.FormatHotkey(modifiers, key);
	}
}
