using BeNGy.Helpers;
using BeNGy.Models;
using BeNGy.Services.Interfaces;

namespace BeNGy.Services;

public class BranchNameGenerator : IBranchNameGenerator
{
    public string Generate(BranchInput input)
    {
        if (string.IsNullOrWhiteSpace(input.TicketNumber) &&
            string.IsNullOrWhiteSpace(input.Title))
            return string.Empty;

        var transformedTitle = TransformTitle(input.Title, input.TitleFormat);

        var result = input.Prefix;

        if (!string.IsNullOrWhiteSpace(input.TicketNumber))
        {
            result += input.PrefixTicketSeparator + input.TicketNumber;
        }

        if (!string.IsNullOrWhiteSpace(transformedTitle))
        {
            result += input.TicketTitleSeparator + transformedTitle;
        }

        return result;
    }

    private static string TransformTitle(string title, string format)
    {
        return format switch
        {
            "kebab-case" => StringTransformers.ToKebabCase(title),
            "snake_case" => StringTransformers.ToSnakeCase(title),
            "PascalCase" => StringTransformers.ToPascalCase(title),
            _ => StringTransformers.ToKebabCase(title)
        };
    }
}
