using System.Text.RegularExpressions;

namespace BeNGy.Helpers;

public static class StringTransformers
{
    public static string ToKebabCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Replace spaces and underscores with hyphens, lowercase
        var result = Regex.Replace(input.Trim(), @"[\s_]+", "-");
        result = Regex.Replace(result, @"([a-z])([A-Z])", "$1-$2"); // camelCase handling
        return result.ToLowerInvariant();
    }

    public static string ToSnakeCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var result = Regex.Replace(input.Trim(), @"[\s-]+", "_");
        result = Regex.Replace(result, @"([a-z])([A-Z])", "$1_$2");
        return result.ToLowerInvariant();
    }

    public static string ToPascalCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var words = Regex.Split(input.Trim(), @"[\s_-]+");
        return string.Concat(words
            .Where(w => w.Length > 0)
            .Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));
    }
}
