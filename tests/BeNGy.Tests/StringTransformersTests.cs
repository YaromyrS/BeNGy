using BeNGy.Helpers;

namespace BeNGy.Tests;

/// <summary>
/// Tests for StringTransformers — string formatting helpers for branch name generation.
/// </summary>
public class StringTransformersTests
{
    // ── ToKebabCase ──────────────────────────────────────────────

    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("add user login", "add-user-login")]
    [InlineData("  spaced  out  ", "spaced-out")]
    [InlineData("under_score_case", "under-score-case")]
    [InlineData("camelCaseInput", "camel-case-input")]
    [InlineData("ALLCAPS", "allcaps")]
    [InlineData("multiple   spaces", "multiple-spaces")]
    public void ToKebabCase_ConvertsCorrectly(string input, string expected)
    {
        var result = StringTransformers.ToKebabCase(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToKebabCase_EmptyOrWhitespace_ReturnsEmpty(string? input)
    {
        var result = StringTransformers.ToKebabCase(input!);
        Assert.Equal(string.Empty, result);
    }

    // ── ToSnakeCase ──────────────────────────────────────────────

    [Theory]
    [InlineData("Hello World", "hello_world")]
    [InlineData("add user login", "add_user_login")]
    [InlineData("kebab-case-input", "kebab_case_input")]
    [InlineData("camelCaseInput", "camel_case_input")]
    [InlineData("  spaced  out  ", "spaced_out")]
    public void ToSnakeCase_ConvertsCorrectly(string input, string expected)
    {
        var result = StringTransformers.ToSnakeCase(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToSnakeCase_EmptyOrWhitespace_ReturnsEmpty(string? input)
    {
        var result = StringTransformers.ToSnakeCase(input!);
        Assert.Equal(string.Empty, result);
    }

    // ── ToPascalCase ─────────────────────────────────────────────

    [Theory]
    [InlineData("hello world", "HelloWorld")]
    [InlineData("add user login", "AddUserLogin")]
    [InlineData("kebab-case-input", "KebabCaseInput")]
    [InlineData("under_score_case", "UnderScoreCase")]
    [InlineData("  spaced  out  ", "SpacedOut")]
    public void ToPascalCase_ConvertsCorrectly(string input, string expected)
    {
        var result = StringTransformers.ToPascalCase(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToPascalCase_EmptyOrWhitespace_ReturnsEmpty(string? input)
    {
        var result = StringTransformers.ToPascalCase(input!);
        Assert.Equal(string.Empty, result);
    }
}
