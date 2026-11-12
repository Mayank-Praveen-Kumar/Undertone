using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Undertone.Models;
using Undertone.Services;
using Undertone.ViewModels;
using Xunit;

namespace Undertone.Tests;

public class SharedAppearanceTests
{
    private static void Styles()
    {
        var app = Application.Current!;
        if (!app.Resources.TryGetResource("HeartFillIcon", null, out _))
        {
            app.Resources["InterSemiBold"] = FontFamily.Default;
            app.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Undertone/")) { Source = new Uri("avares://Undertone.UI/Assets/Icons.axaml") });
            app.Styles.Add(new StyleInclude(new Uri("avares://Undertone/")) { Source = new Uri("avares://Undertone.UI/Assets/Styles.axaml") });
        }
    }

    [AvaloniaFact]
    public void LegacyButtonsAndFields_KeepPillCorners_AfterLocalChanges()
    {
        Styles();
        var button = new Button { Content = "Continue", CornerRadius = new(4), Width = 160, Height = 40 };
        var toggle = new ToggleButton { Content = "Selected", CornerRadius = new(6), Width = 160, Height = 40, IsChecked = true };
        var field = new TextBox { Text = "A rounded field", CornerRadius = new(3), Width = 220 };
        var combo = new ComboBox { ItemsSource = new[] { "An option", "Another option" }, SelectedIndex = 0, Width = 220 };
        var window = new Window { Width = 640, Height = 360, Content = new StackPanel { Margin = new(28), Spacing = 16, Children = { button, toggle, field, combo } } };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            Assert.Equal(new CornerRadius(999), button.CornerRadius);
            Assert.Equal(new CornerRadius(999), toggle.CornerRadius);
            Assert.Equal(new CornerRadius(999), field.CornerRadius);
            Assert.Equal(new CornerRadius(999), combo.CornerRadius);
            button.CornerRadius = new(0); toggle.CornerRadius = new(2);
            Assert.Equal(new CornerRadius(999), button.CornerRadius);
            Assert.Equal(new CornerRadius(999), toggle.CornerRadius);
            var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
            Assert.Equal(new CornerRadius(999), presenter.CornerRadius);
            if (HeadlessTestApp.RealRendering && Environment.GetEnvironmentVariable("UNDERTONE_SHARED_SHOT") is { Length: > 0 } screenshot)
            {
                for (var i=0;i<30;i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Thread.Sleep(5); }
                window.CaptureRenderedFrame()!.Save(screenshot);
            }
        }
        finally { window.Close(); }
    }

    private sealed class MemoryPersistence : TestPersistenceService
    {
        public AppSettings Stored { get; set; } = new();
        private static AppSettings Clone(AppSettings value) => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(value))!;
        public override Task<AppSettings> LoadSettingsAsync() => Task.FromResult(Clone(Stored));
        public override Task SaveSettingsAsync(AppSettings settings) { Stored=Clone(settings); return Task.CompletedTask; }
    }

    [AvaloniaFact]
    public async Task InterfaceSoundSetting_LoadsAndPersists_WithoutChangingPlaybackVolume()
    {
        using var persistence = new MemoryPersistence { Stored = new() { InterfaceSoundsEnabled = false, Volume = 37 } };
        var vm = new SettingsViewModel(persistence, new FakeLibraryService(), new FakeHistoryLog());
        await vm.LoadAsync();
        Assert.False(InterfaceSounds.Enabled);
        vm.InterfaceSoundsEnabled = true;
        await vm.SaveAsync();
        Assert.True(persistence.Stored.InterfaceSoundsEnabled);
        Assert.Equal(37, persistence.Stored.Volume);
        vm.InterfaceSoundsEnabled = false;
        await vm.SaveAsync();
        Assert.False(persistence.Stored.InterfaceSoundsEnabled);
        Assert.False(InterfaceSounds.Enabled);
        InterfaceSounds.Enabled = true;
    }

    [Fact]
    public void InterfaceSound_IsTheOriginalSpineShelfWave()
    {
        using var source = typeof(InterfaceSounds).Assembly.GetManifestResourceStream("Undertone.InterfaceClick.wav");
        Assert.NotNull(source);
        Assert.Equal("103408a083ecf405f68c3a0da996bf584e7d36702e7ae064ca7cb5579f66cd64", Convert.ToHexStringLower(SHA256.HashData(source)));
    }
}
