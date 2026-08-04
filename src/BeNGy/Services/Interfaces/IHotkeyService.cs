namespace BeNGy.Services.Interfaces;

/// <summary>
/// Abstraction for global hotkey registration and notification.
/// Platform implementations handle native hotkey registration (e.g., Win32 RegisterHotKey).
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Registers a global hotkey with the specified modifiers and key.
    /// </summary>
    /// <returns>A result indicating success or failure with error details.</returns>
    HotkeyRegistrationResult RegisterHotkey(HotkeyModifiers modifiers, HotkeyKey key);

    /// <summary>
    /// Unregisters the currently registered global hotkey.
    /// </summary>
    void UnregisterHotkey();

    /// <summary>
    /// Fired on the UI thread when the registered hotkey is pressed.
    /// </summary>
    event EventHandler? HotkeyPressed;

    /// <summary>
    /// True when a hotkey is currently registered and active.
    /// </summary>
    bool IsRegistered { get; }
}

// ⚠️ Named HotkeyModifiers/HotkeyKey to avoid collision with
// System.Windows.Input.ModifierKeys and Microsoft.UI.Input.VirtualKey

/// <summary>
/// Modifier keys for global hotkey registration.
/// Values match Win32 MOD_* constants for direct P/Invoke use.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,        // MOD_ALT
    Control = 2,    // MOD_CONTROL
    Shift = 4,      // MOD_SHIFT
    Win = 8         // MOD_WIN
}

/// <summary>
/// Virtual key codes for hotkey registration.
/// Values match Win32 VK_* constants.
/// </summary>
public enum HotkeyKey
{
    None = 0,
    Backspace = 0x08, Tab = 0x09, Enter = 0x0D, Escape = 0x1B, Space = 0x20,
    PageUp = 0x21, PageDown = 0x22, End = 0x23, Home = 0x24,
    Left = 0x25, Up = 0x26, Right = 0x27, Down = 0x28,
    Insert = 0x2D, Delete = 0x2E,
    D0 = 0x30, D1 = 0x31, D2 = 0x32, D3 = 0x33, D4 = 0x34, D5 = 0x35, D6 = 0x36, D7 = 0x37, D8 = 0x38, D9 = 0x39,
    A = 0x41, B = 0x42, C = 0x43, D = 0x44, E = 0x45, F = 0x46, G = 0x47, H = 0x48, I = 0x49,
    J = 0x4A, K = 0x4B, L = 0x4C, M = 0x4D, N = 0x4E, O = 0x4F, P = 0x50, Q = 0x51, R = 0x52,
    S = 0x53, T = 0x54, U = 0x55, V = 0x56, W = 0x57, X = 0x58, Y = 0x59, Z = 0x5A,
    F1 = 0x70, F2 = 0x71, F3 = 0x72, F4 = 0x73, F5 = 0x74, F6 = 0x75, F7 = 0x76, F8 = 0x77, F9 = 0x78, F10 = 0x79, F11 = 0x7A, F12 = 0x7B
}

/// <summary>
/// Result of a hotkey registration attempt. Includes error details for user notification (Story 5.2).
/// Implemented from the start to avoid breaking interface changes between 5.1 and 5.2.
/// </summary>
public class HotkeyRegistrationResult
{
    public bool IsSuccess { get; private init; }
    public string ErrorMessage { get; private init; } = string.Empty;

    public static HotkeyRegistrationResult Success() => new() { IsSuccess = true };
    public static HotkeyRegistrationResult Failed(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}
