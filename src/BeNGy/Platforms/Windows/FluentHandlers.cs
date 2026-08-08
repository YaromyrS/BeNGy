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

        // Interactive things get a hand cursor, like everywhere else on Windows.
        Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping("Fluent", (handler, _) =>
        {
            SetHandCursor(handler.PlatformView);
        });
    }

    /// <summary>
    /// Sets the hand cursor on a WinUI element. UIElement.ProtectedCursor is
    /// protected; reflection is the accepted workaround until the API is public.
    /// </summary>
    public static void SetHandCursor(Microsoft.UI.Xaml.UIElement element)
    {
        try
        {
            typeof(Microsoft.UI.Xaml.UIElement)
                .GetProperty("ProtectedCursor",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .SetValue(element, Microsoft.UI.Input.InputSystemCursor.Create(
                    Microsoft.UI.Input.InputSystemCursorShape.Hand));
        }
        catch
        {
            // Cosmetic only — never let a cursor break the app.
        }
    }
}
