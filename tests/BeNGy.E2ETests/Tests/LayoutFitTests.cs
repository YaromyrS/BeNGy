using BeNGy.E2ETests.Helpers;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Xunit.Abstractions;

namespace BeNGy.E2ETests.Tests;

/// <summary>
/// The main window is fixed at 440x480 and non-resizable (WindowService.cs), so
/// every control has to fit inside it. UI Automation can find and Invoke an element
/// that has been laid out past the bottom edge, so the functional tests pass happily
/// while a user sees a clipped window with no reachable Copy button. These tests
/// assert geometry, which is the part automation cannot fake.
/// </summary>
[Collection("BeNGy App")]
[Trait("Category", "E2E")]
public class LayoutFitTests
{
    private readonly AppTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public LayoutFitTests(AppTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _fixture.ResetForTest();
    }

    [Fact]
    public void MainWindow_AllPrimaryControls_FitInsideTheWindow()
    {
        _fixture.EnsureRunning();
        var window = _fixture.GetMainWindow();
        var windowBounds = window.BoundingRectangle;

        _output.WriteLine($"window: {windowBounds}");

        var ids = new[]
        {
            AutomationIds.PrefixPicker,
            AutomationIds.TicketEntry,
            AutomationIds.TitleEntry,
            AutomationIds.PreviewLabel,
            AutomationIds.CopyButton,
        };

        var overflowing = new List<string>();

        foreach (var id in ids)
        {
            var element = _fixture.WaitForElement(window, id);
            var bounds = element.BoundingRectangle;
            var fits = bounds.Bottom <= windowBounds.Bottom && bounds.Top >= windowBounds.Top;

            _output.WriteLine($"{id,-16} {bounds}  fits={fits}");

            if (!fits)
                overflowing.Add($"{id} (bottom={bounds.Bottom}, window bottom={windowBounds.Bottom}, overflow={bounds.Bottom - windowBounds.Bottom}px)");
        }

        Assert.True(
            overflowing.Count == 0,
            "Controls laid out beyond the window edge -- a user cannot see or click these:\n  "
            + string.Join("\n  ", overflowing));
    }

    /// <summary>
    /// The invalid state is the tallest the form ever gets: it adds the warning message,
    /// the invalid-character row, and the "fix validation issues" hint below the button.
    /// This is the state that overflowed a 580px window.
    /// </summary>
    [Fact]
    public void MainWindow_InvalidInput_WarningsAndButtonStillFit()
    {
        _fixture.EnsureRunning();
        var window = _fixture.GetMainWindow();

        var titleEntry = _fixture.WaitForElement(window, AutomationIds.TitleEntry);
        _fixture.TypeText(titleEntry, "bad?title");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

        var windowBounds = window.BoundingRectangle;
        var copyButton = _fixture.WaitForElement(window, AutomationIds.CopyButton);
        // Bottom-most element of the invalid state is the warning message inside
        // the InfoBar (its only text since the duplicate characters row went).
        var warning = _fixture.WaitForElement(window, AutomationIds.ValidationMessageLabel);

        _output.WriteLine($"window:     {windowBounds}");
        _output.WriteLine($"copyButton: {copyButton.BoundingRectangle}");
        _output.WriteLine($"warning:    {warning.BoundingRectangle} offscreen={warning.IsOffscreen}");

        Assert.True(
            copyButton.BoundingRectangle.Bottom <= windowBounds.Bottom,
            $"Copy button is clipped by {copyButton.BoundingRectangle.Bottom - windowBounds.Bottom}px in the invalid state.");

        Assert.False(warning.IsOffscreen, "The validation warning is not visible.");
        Assert.True(
            warning.BoundingRectangle.Bottom <= windowBounds.Bottom,
            $"InfoBar content is clipped by {warning.BoundingRectangle.Bottom - windowBounds.Bottom}px.");
    }

    [Fact]
    public void MainWindow_CopyButton_IsTheReachablePrimaryAction()
    {
        _fixture.EnsureRunning();
        var window = _fixture.GetMainWindow();

        var copyButton = _fixture.WaitForElement(window, AutomationIds.CopyButton);

        _output.WriteLine($"window:     {window.BoundingRectangle}");
        _output.WriteLine($"copyButton: {copyButton.BoundingRectangle}");
        _output.WriteLine($"offscreen:  {copyButton.IsOffscreen}");

        Assert.False(
            copyButton.IsOffscreen,
            "The Copy button is the primary action and reports IsOffscreen -- it cannot be clicked.");

        Assert.True(
            copyButton.BoundingRectangle.Bottom <= window.BoundingRectangle.Bottom,
            $"Copy button extends {copyButton.BoundingRectangle.Bottom - window.BoundingRectangle.Bottom}px past the bottom of a non-resizable window.");
    }
}
