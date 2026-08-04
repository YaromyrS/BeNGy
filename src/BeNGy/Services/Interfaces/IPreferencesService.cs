namespace BeNGy.Services.Interfaces;

/// <summary>
/// Abstraction for application preferences persistence.
/// Wraps MAUI Preferences API for testability and type safety.
/// </summary>
public interface IPreferencesService
{
    /// <summary>
    /// Gets a preference value, returning <paramref name="defaultValue"/> if not set.
    /// Supports string, int, bool, double types.
    /// </summary>
    T Get<T>(string key, T defaultValue);

    /// <summary>
    /// Sets a preference value. Supports string, int, bool, double types.
    /// </summary>
    void Set<T>(string key, T value);

    /// <summary>
    /// Removes a preference by key.
    /// </summary>
    void Remove(string key);

    /// <summary>
    /// Checks whether a preference key exists.
    /// </summary>
    bool ContainsKey(string key);

    /// <summary>
    /// Clears all preferences.
    /// </summary>
    void Clear();
}
