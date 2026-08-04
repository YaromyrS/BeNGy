using BeNGy.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BeNGy.Services;

/// <summary>
/// Clipboard service backed by the MAUI Clipboard API.
/// Wraps the call in a try/catch so failures are surfaced as a bool
/// rather than bubbling exceptions up to the ViewModel.
/// </summary>
public class ClipboardService : IClipboardService
{
    private readonly ILogger<ClipboardService> _logger;

    public ClipboardService(ILogger<ClipboardService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> CopyToClipboardAsync(string text)
    {
        try
        {
            await Clipboard.SetTextAsync(text);
            _logger.LogDebug("Copied to clipboard: {Text}", text);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy to clipboard");
            return false;
        }
    }
}
