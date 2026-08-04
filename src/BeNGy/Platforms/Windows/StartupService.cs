using BeNGy.Models;
using BeNGy.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace BeNGy.Platforms.Windows;

/// <summary>
/// Manages auto-start via HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run registry key.
/// Adds "--minimized" flag so the app starts in the system tray.
/// </summary>
public class StartupService : IStartupService
{
    private const string StartupKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "BeNGy";

    private readonly ILogger<StartupService> _logger;
    private readonly IPreferencesService _preferences;

    public StartupService(ILogger<StartupService> logger, IPreferencesService preferences)
    {
        _logger = logger;
        _preferences = preferences;
    }

    public bool IsStartWithWindows
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading startup registry");
                return false;
            }
        }
    }

    public void SetStartWithWindows(bool enabled)
    {
        try
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

            _preferences.Set(PreferenceKeys.StartWithWindows, enabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error modifying startup registry");
        }
    }
}
