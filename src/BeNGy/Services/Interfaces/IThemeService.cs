namespace BeNGy.Services.Interfaces;

/// <summary>
/// Theme modes for the application.
/// "System" follows the Windows system theme; "Light"/"Dark" override it.
/// </summary>
public enum BeNGyTheme
{
    Light,
    Dark,
    System
}

/// <summary>
/// Abstraction for application theme management.
/// Applies themes immediately without requiring restart.
/// </summary>
public interface IThemeService
{
    /// <summary>Gets the currently active theme setting.</summary>
    BeNGyTheme CurrentTheme { get; }

    /// <summary>
    /// Gets the concrete theme (Light or Dark) being displayed.
    /// If CurrentTheme is System, this returns the resolved system theme.
    /// </summary>
    BeNGyTheme EffectiveTheme { get; }

    /// <summary>
    /// Fired when the applied theme changes, either by user setting 
    /// or by a system theme change while in 'System' mode.
    /// </summary>
    event EventHandler<BeNGyTheme>? ThemeChanged;

    /// <summary>
    /// Sets the application theme and persists the selection.
    /// Changes are applied immediately (no restart required).
    /// </summary>
    void SetTheme(BeNGyTheme theme);

    /// <summary>Detects the current system (Windows) theme.</summary>
    BeNGyTheme GetSystemTheme();
}
