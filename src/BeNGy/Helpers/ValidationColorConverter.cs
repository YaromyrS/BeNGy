using System.Globalization;

namespace BeNGy.Helpers;

/// <summary>
/// Converts a boolean IsValid value to a display color.
/// True → Green (#22C55E) | False → Amber (#F59E0B)
/// </summary>
public class ValidationColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool isValid && isValid
            ? Color.FromArgb("#22C55E")
            : Color.FromArgb("#F59E0B");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
