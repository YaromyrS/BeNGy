namespace BeNGy.Views;

public partial class MainPage : ContentPage
{
	private readonly MainViewModel _viewModel;

	public MainPage(MainViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = viewModel;

		// Tab from Ticket → Title via Return key (NFR11)
		TicketEntry.Completed += (_, _) => TitleEntry.Focus();
	}

	private void OnSettingsClicked(object sender, EventArgs e)
	{
		(Application.Current as App)?.OpenSettings();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

		// Palette entrance: fade + rise + settle, PowerToys Run-style.
		this.Opacity = 0;
		this.TranslationY = 8;
		this.Scale = 0.98;
		_ = Task.WhenAll(
			this.FadeTo(1, 120, Easing.CubicOut),
			this.TranslateTo(0, 0, 120, Easing.CubicOut),
			this.ScaleTo(1, 120, Easing.CubicOut));

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
