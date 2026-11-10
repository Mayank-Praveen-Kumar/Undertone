using System.Text.Json;
using Undertone.Models;
using Xunit;

namespace Undertone.Tests;

public class CustomThemePersistenceTests
{
    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var settings = new AppSettings
        {
            Theme = "Custom:abc123",
            CustomThemes = new List<CustomThemeDefinition>
            {
                new()
                {
                    Id = "abc123",
                    Name = "My Sunset",
                    BaseMode = "Dark",
                    MainBackgroundHex = "#1A0E14",
                    SidebarBackgroundHex = "#221319",
                    AccentHex = "#FF8547"
                }
            }
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<AppSettings>(json)!;

        Assert.Equal("Custom:abc123", restored.Theme);
        Assert.Single(restored.CustomThemes);
        var t = restored.CustomThemes[0];
        Assert.Equal("abc123", t.Id);
        Assert.Equal("My Sunset", t.Name);
        Assert.Equal("Dark", t.BaseMode);
        Assert.Equal("#1A0E14", t.MainBackgroundHex);
        Assert.Equal("#221319", t.SidebarBackgroundHex);
        Assert.Equal("#FF8547", t.AccentHex);
    }

    [Fact]
    public void EmptySettings_HasNoCustomThemes()
    {
        var settings = new AppSettings();
        Assert.Equal("Midnight", settings.Theme);
        Assert.NotNull(settings.CustomThemes);
        Assert.Empty(settings.CustomThemes);
    }

    [Fact]
    public void StaleCustomThemeReference_DeserializesCleanly()
    {
        var json = """
        {
            "Theme": "Custom:nonexistent-id",
            "CustomThemes": []
        }
        """;
        var restored = JsonSerializer.Deserialize<AppSettings>(json)!;
        Assert.Equal("Custom:nonexistent-id", restored.Theme);
        Assert.Empty(restored.CustomThemes);
        // The fallback-to-Cyberpunk happens at load time in SettingsViewModel;
        // this test only confirms the JSON shape stays valid.
    }
}
