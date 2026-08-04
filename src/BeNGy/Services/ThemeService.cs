using BeNGy.Models;
using BeNGy.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BeNGy.Services;

/// <summary>
/// Manages application theme by setting Application.UserAppTheme.
/// "System" → Unspecified (follows Windows), "Light"/"Dark" → forced.
/// Persists selection via IPreferencesService.
/// </summary>
public class ThemeService : IThemeService, IDisposable
{
    private readonly IPreferencesService _preferences;
    private readonly ILogger<ThemeService> _logger;
    private BeNGyTheme _currentTheme;
    private bool _isSubscribed;

    public event EventHandler<BeNGyTheme>? ThemeChanged;

    public ThemeService(IPreferencesService preferences, ILogger<ThemeService> logger)
    {
        _preferences = preferences;
        _logger = logger;

        var saved = _preferences.Get(PreferenceKeys.Theme, PreferenceKeys.ThemeDefault);
        _currentTheme = Enum.TryParse<BeNGyTheme>(saved, true, out var parsed)
            ? parsed
            : BeNGyTheme.System;
        
        _logger.LogInformation("ThemeService initialized. Current setting: {Theme}", _currentTheme);

        TrySubscribe();
    }

    public BeNGyTheme CurrentTheme => _currentTheme;

    public BeNGyTheme EffectiveTheme => _currentTheme == BeNGyTheme.System 
        ? ResolveConcreteSystemTheme() 
        : _currentTheme;

    public void SetTheme(BeNGyTheme theme)
    {
        if (_currentTheme == theme) return;

        _logger.LogInformation("Changing theme to: {Theme}", theme);
        _currentTheme = theme;
        _preferences.Set(PreferenceKeys.Theme, theme.ToString().ToLowerInvariant());
        
        ApplyThemeToMaui(theme);
        ThemeChanged?.Invoke(this, EffectiveTheme);
    }

    public void ApplyCurrentTheme()
    {
        _logger.LogDebug("Applying current theme: {Theme}", _currentTheme);
        ApplyThemeToMaui(_currentTheme);
        TrySubscribe(); // Ensure we are subscribed after App initialization
    }

    private void ApplyThemeToMaui(BeNGyTheme theme)
    {
        var app = Application.Current;
        if (app == null)
        {
            _logger.LogWarning("Application.Current is null, cannot apply theme.");
            return;
        }

        app.UserAppTheme = MapToMauiTheme(theme);
        _logger.LogDebug("Applied UserAppTheme: {Theme}", app.UserAppTheme);
    }

    public BeNGyTheme GetSystemTheme()
    {
        var requested = Application.Current?.RequestedTheme;
        _logger.LogDebug("System requested theme: {Theme}", requested);
        return MapFromMauiTheme(requested);
    }

    private BeNGyTheme ResolveConcreteSystemTheme()
    {
        var system = GetSystemTheme();
        // Resolve 'System' (as a return value from GetSystemTheme) to a concrete fallback
        return system == BeNGyTheme.System ? BeNGyTheme.Light : system;
    }

    private void OnSystemThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        _logger.LogDebug("System theme changed event received: {Theme}", e.RequestedTheme);
        
        if (_currentTheme == BeNGyTheme.System)
        {
            _logger.LogInformation("Automatic theme switch triggered by system: {Theme}", e.RequestedTheme);
            ThemeChanged?.Invoke(this, MapFromMauiTheme(e.RequestedTheme));
        }
    }

    private void TrySubscribe()
    {
        if (!_isSubscribed && Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += OnSystemThemeChanged;
            _isSubscribed = true;
            _logger.LogDebug("Subscribed to Application.RequestedThemeChanged");
        }
    }

    private static Microsoft.Maui.ApplicationModel.AppTheme MapToMauiTheme(BeNGyTheme theme) => theme switch
    {
        BeNGyTheme.Light => Microsoft.Maui.ApplicationModel.AppTheme.Light,
        BeNGyTheme.Dark => Microsoft.Maui.ApplicationModel.AppTheme.Dark,
        _ => Microsoft.Maui.ApplicationModel.AppTheme.Unspecified
    };

    private static BeNGyTheme MapFromMauiTheme(Microsoft.Maui.ApplicationModel.AppTheme? theme) => theme switch
    {
        Microsoft.Maui.ApplicationModel.AppTheme.Light => BeNGyTheme.Light,
        Microsoft.Maui.ApplicationModel.AppTheme.Dark => BeNGyTheme.Dark,
        _ => BeNGyTheme.Light // Fallback to Light for Unspecified/Null as per PRD
    };

    public void Dispose()
    {
        if (_isSubscribed && Application.Current != null)
        {
            Application.Current.RequestedThemeChanged -= OnSystemThemeChanged;
            _isSubscribed = false;
            _logger.LogDebug("ThemeService unsubscribed from RequestedThemeChanged");
        }
    }
}
