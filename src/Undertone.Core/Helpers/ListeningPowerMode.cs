namespace Undertone.Helpers;

/// <summary>Runtime limits; appearance preferences are never rewritten.</summary>
public static class ListeningPowerMode
{
    private static volatile bool _enabled;
    public static bool Enabled { get => _enabled; set => _enabled = value; }
    public static bool ShouldRender(double nowMs, double previousMs) => !Enabled || nowMs - previousMs >= 1000.0 / 15;
}
