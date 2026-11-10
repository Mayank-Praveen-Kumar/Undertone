namespace Undertone.Models;

public static class ListeningModeContext
{
    public static string CurrentId { get; set; } = "everyday";
}

public sealed class ListeningMode
{
    public string Id { get; set; } = "everyday";
    public string Name { get; set; } = "Everyday";
    public bool IsCustom { get; set; }
    public bool Grayscale { get; set; }
    public bool Initialized { get; set; }
    public string Theme { get; set; } = AppSettings.DefaultTheme;
    public string AccentHex { get; set; } = "#E74856";
    public string AccentName { get; set; } = "Crimson";
    public string Icon { get; set; } = AppSettings.DefaultAppIcon;
    public int Volume { get; set; } = 75;
    public bool Exclusive { get; set; }
    public bool FlowingLyrics { get; set; }
    public string FlowingStyle { get; set; } = FlowingStyles.Drift;
}

public sealed class ListeningModesState
{
    public string ActiveId { get; set; } = "everyday";
    public List<ListeningMode> Profiles { get; set; } = new();
}
