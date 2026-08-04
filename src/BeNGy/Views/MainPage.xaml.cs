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

		// Visual Polish (Story 7.2): simple fade-in transition
		this.Opacity = 0;
		this.FadeTo(1, 250, Easing.CubicIn);

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
