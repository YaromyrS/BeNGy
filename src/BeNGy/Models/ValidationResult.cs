namespace BeNGy.Models;

public class ValidationResult
{
    public bool IsValid { get; private set; }
    public IReadOnlyList<char> InvalidCharacters { get; private set; } = Array.Empty<char>();
    public string ErrorMessage { get; private set; } = string.Empty;
    public IReadOnlyList<string> ValidationErrors { get; private set; } = Array.Empty<string>();

    private ValidationResult() { }

    public static ValidationResult Valid() => new() { IsValid = true };

    public static ValidationResult Invalid(string message, IEnumerable<char>? invalidChars = null)
    {
        return new ValidationResult
        {
            IsValid = false,
            ErrorMessage = message,
            InvalidCharacters = invalidChars?.Distinct().ToList() ?? new List<char>()
        };
    }

    public static ValidationResult Invalid(IEnumerable<string> errors, IEnumerable<char>? invalidChars = null)
    {
        var errorsList = errors.ToList();
        return new ValidationResult
        {
            IsValid = false,
            ValidationErrors = errorsList,
            ErrorMessage = string.Join("; ", errorsList),
            InvalidCharacters = invalidChars?.Distinct().ToList() ?? new List<char>()
        };
    }
}
