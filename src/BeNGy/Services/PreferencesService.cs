using BeNGy.Services.Interfaces;

namespace BeNGy.Services;

/// <summary>
/// Wraps MAUI Preferences API for type-safe persistence.
/// Stored at %LOCALAPPDATA%\[AppId]\Settings on Windows.
/// </summary>
public class PreferencesService : IPreferencesService
{
    private readonly ILogger<PreferencesService> _logger;

    public PreferencesService(ILogger<PreferencesService> logger)
    {
        _logger = logger;
    }

    public T Get<T>(string key, T defaultValue)
    {
        try
        {
            if (defaultValue is string s)
                return (T)(object)Preferences.Get(key, s);
            if (defaultValue is int i)
                return (T)(object)Preferences.Get(key, i);
            if (defaultValue is bool b)
                return (T)(object)Preferences.Get(key, b);
            if (defaultValue is double d)
                return (T)(object)Preferences.Get(key, d);
            if (defaultValue is float f)
                return (T)(object)Preferences.Get(key, f);
            if (defaultValue is long l)
                return (T)(object)Preferences.Get(key, l);

            _logger.LogWarning("Unsupported preference type: {Type}", typeof(T).Name);
            return defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading preference {Key}", key);
            return defaultValue;
        }
    }

    public void Set<T>(string key, T value)
    {
        try
        {
            if (value is string s)
                Preferences.Set(key, s);
            else if (value is int i)
                Preferences.Set(key, i);
            else if (value is bool b)
                Preferences.Set(key, b);
            else if (value is double d)
                Preferences.Set(key, d);
            else if (value is float f)
                Preferences.Set(key, f);
            else if (value is long l)
                Preferences.Set(key, l);
            else
                _logger.LogWarning("Unsupported preference type: {Type}", typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting preference {Key}", key);
        }
    }

    public void Remove(string key) => Preferences.Remove(key);
    public bool ContainsKey(string key) => Preferences.ContainsKey(key);
    public void Clear() => Preferences.Clear();
}
