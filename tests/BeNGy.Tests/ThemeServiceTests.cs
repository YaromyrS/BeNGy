using BeNGy.Models;
using BeNGy.Services;
using BeNGy.Services.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BeNGy.Tests;

public class ThemeServiceTests
{
    private readonly IPreferencesService _preferences;
    private readonly ILogger<ThemeService> _logger;

    public ThemeServiceTests()
    {
        _preferences = Substitute.For<IPreferencesService>();
        _logger = Substitute.For<ILogger<ThemeService>>();
        
        _preferences.Get(PreferenceKeys.Theme, PreferenceKeys.ThemeDefault)
            .Returns(PreferenceKeys.ThemeDefault);
    }

    [Fact]
    public void SetTheme_SavesPreferenceAndAppliesTheme()
    {
        var service = new ThemeService(_preferences, _logger);
        
        service.SetTheme(BeNGyTheme.Dark);
        
        Assert.Equal(BeNGyTheme.Dark, service.CurrentTheme);
        _preferences.Received(1).Set(PreferenceKeys.Theme, "dark");
    }

    [Fact]
    public void SetTheme_FiresThemeChangedEvent()
    {
        var service = new ThemeService(_preferences, _logger);
        BeNGyTheme? capturedTheme = null;
        service.ThemeChanged += (s, e) => capturedTheme = e;

        service.SetTheme(BeNGyTheme.Dark);

        Assert.Equal(BeNGyTheme.Dark, capturedTheme);
    }

    [Fact]
    public void GetSystemTheme_ReturnsCorrectTheme()
    {
        // Application.Current is null in headless tests.
        var service = new ThemeService(_preferences, _logger);
        
        var theme = service.GetSystemTheme();
        
        // MapFromMauiTheme(null) returns Light as per PRD fallback
        Assert.Equal(BeNGyTheme.Light, theme); 
    }

    [Fact]
    public void EffectiveTheme_ReturnsConcreteThemeEvenIfSystem()
    {
        var service = new ThemeService(_preferences, _logger);
        
        // Setting is 'System' (from constructor)
        Assert.Equal(BeNGyTheme.System, service.CurrentTheme);
        
        // Effective resolves 'System' (via GetSystemTheme/MapFromMauiTheme) to 'Light'
        Assert.Equal(BeNGyTheme.Light, service.EffectiveTheme);
    }
}
