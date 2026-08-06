using Windows.UI.ViewManagement;

namespace BeNGy.Platforms.Windows;

/// <summary>
/// Reads the user's Windows accent color and injects it into the app's
/// resource dictionary. PowerToys never ships a brand color; neither do we.
/// Styles consume these via DynamicResource, so replacement propagates.
/// </summary>
public static class SystemAccent
{
    public static void Apply(ResourceDictionary resources)
    {
        try
        {
            var ui = new UISettings();
            var accent = ui.GetColorValue(UIColorType.Accent);
            var hover = ui.GetColorValue(UIColorType.AccentLight1);
            var pressed = ui.GetColorValue(UIColorType.AccentDark1);

            resources["Accent"] = ToMaui(accent);
            resources["AccentHover"] = ToMaui(hover);
            resources["AccentPressed"] = ToMaui(pressed);
        }
        catch
        {
            // Keep the XAML fallback values; a missing accent is cosmetic.
        }
    }

    private static Color ToMaui(global::Windows.UI.Color c) =>
        Color.FromRgba(c.R, c.G, c.B, c.A);
}
