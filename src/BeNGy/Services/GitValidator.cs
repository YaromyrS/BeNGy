using BeNGy.Models;
using BeNGy.Services.Interfaces;

namespace BeNGy.Services;

public class GitValidator : IGitValidator
{
    // Characters not allowed in git branch names
    private static readonly char[] InvalidChars = { ' ', '~', '^', ':', '?', '*', '[', '\\' };

    public ValidationResult Validate(string branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return ValidationResult.Invalid("Branch name cannot be empty");
        }

        var errors = new List<string>();
        var invalidCharsFound = new List<char>();

        // Check for invalid characters
        foreach (var c in InvalidChars)
        {
            if (branchName.Contains(c))
            {
                invalidCharsFound.Add(c);
            }
        }

        if (invalidCharsFound.Count > 0)
        {
            var charList = string.Join(", ", invalidCharsFound.Select(c => c == ' ' ? "space" : $"'{c}'"));
            errors.Add($"Invalid characters: {charList}");
        }

        // Check for consecutive dots
        if (branchName.Contains(".."))
        {
            errors.Add("Cannot contain consecutive dots (..)");
        }

        // Check for @{ sequence (reflog syntax).
        // Note: '@' alone is valid in git branch names (e.g. "user@email").
        // We add '@' to InvalidCharacters as a visual indicator — only the @{ *sequence* is invalid.
        if (branchName.Contains("@{"))
        {
            errors.Add("Cannot contain @{ sequence");
            if (!invalidCharsFound.Contains('@'))
            {
                invalidCharsFound.Add('@');
            }
        }

        // Check for leading/trailing dots
        if (branchName.StartsWith('.') || branchName.EndsWith('.'))
        {
            errors.Add("Cannot start or end with a dot");
        }

        // Check for leading/trailing slashes
        if (branchName.StartsWith('/') || branchName.EndsWith('/'))
        {
            errors.Add("Cannot start or end with a slash");
        }

        // Check for consecutive slashes
        if (branchName.Contains("//"))
        {
            errors.Add("Cannot contain consecutive slashes");
        }

        // Check for .lock suffix
        if (branchName.EndsWith(".lock"))
        {
            errors.Add("Cannot end with .lock");
        }

        if (errors.Count > 0)
        {
            return ValidationResult.Invalid(errors, invalidCharsFound);
        }

        return ValidationResult.Valid();
    }
}
