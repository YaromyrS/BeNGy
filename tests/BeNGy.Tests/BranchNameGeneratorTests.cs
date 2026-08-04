using BeNGy.Models;
using BeNGy.Services;
using BeNGy.Services.Interfaces;

namespace BeNGy.Tests;

/// <summary>
/// Tests for BranchNameGenerator — core branch name generation logic.
/// </summary>
public class BranchNameGeneratorTests
{
    private readonly IBranchNameGenerator _generator = new BranchNameGenerator();

    // ── Empty input ──────────────────────────────────────────────

    [Fact]
    public void Generate_NoTicketOrTitle_ReturnsEmpty()
    {
        var input = new BranchInput
        {
            Prefix = "feature",
            TicketNumber = "",
            Title = ""
        };

        var result = _generator.Generate(input);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Generate_WhitespaceOnly_ReturnsEmpty()
    {
        var input = new BranchInput
        {
            Prefix = "feature",
            TicketNumber = "   ",
            Title = "   "
        };

        var result = _generator.Generate(input);

        Assert.Equal(string.Empty, result);
    }

    // ── Ticket only ──────────────────────────────────────────────

    [Fact]
    public void Generate_TicketOnly_ReturnsPrefixAndTicket()
    {
        var input = new BranchInput
        {
            Prefix = "feature",
            TicketNumber = "PROJ-123",
            Title = ""
        };

        var result = _generator.Generate(input);

        Assert.Equal("feature/PROJ-123", result);
    }

    // ── Title only ───────────────────────────────────────────────

    [Fact]
    public void Generate_TitleOnly_ReturnsPrefixAndTitle()
    {
        var input = new BranchInput
        {
            Prefix = "feature",
            TicketNumber = "",
            Title = "Add User Login"
        };

        var result = _generator.Generate(input);

        Assert.Equal("feature_add-user-login", result);
    }

    // ── Full input ───────────────────────────────────────────────

    [Fact]
    public void Generate_FullInput_ReturnsPrefixTicketAndTitle()
    {
        var input = new BranchInput
        {
            Prefix = "feature",
            TicketNumber = "PROJ-123",
            Title = "Add User Login",
            TitleFormat = "kebab-case",
            PrefixTicketSeparator = "/",
            TicketTitleSeparator = "_"
        };

        var result = _generator.Generate(input);

        Assert.Equal("feature/PROJ-123_add-user-login", result);
    }

    // ── Different prefixes ───────────────────────────────────────

    [Theory]
    [InlineData("bugfix")]
    [InlineData("hotfix")]
    [InlineData("release")]
    [InlineData("chore")]
    public void Generate_DifferentPrefixes_UsesPrefix(string prefix)
    {
        var input = new BranchInput
        {
            Prefix = prefix,
            TicketNumber = "T-1",
            Title = "fix it"
        };

        var result = _generator.Generate(input);

        Assert.StartsWith($"{prefix}/", result);
    }

    // ── Title format variations ──────────────────────────────────

    [Fact]
    public void Generate_SnakeCaseFormat_TransformsTitleToSnakeCase()
    {
        var input = new BranchInput
        {
            Prefix = "feature",
            TicketNumber = "T-1",
            Title = "Add User Login",
            TitleFormat = "snake_case"
        };

        var result = _generator.Generate(input);

        Assert.Equal("feature/T-1_add_user_login", result);
    }

    [Fact]
    public void Generate_PascalCaseFormat_TransformsTitleToPascalCase()
    {
        var input = new BranchInput
        {
            Prefix = "feature",
            TicketNumber = "T-1",
            Title = "add user login",
            TitleFormat = "PascalCase"
        };

        var result = _generator.Generate(input);

        Assert.Equal("feature/T-1_AddUserLogin", result);
    }

    [Fact]
    public void Generate_UnknownFormat_DefaultsToKebabCase()
    {
        var input = new BranchInput
        {
            Prefix = "feature",
            TicketNumber = "T-1",
            Title = "Add User Login",
            TitleFormat = "unknown-format"
        };

        var result = _generator.Generate(input);

        Assert.Equal("feature/T-1_add-user-login", result);
    }

    // ── Custom separators ────────────────────────────────────────

    [Fact]
    public void Generate_CustomSeparators_UsesProvidedSeparators()
    {
        var input = new BranchInput
        {
            Prefix = "feature",
            TicketNumber = "T-1",
            Title = "my title",
            PrefixTicketSeparator = "-",
            TicketTitleSeparator = "-"
        };

        var result = _generator.Generate(input);

        Assert.Equal("feature-T-1-my-title", result);
    }

    // ── Defaults ─────────────────────────────────────────────────

    [Fact]
    public void BranchInput_Defaults_AreCorrect()
    {
        var input = new BranchInput();

        Assert.Equal("feature", input.Prefix);
        Assert.Equal(string.Empty, input.TicketNumber);
        Assert.Equal(string.Empty, input.Title);
        Assert.Equal("kebab-case", input.TitleFormat);
        Assert.Equal("/", input.PrefixTicketSeparator);
        Assert.Equal("_", input.TicketTitleSeparator);
    }
}
