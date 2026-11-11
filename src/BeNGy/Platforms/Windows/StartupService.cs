using BeNGy.Models;
using BeNGy.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace BeNGy.Platforms.Windows;

/// <summary>
/// "Start with Windows", in the mechanism that fits how BeNGy was installed:
/// <list type="bullet">
/// <item>Unpackaged (installer, MSI, portable zip): a value under
/// HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run, launched with
/// "--minimized" so the app starts in the tray.</item>
/// <item>Packaged (MSIX / Microsoft Store): the manifest's StartupTask. The Run
/// key is no use there -- a packaged app's HKCU writes go to a private hive,
/// and its exe path changes with every update. A startup task passes no
/// arguments; App.ShouldStartMinimized recognises the activation instead.</item>
/// </list>
/// </summary>
public class StartupService : IStartupService
{
    private const string StartupKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "BeNGy";

    /// <summary>Must match the uap5:StartupTask TaskId in Package.appxmanifest.</summary>
    internal const string StartupTaskId = "BeNGyStartup";

    private readonly ILogger<StartupService> _logger;
    private readonly IPreferencesService _preferences;

    public StartupService(ILogger<StartupService> logger, IPreferencesService preferences)
    {
        _logger = logger;
        _preferences = preferences;
    }

    private static bool IsPackaged =>
        Microsoft.Maui.ApplicationModel.AppInfo.Current.PackagingModel == AppPackagingModel.Packaged;

    public bool IsStartWithWindows
    {
        get
        {
            try
            {
                if (IsPackaged)
                    return GetStartupTask().State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;

                using var key = Registry.CurrentUser.OpenSubKey(StartupKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading the start-with-Windows state");
                return false;
            }
        }
    }

    public void SetStartWithWindows(bool enabled)
    {
        try
        {
            if (IsPackaged)
                SetStartupTask(enabled);
            else
                SetRunKey(enabled);

            _preferences.Set(PreferenceKeys.StartWithWindows, enabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing the start-with-Windows state");
        }
    }

    private void SetRunKey(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupKey, true);

        if (key == null)
        {
            _logger.LogError("Could not open startup registry key");
            return;
        }

        if (enabled)
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                _logger.LogError("Could not determine executable path");
                return;
            }

            key.SetValue(AppName, $"\"{exePath}\" --minimized");
            _logger.LogInformation("Added startup registry entry: {Path}", exePath);
        }
        else
        {
            key.DeleteValue(AppName, false);
            _logger.LogInformation("Removed startup registry entry");
        }
    }

    private void SetStartupTask(bool enabled)
    {
        var task = GetStartupTask();
        if (!enabled)
        {
            task.Disable();
            _logger.LogInformation("Startup task disabled");
            return;
        }

        // A task the user turned off in Task Manager or Settings (DisabledByUser)
        // cannot be re-enabled by the app; only the user can, from there.
        var state = Task.Run(async () => await task.RequestEnableAsync()).GetAwaiter().GetResult();
        if (state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy)
            _logger.LogInformation("Startup task enabled");
        else
            _logger.LogWarning("Startup task not enabled; state is {State}", state);
    }

    // The WinRT async calls complete off the UI thread, so waiting on them from
    // a property getter cannot deadlock; Task.Run keeps any synchronization
    // context out of the continuation.
    private static StartupTask GetStartupTask() =>
        Task.Run(async () => await StartupTask.GetAsync(StartupTaskId)).GetAwaiter().GetResult();
}
