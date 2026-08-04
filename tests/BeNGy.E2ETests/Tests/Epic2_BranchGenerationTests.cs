using BeNGy.E2ETests.Helpers;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;

namespace BeNGy.E2ETests.Tests;

[Collection("BeNGy App")]
[Trait("Category", "E2E")]
public class Epic2_BranchGenerationTests
{
    private readonly AppTestFixture _fixture;

    public Epic2_BranchGenerationTests(AppTestFixture fixture)
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
    public void PrefixPicker_Loaded_ContainsExpectedPrefixes() // FR7
    {
        // Arrange
        var window = GetWindow();
        var picker = _fixture.WaitForElement(window, AutomationIds.PrefixPicker);
        var comboBox = picker.AsComboBox();

        // Act
        comboBox.Expand();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));
        var items = comboBox.Items.Select(i => i.Text.ToLowerInvariant()).ToList();
        comboBox.Collapse();

        // Assert
        Assert.Contains("feature", items);
        Assert.Contains("bugfix", items);
        Assert.Contains("hotfix", items);
        Assert.Contains("release", items);
    }

    [Fact]
    public void PrefixPicker_Default_IsFeature() // FR7
    {
        // Arrange
        var window = GetWindow();
        var picker = _fixture.WaitForElement(window, AutomationIds.PrefixPicker);
        var comboBox = picker.AsComboBox();

        // Assert
        Assert.Equal("feature", comboBox.SelectedItem?.Text?.ToLowerInvariant());
    }

    [Fact]
    public void PrefixPicker_SelectDifferentPrefix_SelectionChanges() // FR7
    {
        // Arrange
        var window = GetWindow();
        var picker = _fixture.WaitForElement(window, AutomationIds.PrefixPicker);
        var comboBox = picker.AsComboBox();

        // Act — select "bugfix"
        comboBox.Expand();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));
        var bugfixItem = comboBox.Items.FirstOrDefault(
            i => i.Text.Equals("bugfix", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(bugfixItem);
        bugfixItem.Click();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));

        // Assert
        Assert.Equal("bugfix", comboBox.SelectedItem?.Text?.ToLowerInvariant());
    }

    [Fact]
    public void TicketEntry_TypeText_AcceptsInput() // FR8
    {
        // Arrange
        var window = GetWindow();
        var ticketEntry = _fixture.WaitForElement(window, AutomationIds.TicketEntry);

        // Act
        _fixture.TypeText(ticketEntry, "PROJ-123");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));

        // Assert
        var textBox = ticketEntry.AsTextBox();
        Assert.Equal("PROJ-123", textBox.Text);
    }

    [Fact]
    public void TitleEntry_TypeText_AcceptsInput() // FR9
    {
        // Arrange
        var window = GetWindow();
        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);

        // Act
        _fixture.TypeText(titleEntry, "Add user login");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));

        // Assert
        var textBox = titleEntry.AsTextBox();
        Assert.Equal("Add user login", textBox.Text);
    }

    [Fact]
    public void Preview_TicketEntered_UpdatesInRealTime() // FR10
    {
        // Arrange
        var window = GetWindow();
        var ticketEntry = _fixture.WaitForElement(window, AutomationIds.TicketEntry);
        var previewLabel = _fixture.WaitForElement(window, AutomationIds.PreviewLabel);

        // Act
        _fixture.TypeText(ticketEntry, "PROJ-456");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert — preview should contain the ticket text
        var previewText = previewLabel.AsLabel().Text;
        Assert.Contains("PROJ-456", previewText);
    }

    [Fact]
    public void Preview_TitleEntered_ShowsKebabCasedTitle() // FR10, FR11
    {
        // Arrange
        var window = GetWindow();
        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);
        var previewLabel = _fixture.WaitForElement(window, AutomationIds.PreviewLabel);

        // Act
        _fixture.TypeText(titleEntry, "Add user login");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert — title should be kebab-cased in preview
        var previewText = previewLabel.AsLabel().Text;
        Assert.Contains("add-user-login", previewText);
    }

    [Fact]
    public void Preview_AllFieldsFilled_ShowsFullBranchFormat() // FR10, FR11
    {
        // Arrange
        var window = GetWindow();
        var prefixPicker = _fixture.WaitForElement(window, AutomationIds.PrefixPicker);
        var ticketEntry = _fixture.WaitForElement(window, AutomationIds.TicketEntry);
        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);
        var previewLabel = _fixture.WaitForElement(window, AutomationIds.PreviewLabel);

        // Ensure "feature" prefix is selected
        var comboBox = prefixPicker.AsComboBox();
        if (comboBox.SelectedItem?.Text?.ToLowerInvariant() != "feature")
        {
            comboBox.Select("feature");
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));
        }

        // Act
        _fixture.TypeText(ticketEntry, "PROJ-123");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));
        _fixture.TypeText(titleEntry, "Add user login");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert — full format: prefix/ticket_kebab-title
        var previewText = previewLabel.AsLabel().Text;
        Assert.Equal("feature/PROJ-123_add-user-login", previewText);
    }

    [Fact]
    public void Preview_PrefixChanged_UpdatesImmediately() // FR7, FR10
    {
        // Arrange — fill in ticket and title first
        var window = GetWindow();
        var ticketEntry = _fixture.WaitForElement(window, AutomationIds.TicketEntry);
        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);
        var prefixPicker = _fixture.WaitForElement(window, AutomationIds.PrefixPicker);
        var previewLabel = _fixture.WaitForElement(window, AutomationIds.PreviewLabel);

        _fixture.TypeText(ticketEntry, "PROJ-123");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));
        _fixture.TypeText(titleEntry, "Fix bug");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Act — change prefix to hotfix
        var comboBox = prefixPicker.AsComboBox();
        comboBox.Expand();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));
        var hotfixItem = comboBox.Items.FirstOrDefault(
            i => i.Text.Equals("hotfix", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(hotfixItem);
        hotfixItem.Click();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert — preview should now start with "hotfix/"
        var previewText = previewLabel.AsLabel().Text;
        Assert.StartsWith("hotfix/", previewText);
    }

    [Fact]
    public void TicketEntry_OnWindowOpen_HasFocus() // FR8
    {
        // Arrange — the window is already open
        var window = GetWindow();
        var ticketEntry = _fixture.WaitForElement(window, AutomationIds.TicketEntry);

        // Assert — ticket field should have focus on window open
        // Give the app a moment to set initial focus
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        var focusedElement = _fixture.Automation.FocusedElement();
        Assert.NotNull(focusedElement);
        Assert.Equal(AutomationIds.TicketEntry, focusedElement.AutomationId);
    }
}
