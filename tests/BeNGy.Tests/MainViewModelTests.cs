using BeNGy.Models;
using BeNGy.Services;
using BeNGy.Services.Interfaces;
using BeNGy.ViewModels;
using NSubstitute;

namespace BeNGy.Tests;

/// <summary>
/// Tests for MainViewModel — prefix options, default values, property-change notification,
/// branch name generation, real-time validation, clipboard copy, dismiss, and form reset.
/// </summary>
public class MainViewModelTests
{
    private readonly IBranchNameGenerator _generator;
    private readonly IGitValidator _validator;
    private readonly IClipboardService _clipboard;
    private readonly IWindowService _windowService;
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _generator = new BranchNameGenerator();
        _validator = new GitValidator();
        _clipboard = Substitute.For<IClipboardService>();
        _windowService = Substitute.For<IWindowService>();

        // Default: clipboard always succeeds
        _clipboard.CopyToClipboardAsync(Arg.Any<string>()).Returns(true);

        _viewModel = new MainViewModel(_generator, _validator, _clipboard, _windowService);
    }

    // ── PrefixOptions ────────────────────────────────────────────

    [Fact]
    public void PrefixOptions_ContainsExpectedItems()
    {
        var expected = new[] { "feature", "bugfix", "hotfix", "release", "task" };

        Assert.Equal(expected, _viewModel.PrefixOptions);
    }

    [Fact]
    public void PrefixOptions_MatchesDefaultCatalog()
    {
        Assert.Equal(5, _viewModel.PrefixOptions.Count);
    }

    [Fact]
    public void RefreshFromPreferences_RegeneratesPreviewWithNewSeparator()
    {
        // Simulates: user types, opens settings, changes a separator, returns.
        var prefs = NSubstitute.Substitute.For<BeNGy.Services.Interfaces.IPreferencesService>();
        prefs.Get(Arg.Any<string>(), Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(1));
        prefs.Get(Arg.Any<string>(), Arg.Any<int>()).Returns(ci => ci.ArgAt<int>(1));
        prefs.Get(Arg.Any<string>(), Arg.Any<bool>()).Returns(ci => ci.ArgAt<bool>(1));
        var vm = new MainViewModel(_generator, _validator, _clipboard, _windowService, prefs);

        vm.TicketNumber = "EXP-1";
        vm.Title = "do thing";
        var before = vm.GeneratedBranchName;
        Assert.Contains("/", before);   // default prefix/ticket separator

        // Settings changed the separator; nothing typed afterwards.
        prefs.Get(BeNGy.Models.PreferenceKeys.PrefixTicketSeparator, Arg.Any<string>()).Returns(".");
        vm.RefreshFromPreferences();

        Assert.Contains(".", vm.GeneratedBranchName);
        Assert.NotEqual(before, vm.GeneratedBranchName);
    }

    // ── Default values ───────────────────────────────────────────

    [Fact]
    public void Prefix_DefaultsToFeature()
    {
        Assert.Equal("feature", _viewModel.Prefix);
    }

    [Fact]
    public void TicketNumber_DefaultsToEmpty()
    {
        Assert.Equal(string.Empty, _viewModel.TicketNumber);
    }

    [Fact]
    public void Title_DefaultsToEmpty()
    {
        Assert.Equal(string.Empty, _viewModel.Title);
    }

    [Fact]
    public void GeneratedBranchName_DefaultsToEmpty()
    {
        Assert.Equal(string.Empty, _viewModel.GeneratedBranchName);
    }

    // ── Prefix change triggers preview update ────────────────────

    [Fact]
    public void ChangingPrefix_UpdatesGeneratedBranchName()
    {
        _viewModel.TicketNumber = "PROJ-123";

        _viewModel.Prefix = "bugfix";

        Assert.StartsWith("bugfix/", _viewModel.GeneratedBranchName);
    }

    [Fact]
    public void ChangingPrefix_RaisesPropertyChanged_ForGeneratedBranchName()
    {
        _viewModel.TicketNumber = "T-1"; // Need input so branch name is non-empty

        var raised = false;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.GeneratedBranchName))
                raised = true;
        };

        _viewModel.Prefix = "hotfix";

        Assert.True(raised);
    }

    // ── TicketNumber change triggers preview update ──────────────

    [Fact]
    public void ChangingTicketNumber_UpdatesGeneratedBranchName()
    {
        _viewModel.TicketNumber = "PROJ-456";

        Assert.Contains("PROJ-456", _viewModel.GeneratedBranchName);
    }

    [Fact]
    public void ChangingTicketNumber_RaisesPropertyChanged_ForGeneratedBranchName()
    {
        var raised = false;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.GeneratedBranchName))
                raised = true;
        };

        _viewModel.TicketNumber = "T-1";

        Assert.True(raised);
    }

    // ── Title change triggers preview update ─────────────────────

    [Fact]
    public void ChangingTitle_UpdatesGeneratedBranchName()
    {
        _viewModel.Title = "Add User Login";

        Assert.Contains("add-user-login", _viewModel.GeneratedBranchName);
    }

    [Fact]
    public void ChangingTitle_RaisesPropertyChanged_ForGeneratedBranchName()
    {
        var raised = false;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.GeneratedBranchName))
                raised = true;
        };

        _viewModel.Title = "Some Title";

        Assert.True(raised);
    }

    // ── Full branch name format ──────────────────────────────────

    [Fact]
    public void AllFields_ProducesCorrectBranchName()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ-123";
        _viewModel.Title = "Add User Login";

        Assert.Equal("feature/PROJ-123_add-user-login", _viewModel.GeneratedBranchName);
    }

    [Theory]
    [InlineData("feature")]
    [InlineData("bugfix")]
    [InlineData("hotfix")]
    [InlineData("release")]
    public void EachPrefix_ProducesCorrectFormat(string prefix)
    {
        _viewModel.Prefix = prefix;
        _viewModel.TicketNumber = "T-1";
        _viewModel.Title = "fix it";

        Assert.Equal($"{prefix}/T-1_fix-it", _viewModel.GeneratedBranchName);
    }

    // ── IsBusy from BaseViewModel ────────────────────────────────

    [Fact]
    public void IsBusy_DefaultsToFalse()
    {
        Assert.False(_viewModel.IsBusy);
    }

    [Fact]
    public void IsNotBusy_IsInverseOfIsBusy()
    {
        Assert.True(_viewModel.IsNotBusy);

        _viewModel.IsBusy = true;

        Assert.False(_viewModel.IsNotBusy);
    }

    [Fact]
    public void IsBusy_Change_RaisesPropertyChanged_ForIsNotBusy()
    {
        var raised = false;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BaseViewModel.IsNotBusy))
                raised = true;
        };

        _viewModel.IsBusy = true;

        Assert.True(raised);
    }

    // ── Validation — IsValid / ValidationMessage ────────────────

    [Fact]
    public void InitialState_IsValid_WhenNoInput()
    {
        Assert.True(_viewModel.IsValid);
    }

    [Fact]
    public void ValidInput_IsValid_ReturnsTrue()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ-123";
        _viewModel.Title = "add login flow";

        Assert.True(_viewModel.IsValid);
        Assert.Equal(string.Empty, _viewModel.ValidationMessage);
    }

    [Fact]
    public void InvalidInput_WithSpace_IsValid_ReturnsFalse()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ 123";

        Assert.False(_viewModel.IsValid);
        Assert.NotEmpty(_viewModel.ValidationMessage);
    }

    [Fact]
    public void InvalidInput_ValidationMessage_ContainsHelpfulText()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ?123";

        Assert.False(_viewModel.IsValid);
        Assert.NotEmpty(_viewModel.ValidationMessage);
        Assert.DoesNotContain("ERROR", _viewModel.ValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationStatus_RaisesPropertyChanged_ForIsValid()
    {
        _viewModel.TicketNumber = "VALID-123";

        var raised = false;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsValid))
                raised = true;
        };

        _viewModel.TicketNumber = "PROJ?123";

        Assert.True(raised);
    }

    [Fact]
    public void ValidationStatus_RaisesPropertyChanged_ForValidationMessage()
    {
        _viewModel.TicketNumber = "VALID-123";

        var raised = false;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ValidationMessage))
                raised = true;
        };

        _viewModel.TicketNumber = "PROJ?123";

        Assert.True(raised);
    }

    // ── Specific invalid character feedback ─────────────────────

    [Fact]
    public void HasInvalidCharacters_False_WhenValid()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ-123";

        Assert.False(_viewModel.HasInvalidCharacters);
        Assert.Equal(string.Empty, _viewModel.InvalidCharactersDisplay);
    }

    [Fact]
    public void HasInvalidCharacters_True_WhenInvalidCharPresent()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ?123";

        Assert.True(_viewModel.HasInvalidCharacters);
        Assert.NotEmpty(_viewModel.InvalidCharactersDisplay);
    }

    [Fact]
    public void InvalidCharactersDisplay_QuotesIndividualChars()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ?123";

        Assert.Contains("'?'", _viewModel.InvalidCharactersDisplay);
    }

    [Fact]
    public void InvalidCharactersDisplay_ShowsWordForSpace()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ 123";

        Assert.Contains("space", _viewModel.InvalidCharactersDisplay);
    }

    [Fact]
    public void InvalidCharactersDisplay_ListsAllInvalidChars()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PR?OJ*123";

        var display = _viewModel.InvalidCharactersDisplay;
        Assert.Contains("'?'", display);
        Assert.Contains("'*'", display);
    }

    [Fact]
    public void InvalidCharactersDisplay_ClearsWhenFixed()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ?123";
        Assert.NotEmpty(_viewModel.InvalidCharactersDisplay);

        _viewModel.TicketNumber = "PROJ-123";
        Assert.Equal(string.Empty, _viewModel.InvalidCharactersDisplay);
    }

    [Fact]
    public void ValidationMessage_UsesMockValidator_WhenInjected()
    {
        var mockValidator = Substitute.For<IGitValidator>();
        mockValidator.Validate(Arg.Any<string>())
            .Returns(ValidationResult.Invalid("Mock error", new[] { '!' }));

        var vm = new MainViewModel(_generator, mockValidator, _clipboard, _windowService);
        vm.Prefix = "feature";
        vm.TicketNumber = "T-1";

        Assert.False(vm.IsValid);
        Assert.Equal("Mock error", vm.ValidationMessage);
        Assert.Contains('!', vm.ValidationResult.InvalidCharacters);
    }

    // ── RefreshBranchName fires on each input change ───────────

    [Fact]
    public void SettingInputProperties_AutomaticallyTriggersValidation()
    {
        _viewModel.TicketNumber = "PROJ-123";
        Assert.True(_viewModel.IsValid);

        _viewModel.TicketNumber = "PROJ 123";
        Assert.False(_viewModel.IsValid);

        _viewModel.TicketNumber = "PROJ-123";
        Assert.True(_viewModel.IsValid);
    }

    // ── Story 4.1: CopyCommand + CopyButtonText + CopySuccess ───

    [Fact]
    public void CopyButtonText_DefaultsToCopy()
    {
        Assert.Equal("Copy", _viewModel.CopyButtonText);
    }

    [Fact]
    public void CopySuccess_DefaultsToFalse()
    {
        Assert.False(_viewModel.CopySuccess);
    }

    [Fact]
    public void CopyCommand_CanExecute_TrueWhenValidAndNonEmpty()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ-123";

        Assert.True(_viewModel.CopyCommand.CanExecute(null));
    }

    [Fact]
    public void CopyCommand_CanExecute_FalseWhenEmpty()
    {
        // No ticket or title → empty branch name
        Assert.False(_viewModel.CopyCommand.CanExecute(null));
    }

    [Fact]
    public void CopyCommand_CanExecute_FalseWhenInvalid()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ?123";

        Assert.False(_viewModel.CopyCommand.CanExecute(null));
    }

    [Fact]
    public async Task CopyCommand_Success_CallsClipboardService()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ-123";

        await _viewModel.CopyCommand.ExecuteAsync(null);

        await _clipboard.Received(1).CopyToClipboardAsync("feature/PROJ-123");
    }

    [Fact]
    public async Task CopyCommand_Success_HidesWindow()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ-123";

        await _viewModel.CopyCommand.ExecuteAsync(null);

        _windowService.Received(1).HideWindow();
    }

    [Fact]
    public async Task CopyCommand_Success_ClearsForm()
    {
        _viewModel.Prefix = "bugfix";
        _viewModel.TicketNumber = "PROJ-123";
        _viewModel.Title = "some title";

        await _viewModel.CopyCommand.ExecuteAsync(null);

        // Ticket and title cleared, prefix preserved
        Assert.Equal(string.Empty, _viewModel.TicketNumber);
        Assert.Equal(string.Empty, _viewModel.Title);
        Assert.Equal("bugfix", _viewModel.Prefix);
    }

    [Fact]
    public async Task CopyCommand_Success_ResetsButtonState()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ-123";

        await _viewModel.CopyCommand.ExecuteAsync(null);

        // After auto-close cycle, button text and success should be reset
        Assert.Equal("Copy", _viewModel.CopyButtonText);
        Assert.False(_viewModel.CopySuccess);
    }

    [Fact]
    public async Task CopyCommand_Failure_DoesNotHideWindow()
    {
        _clipboard.CopyToClipboardAsync(Arg.Any<string>()).Returns(false);

        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ-123";

        await _viewModel.CopyCommand.ExecuteAsync(null);

        _windowService.DidNotReceive().HideWindow();
    }

    [Fact]
    public async Task CopyCommand_Failure_DoesNotClearForm()
    {
        _clipboard.CopyToClipboardAsync(Arg.Any<string>()).Returns(false);

        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ-123";
        _viewModel.Title = "some title";

        await _viewModel.CopyCommand.ExecuteAsync(null);

        Assert.Equal("PROJ-123", _viewModel.TicketNumber);
        Assert.Equal("some title", _viewModel.Title);
    }

    [Fact]
    public async Task CopyCommand_Failure_ResetsButtonText()
    {
        _clipboard.CopyToClipboardAsync(Arg.Any<string>()).Returns(false);

        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ-123";

        await _viewModel.CopyCommand.ExecuteAsync(null);

        // After error delay, button text should reset to "Copy"
        Assert.Equal("Copy", _viewModel.CopyButtonText);
    }

    [Fact]
    public void CopyCommand_CanExecuteChanges_WhenValidationChanges()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ-123";
        Assert.True(_viewModel.CopyCommand.CanExecute(null));

        // Introduce invalid character
        _viewModel.TicketNumber = "PROJ?123";
        Assert.False(_viewModel.CopyCommand.CanExecute(null));

        // Fix it
        _viewModel.TicketNumber = "PROJ-123";
        Assert.True(_viewModel.CopyCommand.CanExecute(null));
    }

    // ── Story 4.4: DismissCommand ──────────────────────────────────

    [Fact]
    public void DismissCommand_HidesWindow()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ-123";

        _viewModel.DismissCommand.Execute(null);

        _windowService.Received(1).HideWindow();
    }

    [Fact]
    public void DismissCommand_ClearsFormButKeepsPrefix()
    {
        _viewModel.Prefix = "bugfix";
        _viewModel.TicketNumber = "PROJ-123";
        _viewModel.Title = "some title";

        _viewModel.DismissCommand.Execute(null);

        Assert.Equal(string.Empty, _viewModel.TicketNumber);
        Assert.Equal(string.Empty, _viewModel.Title);
        Assert.Equal("bugfix", _viewModel.Prefix);
    }

    [Fact]
    public void DismissCommand_DoesNotCopyToClipboard()
    {
        _viewModel.Prefix = "feature";
        _viewModel.TicketNumber = "PROJ-123";

        _viewModel.DismissCommand.Execute(null);

        _clipboard.DidNotReceive().CopyToClipboardAsync(Arg.Any<string>());
    }

    [Fact]
    public void DismissCommand_WorksEvenWhenInvalid()
    {
        _viewModel.TicketNumber = "PROJ?123";

        _viewModel.DismissCommand.Execute(null);

        _windowService.Received(1).HideWindow();
    }

    [Fact]
    public void DismissCommand_WorksWithEmptyForm()
    {
        _viewModel.DismissCommand.Execute(null);

        _windowService.Received(1).HideWindow();
    }
}
