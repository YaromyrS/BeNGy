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
        var hasTicket = !string.IsNullOrWhiteSpace(input.TicketNumber);

        if (hasTicket)
        {
            result += input.PrefixTicketSeparator + input.TicketNumber;
        }

        if (!string.IsNullOrWhiteSpace(transformedTitle))
        {
            // Whatever directly follows the prefix gets the prefix separator, so
            // a ticket-less branch reads "feature/add-login" (grouped under the
            // feature/ folder by git tools), not "feature_add-login".
            result += (hasTicket ? input.TicketTitleSeparator : input.PrefixTicketSeparator)
                + transformedTitle;
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
