using BeNGy.Services;

namespace BeNGy.Tests;

public class GitValidatorTests
{
    private readonly GitValidator _validator = new();

    // ==========================================
    // 5.1 — Valid branch names pass
    // ==========================================

    [Theory]
    [InlineData("feature/PROJ-123_add-login")]
    [InlineData("bugfix/fix-null-pointer")]
    [InlineData("release/v1.0.0")]
    [InlineData("hotfix/urgent-patch")]
    [InlineData("feature/ABC-1_a")]
    public void Validate_ValidBranchNames_ReturnsValid(string branchName)
    {
        var result = _validator.Validate(branchName);

        Assert.True(result.IsValid);
        Assert.Empty(result.InvalidCharacters);
        Assert.Equal(string.Empty, result.ErrorMessage);
    }

    // ==========================================
    // 5.2 — Each invalid character is caught
    // ==========================================

    [Theory]
    [InlineData("feature/my branch", ' ')]
    [InlineData("feature/test~name", '~')]
    [InlineData("feature/test^name", '^')]
    [InlineData("feature/test:name", ':')]
    [InlineData("feature/test?name", '?')]
    [InlineData("feature/test*name", '*')]
    [InlineData("feature/test[name", '[')]
    [InlineData("feature/test\\name", '\\')]
    public void Validate_InvalidCharacter_ReturnsInvalidWithChar(string branchName, char expectedChar)
    {
        var result = _validator.Validate(branchName);

        Assert.False(result.IsValid);
        Assert.Contains(expectedChar, result.InvalidCharacters);
        Assert.NotEmpty(result.ErrorMessage);
    }

    // ==========================================
    // 5.3 — Consecutive dots detected
    // ==========================================

    [Fact]
    public void Validate_ConsecutiveDots_ReturnsInvalid()
    {
        var result = _validator.Validate("feature/test..name");

        Assert.False(result.IsValid);
        Assert.Contains("consecutive dots", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ==========================================
    // 5.4 — Leading/trailing characters detected
    // ==========================================

    [Theory]
    [InlineData(".feature/test")]
    [InlineData("feature/test.")]
    public void Validate_LeadingOrTrailingDot_ReturnsInvalid(string branchName)
    {
        var result = _validator.Validate(branchName);

        Assert.False(result.IsValid);
        Assert.Contains("dot", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/feature/test")]
    [InlineData("feature/test/")]
    public void Validate_LeadingOrTrailingSlash_ReturnsInvalid(string branchName)
    {
        var result = _validator.Validate(branchName);

        Assert.False(result.IsValid);
        Assert.Contains("slash", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ==========================================
    // 5.5 — @{ sequence detected
    // ==========================================

    [Fact]
    public void Validate_AtBraceSequence_ReturnsInvalid()
    {
        var result = _validator.Validate("feature/test@{1}");

        Assert.False(result.IsValid);
        Assert.Contains("@{", result.ErrorMessage);
        Assert.Contains('@', result.InvalidCharacters);
    }

    // ==========================================
    // Additional edge-case tests
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespace_ReturnsInvalid(string? branchName)
    {
        var result = _validator.Validate(branchName!);

        Assert.False(result.IsValid);
        Assert.Contains("empty", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ConsecutiveSlashes_ReturnsInvalid()
    {
        var result = _validator.Validate("feature//test");

        Assert.False(result.IsValid);
        Assert.Contains("consecutive slashes", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_LockSuffix_ReturnsInvalid()
    {
        var result = _validator.Validate("feature/test.lock");

        Assert.False(result.IsValid);
        Assert.Contains(".lock", result.ErrorMessage);
    }

    [Fact]
    public void Validate_MultipleInvalidCharacters_AllDetected()
    {
        // Contains space, ~, and ?
        var result = _validator.Validate("feature/my test~name?");

        Assert.False(result.IsValid);
        Assert.Contains(' ', result.InvalidCharacters);
        Assert.Contains('~', result.InvalidCharacters);
        Assert.Contains('?', result.InvalidCharacters);
        Assert.True(result.InvalidCharacters.Count >= 3);
    }

    [Fact]
    public void Validate_MultipleViolations_AllErrorsReported()
    {
        // Leading dot + consecutive dots + @{ sequence
        var result = _validator.Validate(".test..name@{1}");

        Assert.False(result.IsValid);
        Assert.True(result.ValidationErrors.Count >= 3);
    }

    [Fact]
    public void Validate_Valid_ReturnsEmptyCollections()
    {
        var result = _validator.Validate("feature/valid-name");

        Assert.True(result.IsValid);
        Assert.Empty(result.InvalidCharacters);
        Assert.Empty(result.ValidationErrors);
    }
}
