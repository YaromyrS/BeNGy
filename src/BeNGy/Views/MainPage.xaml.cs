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
					Dispatcher.Dispatch(() => TicketEntry.Focus());
			};
		}
	}

	private void UpdateSurface()
	{
		// Settings sits on top of this page in the navigation stack and owns
		// the window size while it is shown.
		if (!_isCurrentPage) return;

		if (ContentRoot.Height > 0)
			_windowService?.FitPaletteToContent(ContentRoot.Height);
		else
			_windowService?.SetPaletteSize(PaletteSurface.Main);   // before the first layout pass
	}

	private void OnSettingsClicked(object sender, EventArgs e)
	{
		(Application.Current as App)?.OpenSettings();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_isCurrentPage = true;

		// Palette entrance: fade + rise + settle, PowerToys Run-style.
		this.Opacity = 0;
		this.TranslationY = 8;
		this.Scale = 0.98;
		_ = Task.WhenAll(
			this.FadeTo(1, 120, Easing.CubicOut),
			this.TranslateTo(0, 0, 120, Easing.CubicOut),
			this.ScaleTo(1, 120, Easing.CubicOut));

		// Returning from Settings (or first show): re-apply anything settings
		// changed (separators, formats, prefix catalog), then pick the surface.
		_viewModel.RefreshFromPreferences();
		UpdateSurface();

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
#endif
}
