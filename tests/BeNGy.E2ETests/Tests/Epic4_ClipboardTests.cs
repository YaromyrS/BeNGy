using BeNGy.E2ETests.Helpers;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace BeNGy.E2ETests.Tests;

[Collection("BeNGy App")]
[Trait("Category", "E2E")]
public class Epic4_ClipboardTests
{
    private readonly AppTestFixture _fixture;

    public Epic4_ClipboardTests(AppTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureRunning();
    }

    private Window LaunchAndGetWindow()
    {
        _fixture.EnsureRunning();
        return _fixture.GetMainWindow();
    }

    private void FillValidBranchFields(Window window, string ticket = "PROJ-123", string title = "add user login")
    {
        var ticketEntry = _fixture.WaitForElement(window, AutomationIds.TicketEntry);
        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);

        _fixture.TypeText(ticketEntry, ticket);
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));
        _fixture.TypeText(titleEntry, title);
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void CopyButton_Clicked_CopiesBranchNameToClipboard() // FR14
    {
        // Arrange
        var window = LaunchAndGetWindow();
        FillValidBranchFields(window);

        var copyButton = _fixture.WaitForElement(window, AutomationIds.CopyButton);

        // Act
        copyButton.AsButton().Invoke();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert
        var clipboardText = _fixture.GetClipboardText();
        Assert.Contains("feature/PROJ-123", clipboardText);
        Assert.Contains("add-user-login", clipboardText);
    }

    [Fact]
    public void CopyButton_Default_IsPresentAndAnnouncesCopy() // FR14
    {
        // The copy action is now a glyph button inside the preview card; its
        // UIA name comes from SemanticProperties ("Copy branch name to
        // clipboard"), not from visible text.
        var window = LaunchAndGetWindow();

        var copyButton = _fixture.WaitForElement(window, AutomationIds.CopyButton);

        Assert.Contains("Copy", copyButton.AsButton().Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnterKey_InTitleField_TriggersCopy() // FR15
    {
        // Arrange
        var window = LaunchAndGetWindow();
        FillValidBranchFields(window);

        // Focus the title field
        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);
        titleEntry.Focus();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));

        // Act — press Enter
        Keyboard.Type(VirtualKeyShort.ENTER);
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert — clipboard should contain the branch name
        var clipboardText = _fixture.GetClipboardText();
        Assert.Contains("feature/PROJ-123", clipboardText);
        Assert.Contains("add-user-login", clipboardText);
    }

    [Fact]
    public void Copy_Successful_WindowAutoCloses() // FR16
    {
        // Arrange
        var window = LaunchAndGetWindow();
        FillValidBranchFields(window);

        var copyButton = _fixture.WaitForElement(window, AutomationIds.CopyButton);

        // Act
        copyButton.AsButton().Invoke();

        // Assert — window should auto-close (hide)
        var windowClosed = _fixture.WaitForWindowClose(TimeSpan.FromSeconds(5));
        Assert.True(windowClosed, "Window should auto-close after successful copy.");
    }

    [Fact]
    public void Copy_Successful_FormIsCleared() // FR16
    {
        // Arrange
        var window = LaunchAndGetWindow();
        FillValidBranchFields(window);

        var copyButton = _fixture.WaitForElement(window, AutomationIds.CopyButton);

        // Act — copy then re-open the window
        copyButton.AsButton().Invoke();
        _fixture.WaitForWindowClose(TimeSpan.FromSeconds(5));

        // Re-show the window (simulate tray open or hotkey)
        // Since the app is still running, we look for the tray icon to re-open
        var desktop = _fixture.Automation.GetDesktop();
        var notifyIcon = Retry.WhileNull(
            () =>
            {
                var candidates = desktop.FindAllDescendants(
                    _fixture.Automation.ConditionFactory.ByName("BeNGy"));
                return candidates.FirstOrDefault();
            },
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(500));

        if (notifyIcon.Result is not null)
        {
            notifyIcon.Result.DoubleClick();
            Wait.UntilInputIsProcessed(TimeSpan.FromSeconds(1));
        }

        // Get the re-opened window
        var reopenedWindow = _fixture.GetMainWindow(TimeSpan.FromSeconds(5));

        // Assert — ticket and title should be cleared
        var ticketEntry = _fixture.WaitForElement(reopenedWindow, AutomationIds.TicketEntry);
        var titleEntry = _fixture.WaitForElement(reopenedWindow, AutomationIds.TitleEntry);
        var prefixPicker = _fixture.WaitForElement(reopenedWindow, AutomationIds.PrefixPicker);

        Assert.Equal(string.Empty, ticketEntry.AsTextBox().Text);
        Assert.Equal(string.Empty, titleEntry.AsTextBox().Text);

        // Prefix should be preserved (still "feature")
        Assert.Equal("feature", prefixPicker.AsComboBox().SelectedItem?.Text?.ToLowerInvariant());
    }

    [Fact]
    public void CopyButton_BranchNameEmpty_IsDisabled() // FR14
    {
        // Arrange — launch with empty fields
        var window = LaunchAndGetWindow();

        // Act
        var copyButton = _fixture.WaitForElement(window, AutomationIds.CopyButton);

        // Assert — copy should be disabled when there's no branch name content
        Assert.False(copyButton.AsButton().IsEnabled,
            "Copy button should be disabled when branch name is empty.");
    }

    [Fact]
    public void CopyButton_ValidationFails_IsDisabled() // FR14, FR12
    {
        // Arrange
        var window = LaunchAndGetWindow();
        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);

        // Act — enter invalid input
        _fixture.TypeText(titleEntry, "bad?title");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        var copyButton = _fixture.WaitForElement(window, AutomationIds.CopyButton);

        // Assert
        Assert.False(copyButton.AsButton().IsEnabled,
            "Copy button should be disabled when validation fails.");
    }

    [Fact]
    public void EscapeKey_Pressed_DismissesWindowWithoutCopying() // FR15
    {
        // Arrange
        var window = LaunchAndGetWindow();
        FillValidBranchFields(window);

        // Clear clipboard first to verify nothing gets copied
        ClearClipboard();

        // Act — press Escape
        Keyboard.Type(VirtualKeyShort.ESCAPE);

        // Assert — window should close
        var windowClosed = _fixture.WaitForWindowClose(TimeSpan.FromSeconds(5));
        Assert.True(windowClosed, "Window should dismiss on Escape key.");
    }

    [Fact]
    public void EscapeKey_Pressed_DoesNotCopyToClipboard() // FR15
    {
        // Arrange
        var window = LaunchAndGetWindow();
        FillValidBranchFields(window);

        // Clear clipboard
        ClearClipboard();
        var clipboardBefore = _fixture.GetClipboardText();

        // Act — press Escape
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        _fixture.WaitForWindowClose(TimeSpan.FromSeconds(5));
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        // Assert — clipboard should not contain the branch name
        var clipboardAfter = _fixture.GetClipboardText();
        Assert.Equal(clipboardBefore, clipboardAfter);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    private static void ClearClipboard()
    {
        var thread = new Thread(() =>
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                EmptyClipboard();
                CloseClipboard();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
