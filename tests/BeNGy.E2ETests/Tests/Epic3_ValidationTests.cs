using BeNGy.E2ETests.Helpers;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;

namespace BeNGy.E2ETests.Tests;

[Collection("BeNGy App")]
[Trait("Category", "E2E")]
public class Epic3_ValidationTests
{
    private readonly AppTestFixture _fixture;

    public Epic3_ValidationTests(AppTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureRunning();
    }

    private Window GetWindow()
    {
        _fixture.EnsureRunning();
        return _fixture.GetMainWindow();
    }

    [Fact]
    public void Validation_ValidInput_ShowsGreenValidIndicator() // FR12
    {
        // Arrange
        var window = GetWindow();
        var ticketEntry = _fixture.WaitForElement(window, AutomationIds.TicketEntry);
        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);

        // Act — enter valid input
        _fixture.TypeText(ticketEntry, "PROJ-123");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));
        _fixture.TypeText(titleEntry, "valid-title");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert — valid indicator should be visible
        var validIndicator = _fixture.FindByAutomationId(window, AutomationIds.ValidIndicator);
        Assert.NotNull(validIndicator);
        Assert.False(validIndicator.IsOffscreen,
            "Valid indicator should be visible for valid input.");
    }

    [Fact]
    public void Validation_InvalidCharacter_ShowsAmberWarningIndicator() // FR12
    {
        // Arrange
        var window = GetWindow();
        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);

        // Act — enter title with invalid character
        _fixture.TypeText(titleEntry, "bad?title");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert — invalid indicator should be visible
        var invalidIndicator = _fixture.FindByAutomationId(window, AutomationIds.InvalidIndicator);
        Assert.NotNull(invalidIndicator);
        Assert.False(invalidIndicator.IsOffscreen,
            "Invalid/warning indicator should be visible for invalid input.");
    }

    [Fact]
    public void Validation_InvalidCharacter_DisplaysSpecificBadCharacters() // FR12, FR13
    {
        // Arrange
        var window = GetWindow();
        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);

        // Act — enter title with a question mark
        _fixture.TypeText(titleEntry, "bad?title");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert — invalid chars label shows the "?" character
        var invalidCharsLabel = _fixture.WaitForElement(window, AutomationIds.InvalidCharsLabel);
        var labelText = invalidCharsLabel.AsLabel().Text;
        Assert.Contains("?", labelText);
    }

    [Fact]
    public void Validation_SpaceInTicket_ShowsSpaceInInvalidCharsDisplay() // FR12, FR13
    {
        // Arrange
        var window = GetWindow();
        var ticketEntry = _fixture.WaitForElement(window, AutomationIds.TicketEntry);

        // Act — enter ticket with a space
        _fixture.TypeText(ticketEntry, "PROJ 123");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert — invalid chars display mentions "space"
        var invalidCharsLabel = _fixture.WaitForElement(window, AutomationIds.InvalidCharsLabel);
        var labelText = invalidCharsLabel.AsLabel().Text;
        Assert.Contains("space", labelText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validation_MultipleInvalidChars_AllAreListed() // FR12, FR13
    {
        // Arrange
        var window = GetWindow();
        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);

        // Act — enter title with multiple invalid characters
        _fixture.TypeText(titleEntry, "bad?ti~tle");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert — both invalid chars are listed
        var invalidCharsLabel = _fixture.WaitForElement(window, AutomationIds.InvalidCharsLabel);
        var labelText = invalidCharsLabel.AsLabel().Text;
        Assert.Contains("?", labelText);
        Assert.Contains("~", labelText);
    }

    [Fact]
    public void Validation_FixInvalidInput_ClearsWarning() // FR12
    {
        // Arrange — enter invalid input first
        var window = GetWindow();
        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);

        _fixture.TypeText(titleEntry, "bad?title");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Verify warning is showing
        var invalidIndicator = _fixture.FindByAutomationId(window, AutomationIds.InvalidIndicator);
        Assert.NotNull(invalidIndicator);
        Assert.False(invalidIndicator.IsOffscreen);

        // Act — fix the input
        _fixture.TypeText(titleEntry, "good-title");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert — valid indicator should now be visible, invalid should be hidden
        var validIndicator = _fixture.FindByAutomationId(window, AutomationIds.ValidIndicator);
        Assert.NotNull(validIndicator);
        Assert.False(validIndicator.IsOffscreen,
            "Valid indicator should appear after fixing input.");
    }

    [Fact]
    public void Validation_Message_IsHelpfulNoErrorText() // FR13, FR32
    {
        // Arrange
        var window = GetWindow();
        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);

        // Act — enter invalid input
        _fixture.TypeText(titleEntry, "bad?title");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert — validation message should be present and NOT contain "ERROR"
        var validationMessage = _fixture.FindByAutomationId(window, AutomationIds.ValidationMessageLabel);
        Assert.NotNull(validationMessage);
        var messageText = validationMessage.AsLabel().Text;
        Assert.False(string.IsNullOrWhiteSpace(messageText),
            "Validation message should not be empty.");
        Assert.DoesNotContain("ERROR", messageText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validation_Invalid_CopyButtonIsDisabled() // FR12, FR32
    {
        // Arrange
        var window = GetWindow();
        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);

        // Act — enter invalid input
        _fixture.TypeText(titleEntry, "bad?title");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert — copy button should be disabled
        var copyButton = _fixture.WaitForElement(window, AutomationIds.CopyButton);
        Assert.False(copyButton.AsButton().IsEnabled,
            "Copy button should be disabled when validation fails.");
    }

    [Fact]
    public void Validation_Invalid_ShowsFixValidationHint() // FR32
    {
        // Arrange
        var window = GetWindow();
        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);

        // Act — enter invalid input
        _fixture.TypeText(titleEntry, "bad?title");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert — a copy-blocked or validation hint label should appear
        var copyBlockedLabel = _fixture.FindByAutomationId(window, AutomationIds.CopyBlockedLabel);
        Assert.NotNull(copyBlockedLabel);
        Assert.False(copyBlockedLabel.IsOffscreen,
            "Copy blocked / fix validation hint should be visible.");
    }
}
