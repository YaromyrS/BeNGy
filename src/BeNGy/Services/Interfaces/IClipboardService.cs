namespace BeNGy.Services.Interfaces;

/// <summary>
/// Abstraction over system clipboard operations.
/// Enables unit-testing the copy flow without platform dependencies.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copies text to the system clipboard.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    /// <returns>True if the copy succeeded, false otherwise.</returns>
    Task<bool> CopyToClipboardAsync(string text);
}
