using System.Globalization;

namespace BeNGy.Helpers;

/// <summary>
/// Splits a formatted hotkey string ("Ctrl+Shift+B") into its parts so the UI
/// can render each key as its own keycap chip, PowerToys-style.
/// </summary>
public class HotkeyPartsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return Array.Empty<string>();

        return s.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
