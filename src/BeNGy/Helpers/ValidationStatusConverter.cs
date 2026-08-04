using System.Globalization;

namespace BeNGy.Helpers;

/// <summary>
/// Converts a boolean IsValid value to a human-readable validation status string.
/// True → "Valid" | False → "Needs attention"
/// </summary>
public class ValidationStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool isValid && isValid ? "Valid" : "Needs attention";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
