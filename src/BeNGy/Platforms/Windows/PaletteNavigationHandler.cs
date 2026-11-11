using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml.Media.Animation;

namespace BeNGy.Platforms.Windows;

/// <summary>
/// NavigationPage handler whose page switches never animate. MAUI passes a NULL
/// transition to its WinUI Frame for Push/PopAsync(animated: false), and WinUI
/// reads null as "use the default navigation transition": a frame capture
/// (2026-11-11) showed the old page fading out, then ~150 ms of an empty
/// palette and a fade-in of the new one, ~300 ms per switch. An explicit
/// SuppressNavigationTransitionInfo is WinUI's documented "no animation".
/// </summary>
public class PaletteNavigationHandler : NavigationViewHandler
{
    protected override StackNavigationManager CreateNavigationManager() =>
        new NoAnimationNavigationManager(MauiContext ?? throw new InvalidOperationException("MauiContext not set"));

    private sealed class NoAnimationNavigationManager(IMauiContext context) : StackNavigationManager(context)
    {
        protected override NavigationTransitionInfo? GetNavigationTransition(NavigationRequest args) =>
            args.Animated ? base.GetNavigationTransition(args) : new SuppressNavigationTransitionInfo();
    }
}
