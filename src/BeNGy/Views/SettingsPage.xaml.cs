using BeNGy.ViewModels;
using BeNGy.Services.Interfaces;
using BeNGy.Models;

namespace BeNGy.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    private readonly IWindowService? _windowService;

    public SettingsPage(SettingsViewModel viewModel, IWindowService? windowService = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _windowService = windowService;
        BindingContext = viewModel;
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        // Hide to tray; pop first so the next summon starts on the main form.
        await Navigation.PopAsync(animated: false);
        _windowService?.HideWindow();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
#if WINDOWS
        var mauiWindow = Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (mauiWindow != null)
        {
            mauiWindow.Content.KeyDown += OnWindowKeyDown;
        }
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
#if WINDOWS
        var mauiWindow = Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (mauiWindow != null)
        {
            mauiWindow.Content.KeyDown -= OnWindowKeyDown;
        }
#endif
    }

#if WINDOWS
    private void OnWindowKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (!_viewModel.IsCapturingHotkey)
            return;

        // Extract modifiers
        var mods = HotkeyModifiers.None;
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        if (state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mods |= HotkeyModifiers.Control;
        
        state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu); // Alt
        if (state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mods |= HotkeyModifiers.Alt;
        
        state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
        if (state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mods |= HotkeyModifiers.Shift;
        
        state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.LeftWindows);
        if (state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mods |= HotkeyModifiers.Win;
        state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.RightWindows);
        if (state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mods |= HotkeyModifiers.Win;

        // Ignore if it's JUST a modifier key
        if (e.Key is Windows.System.VirtualKey.Control or Windows.System.VirtualKey.Shift or 
            Windows.System.VirtualKey.Menu or Windows.System.VirtualKey.LeftWindows or Windows.System.VirtualKey.RightWindows)
        {
            return;
        }

        // Map key to HotkeyKey enum
        var key = (HotkeyKey)(int)e.Key;

        // Send to ViewModel
        if (_viewModel.HandleCaptureKeyDownCommand.CanExecute((mods, key)))
        {
            _viewModel.HandleCaptureKeyDownCommand.Execute((mods, key));
            e.Handled = true;
        }
    }
#endif

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        // No slide animation — the palette snaps between surfaces.
        await Navigation.PopAsync(animated: false);
    }

}
