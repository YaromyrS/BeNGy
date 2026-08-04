using System.Globalization;

namespace BeNGy.Helpers;

/// <summary>
/// Converts a string to a boolean indicating whether it is non-null and non-empty.
/// Used to bind Label.IsVisible to string properties without code-behind.
/// </summary>
public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
