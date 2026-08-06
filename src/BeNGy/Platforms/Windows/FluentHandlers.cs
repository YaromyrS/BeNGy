namespace BeNGy.Platforms.Windows;

/// <summary>
/// WinUI-level control adjustments that MAUI styles cannot express.
/// Scoped deliberately small (spec §8): anything that fights back gets
/// dropped to token-level styling rather than blocking the redesign.
/// </summary>
public static class FluentHandlers
{
    public static void Register()
    {
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("Fluent", (handler, _) =>
        {
            var box = handler.PlatformView;
            box.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(4);
            // Replace the accent bottom-underline focus style with an even border.
            box.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(1);
        });

        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("Fluent", (handler, _) =>
        {
            handler.PlatformView.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(4);
        });
    }
}
