using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace Undertone.Services;

/// <summary>Only display colours and the selected icon id are shared with linked local apps.</summary>
internal static class CompanionAppearance
{
    internal static object Snapshot()
    {
        var app = Application.Current;
        string Colour(string key, string fallback)
        {
            if (app?.TryGetResource(key, app.ActualThemeVariant, out var value) == true)
            {
                var colour = value switch { ISolidColorBrush brush => brush.Color, Color color => color, _ => (Color?)null };
                if (colour is { } c) return c.A == 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}";
            }
            return fallback;
        }
        return new
        {
            isDark = app?.ActualThemeVariant != ThemeVariant.Light,
            iconVariant = AppIconService.CurrentId,
            colors = new
            {
                background = Colour("AppMainBackground", "#0D0A17"),
                sidebar = Colour("AppSidebarBackground", "#1A1028"),
                surface = Colour("SystemControlBackgroundListLowBrush", "#292335"),
                field = Colour("TextControlBackground", "#3A3247"),
                hover = Colour("SystemControlHighlightListLowBrush", "#493759"),
                foreground = Colour("SystemControlForegroundBaseHighBrush", "#F5F5F5"),
                muted = Colour("SystemControlForegroundBaseMediumBrush", "#B6ABCA"),
                border = Colour("MenuV2BorderBrush", "#FFFFFF17"),
                accent = Colour("AccentColorBrush", "#4338CA"),
                accentForeground = Colour("AccentForegroundBrush", "#FFFFFF"),
            },
        };
    }
}
