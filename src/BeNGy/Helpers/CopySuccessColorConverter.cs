using System.Globalization;

namespace BeNGy.Helpers;

/// <summary>
/// Converts a boolean CopySuccess flag to a button background color.
/// True → Green (#22C55E) for success feedback | False → Blue (#3B82F6) for normal state.
/// </summary>
public class CopySuccessColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool isSuccess && isSuccess
            ? Color.FromArgb("#22C55E")   // Green — success
            : Color.FromArgb("#3B82F6");  // Blue  — normal

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
