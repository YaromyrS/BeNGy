namespace BeNGy.Services.Interfaces;

/// <summary>
/// Abstraction for managing application auto-start with Windows.
/// Platform implementations handle the native startup mechanism (e.g., registry).
/// </summary>
public interface IStartupService
{
    /// <summary>
    /// Gets whether the app is configured to start with Windows.
    /// Reads from the native mechanism (registry), not the preference.
    /// </summary>
    bool IsStartWithWindows { get; }

    /// <summary>
    /// Enables or disables auto-start with Windows.
    /// Modifies the native mechanism and syncs the preference.
    /// </summary>
    void SetStartWithWindows(bool enabled);
}
