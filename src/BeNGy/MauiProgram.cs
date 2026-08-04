using BeNGy.Helpers;
using BeNGy.Services;
using BeNGy.Services.Interfaces;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace BeNGy;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// Single-instance enforcement: acquire mutex before building the app
		var singleInstance = new SingleInstanceManager(
			"BeNGy_SingleInstance_20c5be2d",
			"BeNGy_Activation_20c5be2d",
			action => MainThread.BeginInvokeOnMainThread(action));
		if (!singleInstance.TryAcquire())
		{
			// Another instance is already running — it has been signaled.
			Environment.Exit(0);
			return null!; // Unreachable
		}

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton(singleInstance);

		// Cross-platform services (Story 6.1)
		builder.Services.AddSingleton<IPreferencesService, PreferencesService>();
		builder.Services.AddSingleton<IThemeService, ThemeService>();

#if WINDOWS
		builder.Services.AddSingleton<ITrayService, BeNGy.Platforms.Windows.TrayService>();
		builder.Services.AddSingleton<IWindowService, BeNGy.Platforms.Windows.WindowService>();
		builder.Services.AddSingleton<IHotkeyService, BeNGy.Platforms.Windows.HotkeyService>();
		builder.Services.AddSingleton<IStartupService, BeNGy.Platforms.Windows.StartupService>();
#endif

		builder.Services.AddSingleton<IBranchNameGenerator, BranchNameGenerator>();
		builder.Services.AddSingleton<IGitValidator, GitValidator>();
		builder.Services.AddSingleton<IClipboardService, ClipboardService>();
		builder.Services.AddTransient<MainViewModel>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
