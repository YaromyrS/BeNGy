using BeNGy.Models;
using BeNGy.Services.Interfaces;

namespace BeNGy.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly IBranchNameGenerator _generator;
    private readonly IGitValidator _validator;
    private readonly IClipboardService _clipboardService;
    private readonly IWindowService _windowService;
    private readonly IPreferencesService? _preferences;

    public MainViewModel(
        IBranchNameGenerator generator,
        IGitValidator validator,
        IClipboardService clipboardService,
        IWindowService windowService,
        IPreferencesService? preferences = null)
    {
        _generator = generator;
        _validator = validator;
        _clipboardService = clipboardService;
        _windowService = windowService;
        _preferences = preferences;

        // User-managed prefix catalog (settings can add/remove entries)
        PrefixOptions = new ObservableCollection<string>(Services.PrefixCatalog.Load(_preferences));

        // Story 6.2: Load default prefix from preferences
        _prefix = _preferences?.Get(PreferenceKeys.DefaultPrefix, PreferenceKeys.DefaultPrefixValue)
                  ?? PreferenceKeys.DefaultPrefixValue;
    }

    public ObservableCollection<string> PrefixOptions { get; }

    /// <summary>
    /// Re-reads everything settings can change — the prefix catalog and the
    /// formatting preferences — and regenerates the preview. Called when the
    /// main page reappears so a settings change is visible immediately, not
    /// only after the next keystroke.
    /// </summary>
    public void RefreshFromPreferences()
    {
        var catalog = Services.PrefixCatalog.Load(_preferences);
        var selected = Prefix;

        PrefixOptions.Clear();
        foreach (var p in catalog)
            PrefixOptions.Add(p);

        // Keep the user's selection when it still exists; otherwise fall back
        // to the (possibly reassigned) default.
        Prefix = PrefixOptions.Contains(selected)
            ? selected
            : _preferences?.Get(PreferenceKeys.DefaultPrefix, PreferenceKeys.DefaultPrefixValue)
              ?? PreferenceKeys.DefaultPrefixValue;

        RefreshBranchName();
    }

    // ── Input properties ───────────────────────────────────────────

    [ObservableProperty]
    private string _prefix = "feature";

    [ObservableProperty]
    private string _ticketNumber = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    // ── Branch name + validation ───────────────────────────────────

    [ObservableProperty]
    private string _generatedBranchName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private ValidationResult _validationResult = ValidationResult.Valid();

    /// <summary>True when the generated branch name passes all git validation rules.</summary>
    public bool IsValid => ValidationResult.IsValid;

    /// <summary>
    /// Human-readable summary of all validation errors, or empty when valid. This is
    /// the warning InfoBar's only text: it already lists offending characters
    /// (e.g. "Invalid characters: '?', space"), so no separate character row exists.
    /// </summary>
    public string ValidationMessage => ValidationResult.ErrorMessage;

    // ── Copy / clipboard (Story 4.1 + 4.3 + 6.7) ───────────────────

    [ObservableProperty]
    private bool _copySuccess;

    [ObservableProperty]
    private string _copyButtonText = "Copy";

    /// <summary>
    /// Copies the generated branch name to the clipboard, shows brief feedback,
    /// clears the form, then hides the window (auto-close).
    /// Delay is configurable via Story 6.7 (auto-close delay setting).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCopy), AllowConcurrentExecutions = false)]
    private async Task CopyAsync()
    {
        if (!CanCopy()) return;

        var success = await _clipboardService.CopyToClipboardAsync(GeneratedBranchName);

        if (success)
        {
            // Visual feedback
            CopySuccess = true;
            CopyButtonText = "Copied! ✓";

            // Story 6.7: Read configurable delay from preferences
            var delayMs = _preferences?.Get(PreferenceKeys.AutoCloseDelayMs, PreferenceKeys.AutoCloseDelayMsDefault)
                          ?? PreferenceKeys.AutoCloseDelayMsDefault;

            if (delayMs > 0)
            {
                await Task.Delay(delayMs);
            }

            // Reset form and hide (Story 4.3)
            ClearForm();
            _windowService.HideWindow();

            // Reset button state for next summon
            CopySuccess = false;
            CopyButtonText = "Copy";
        }
        else
        {
            await ShowCopyFailedAsync();
        }
    }

    /// <summary>
    /// Ctrl+C: copies and confirms like <see cref="CopyCommand"/>, but keeps the
    /// palette open with the form intact -- a stray Ctrl+C must not throw the
    /// input away.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCopy), AllowConcurrentExecutions = false)]
    private async Task CopyKeepOpenAsync()
    {
        if (!CanCopy()) return;

        if (!await _clipboardService.CopyToClipboardAsync(GeneratedBranchName))
        {
            await ShowCopyFailedAsync();
            return;
        }

        CopySuccess = true;
        CopyButtonText = "Copied! ✓";
        await Task.Delay(CopiedFeedbackMs);
        CopySuccess = false;
        CopyButtonText = "Copy";
    }

    internal const int CopiedFeedbackMs = 1200;

    private async Task ShowCopyFailedAsync()
    {
        // Copy failed — stay open, show feedback
        CopyButtonText = "Copy failed – try again";
        await Task.Delay(2000);
        CopyButtonText = "Copy";
    }

    private bool CanCopy() => IsValid && !string.IsNullOrEmpty(GeneratedBranchName);

    // Re-evaluate CanCopy whenever validation changes
    partial void OnValidationResultChanged(ValidationResult value)
    {
        CopyCommand.NotifyCanExecuteChanged();
        CopyKeepOpenCommand.NotifyCanExecuteChanged();
    }

    // ── Dismiss / Escape (Story 4.4) ────────────────────────────────

    /// <summary>
    /// Hides the window without copying. Clears ticket/title but preserves prefix.
    /// </summary>
    [RelayCommand]
    private void Dismiss()
    {
        ClearForm();
        _windowService.HideWindow();
    }

    // ── Form management ─────────────────────────────────────────────

    /// <summary>
    /// Resets ticket and title but keeps prefix (user preference).
    /// Branch name and validation update automatically via OnXxxChanged handlers.
    /// </summary>
    private void ClearForm()
    {
        TicketNumber = string.Empty;
        Title = string.Empty;
    }

    // ── Input change handlers — trigger branch name + validation refresh ──

    partial void OnPrefixChanged(string value) => RefreshBranchName();
    partial void OnTicketNumberChanged(string value) => RefreshBranchName();
    partial void OnTitleChanged(string value) => RefreshBranchName();

    /// <summary>
    /// Recomputes the branch name from current inputs and runs git validation.
    /// Called automatically when any input property changes via OnXxxChanged partial methods.
    /// Pure side-effect-free design: no state mutation inside property getters.
    /// </summary>
    private void RefreshBranchName()
    {
        var branchName = _generator.Generate(new BranchInput
        {
            Prefix = Prefix,
            TicketNumber = TicketNumber,
            Title = Title,
            TitleFormat = _preferences?.Get(PreferenceKeys.TitleFormat, PreferenceKeys.TitleFormatDefault)
                          ?? PreferenceKeys.TitleFormatDefault,
            PrefixTicketSeparator = _preferences?.Get(PreferenceKeys.PrefixTicketSeparator, PreferenceKeys.PrefixTicketSeparatorDefault)
                                    ?? PreferenceKeys.PrefixTicketSeparatorDefault,
            TicketTitleSeparator = _preferences?.Get(PreferenceKeys.TicketTitleSeparator, PreferenceKeys.TicketTitleSeparatorDefault)
                                   ?? PreferenceKeys.TicketTitleSeparatorDefault
        });

        GeneratedBranchName = branchName;

        ValidationResult = string.IsNullOrEmpty(branchName)
            ? ValidationResult.Valid()
            : _validator.Validate(branchName);
    }
}
