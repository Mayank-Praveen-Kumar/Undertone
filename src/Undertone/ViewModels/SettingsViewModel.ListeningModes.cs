using Undertone.Models;

namespace Undertone.ViewModels;

public partial class SettingsViewModel
{
    internal ListeningMode CaptureListeningMode(string id, string name, bool custom, bool grayscale, int volume) => new()
    {
        Id = id, Name = name, IsCustom = custom, Grayscale = grayscale, Initialized = true,
        Theme = ResolveActiveThemeKey(), AccentHex = ActiveAccentHex, AccentName = ActiveAccentName,
        Icon = AppIcon, Volume = volume, Exclusive = ExclusiveAudioEnabled,
        FlowingLyrics = LyricsFlowingLightEnabled, FlowingStyle = LyricsFlowingStyle
    };
    internal void EnsureModeTheme(string id, string name, string main, string sidebar, string accent)
    {
        var definition = _settings.CustomThemes.FirstOrDefault(t => t.Id == id);
        if (definition is null)
        {
            definition = new CustomThemeDefinition { Id = id, Name = name, BaseMode = "Light", MainBackgroundHex = main, SidebarBackgroundHex = sidebar, AccentHex = accent };
            _settings.CustomThemes.Add(definition);
        }
        if (!CustomThemes.Any(t => t.Id == id)) CustomThemes.Add(MapDefToTile(definition));
    }
    internal async Task ApplyListeningModeAsync(ListeningMode mode)
    {
        _suspendSettingPersistence = true;
        try
        {
            if (mode.Theme.StartsWith("Custom:", StringComparison.Ordinal)) ApplyCustomTheme(mode.Theme[7..]);
            else if (mode.Theme.StartsWith(App.PackThemePrefix, StringComparison.Ordinal))
            {
                ActiveCustomThemeId = null; foreach (var tile in CustomThemes) tile.IsActive = false;
                RestorePackTheme(mode.Theme); ThemeChanged?.Invoke(this, ResolveActiveThemeKey());
            }
            else ApplyTheme(mode.Theme);
            ApplyAccent(mode.AccentHex, mode.AccentName);
            AppIcon = mode.Icon;
            ExclusiveAudioEnabled = mode.Exclusive && IsExclusiveAudioSupported;
            LyricsFlowingStyle = mode.FlowingStyle;
            LyricsFlowingLightEnabled = mode.FlowingLyrics;
        }
        finally { _suspendSettingPersistence = false; }
        await SaveAsync();
    }
}
