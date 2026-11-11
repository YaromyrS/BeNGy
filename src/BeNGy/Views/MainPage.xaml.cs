using BeNGy.Services.Interfaces;

namespace BeNGy.Views;

public partial class MainPage : ContentPage
{
	private readonly MainViewModel _viewModel;
	private readonly IWindowService? _windowService;
	private bool _isCurrentPage;

	public MainPage(MainViewModel viewModel, IWindowService? windowService = null)
	{
		InitializeComponent();
		_viewModel = viewModel;
		_windowService = windowService;
		BindingContext = viewModel;

		// Tab from Ticket → Title via Return key (NFR11)
		TicketEntry.Completed += (_, _) => TitleEntry.Focus();

		// The palette hugs its content: whenever the form's measured height
		// changes (validation warning appears, wraps to more lines, or goes
		// away), fit the window to it.
		ContentRoot.SizeChanged += (_, _) => UpdateSurface();

		// Every summon starts in the ticket field (NFR12). OnAppearing only covers
		// the page appearing; re-showing a hidden window let WinUI restore focus to
		// wherever it was, e.g. the title field after copy-and-auto-close, so the
		// next ticket number got typed into the title.
		if (_windowService is not null)
		{
			_windowService.Shown += (_, _) =>
			{
				if (_isCurrentPage)
					Dispatcher.Dispatch(() =>
					{
						TicketEntry.Focus();
						PlayEntrance();
					});
			};
		}
	}

	// True from appearing until this page has drawn: until then the window keeps
	// its size (see ApplySurfaceAfterRenderAsync).
	private bool _surfaceDeferred;

	private void UpdateSurface()
	{
		// Settings sits on top of this page in the navigation stack and owns
		// the window size while it is shown.
		if (!_isCurrentPage || _surfaceDeferred) return;

		if (ContentRoot.Height > 0)
			_windowService?.FitPaletteToContent(ContentRoot.Height);
		else
			_windowService?.SetPaletteSize(PaletteSurface.Main);   // before the first layout pass
	}

	/// <summary>
	/// Fits the window to this page once the page has drawn. Shrinking right away
	/// on return from Settings left ~150 ms of a clipped Settings page and then an
	/// empty palette before the form appeared (acrylic resize lag, see FrameAwaiter).
	/// </summary>
	private async Task ApplySurfaceAfterRenderAsync()
	{
		_surfaceDeferred = true;
#if WINDOWS
		await Platforms.Windows.FrameAwaiter.NextFramesAsync();
#else
		await Task.CompletedTask;
#endif
		_surfaceDeferred = false;
		UpdateSurface();
	}

	private bool _hasAppeared;

	/// <summary>Palette entrance: fade + rise + settle, PowerToys Run-style.</summary>
	private void PlayEntrance()
	{
		this.Opacity = 0;
		this.TranslationY = 8;
		this.Scale = 0.98;
		_ = Task.WhenAll(
			this.FadeTo(1, 120, Easing.CubicOut),
			this.TranslateTo(0, 0, 120, Easing.CubicOut),
			this.ScaleTo(1, 120, Easing.CubicOut));
	}

	private void OnSettingsClicked(object sender, EventArgs e)
	{
		(Application.Current as App)?.OpenSettings();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_isCurrentPage = true;

		// The entrance belongs to the palette appearing (first show here, later
		// summons via Shown), not to page switches: replaying it on every return
		// from Settings made going back feel slow.
		if (!_hasAppeared)
		{
			_hasAppeared = true;
			PlayEntrance();
		}

		// Returning from Settings (or first show): re-apply anything settings
		// changed (separators, formats, prefix catalog), then pick the surface.
		_viewModel.RefreshFromPreferences();
		_ = ApplySurfaceAfterRenderAsync();

#if WINDOWS
		// The preview is tap-to-copy — advertise it with a hand cursor.
		if (PreviewLabel.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement previewNative)
			Platforms.Windows.FluentHandlers.SetHandCursor(previewNative);
#endif

		// Auto-focus ticket field when window appears (NFR12)
		Dispatcher.Dispatch(() =>
		{
			TicketEntry.Focus();
		});

		// Register window-level Escape key handler (Story 4.4)
#if WINDOWS
		var mauiWindow = Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
		if (mauiWindow?.Content != null)
		{
			mauiWindow.Content.KeyDown += OnWindowKeyDown;
			mauiWindow.Content.PreviewKeyDown += OnWindowPreviewKeyDown;
		}
#endif
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_isCurrentPage = false;

		// Unregister Escape key handler to avoid leaks
#if WINDOWS
		var mauiWindow = Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
		if (mauiWindow?.Content != null)
		{
			mauiWindow.Content.KeyDown -= OnWindowKeyDown;
			mauiWindow.Content.PreviewKeyDown -= OnWindowPreviewKeyDown;
		}
#endif
	}

#if WINDOWS
	/// <summary>
	/// Window-level Escape key handler (Story 4.4).
	/// Dismisses without copying, regardless of which element has focus.
	/// </summary>
	private void OnWindowKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
	{
		if (e.Key == Windows.System.VirtualKey.Escape)
		{
			_viewModel.DismissCommand.Execute(null);
			e.Handled = true;
		}
	}

	/// <summary>
	/// Ctrl+C copies the branch name -- unless text is selected in a field, which
	/// keeps the ordinary copy. Unlike Enter or the Copy button it does not close
	/// the palette or clear the form: a Ctrl+C can be a slip. Preview (tunneling)
	/// because the focused TextBox would otherwise consume Ctrl+C before a
	/// bubbling KeyDown reaches the window.
	/// </summary>
	private void OnWindowPreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
	{
		if (e.Key != Windows.System.VirtualKey.C || !IsDown(Windows.System.VirtualKey.Control)
			|| IsDown(Windows.System.VirtualKey.Shift) || IsDown(Windows.System.VirtualKey.Menu))
			return;

		var root = (sender as Microsoft.UI.Xaml.UIElement)?.XamlRoot;
		if (root is not null
			&& Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root) is Microsoft.UI.Xaml.Controls.TextBox { SelectionLength: > 0 })
			return;

		if (_viewModel.CopyKeepOpenCommand.CanExecute(null))
		{
			_viewModel.CopyKeepOpenCommand.Execute(null);
			e.Handled = true;
		}
	}

	private static bool IsDown(Windows.System.VirtualKey key) =>
		Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key)
			.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
#endif
}
