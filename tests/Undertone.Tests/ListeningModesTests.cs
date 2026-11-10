using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;
using Undertone.Models;
using Undertone.Services;
using Undertone.ViewModels;
using Xunit;

namespace Undertone.Tests;

public class ListeningModesTests
{
    private sealed class History : IPlayHistoryService
    {
        public IReadOnlyList<PlayHistoryEvent> Events => Array.Empty<PlayHistoryEvent>();
        public Task PreloadAsync() => Task.CompletedTask;
        public void RecordPlay(Track t) { }
        public void RecordSkip(Track t) { }
        public Task FlushAsync() => Task.CompletedTask;
    }
    private static async Task Select(ListeningModesViewModel modes, string id)
    {
        modes.SelectedMode = modes.Profiles.Single(p => p.Id == id);
        await modes.PendingSwitch;
        Assert.Equal("", modes.Status);
    }
    [AvaloniaFact]
    public async Task PresetsApplyAndSwitchingBackRestoresTheUsersChoices()
    {
        using var persistence = new TestPersistenceService();
        var library = new FakeLibraryService(); var history = new History(); var audio = new FakeAudioPlayer();
        var player = new PlayerViewModel(audio, library, persistence, new FakeAnimatedCoverService());
        var settings = new SettingsViewModel(persistence, library, history);
        settings.SetPlayer(player); settings.SetAudioPlayer(audio); await settings.LoadAsync();
        player.Volume = 42;
        var modes = new ListeningModesViewModel(settings, player, persistence.DataDirectory);
        try
        {
            await modes.InitializeAsync();
            Assert.Equal("everyday", modes.ActiveId);
            await Select(modes, "focus");
            Assert.True(FocusGrayscale.Enabled); Assert.True(settings.ExclusiveAudioEnabled);
            Assert.Equal("focus", ListeningModeContext.CurrentId);
            await Select(modes, "energy");
            Assert.False(FocusGrayscale.Enabled); Assert.False(settings.ExclusiveAudioEnabled);
            Assert.True(settings.LyricsFlowingLightEnabled); Assert.Equal(FlowingStyles.Kawarp, settings.LyricsFlowingStyle);
            Assert.Equal("listening-energy", settings.ActiveCustomThemeId);
            await Select(modes, "wind-down");
            Assert.False(settings.LyricsFlowingLightEnabled);
            Assert.Equal(75, player.Volume); Assert.Equal("listening-nature", settings.ActiveCustomThemeId);
            player.Volume = 61;
            await Select(modes, "everyday");
            Assert.Equal(42, player.Volume); Assert.True(settings.IsMidnightTheme); Assert.False(settings.ExclusiveAudioEnabled);
            await Select(modes, "wind-down"); Assert.Equal(61, player.Volume);
            await modes.FlushAsync();
        }
        finally { await modes.FlushAsync(); FocusGrayscale.SetEnabled(false); ListeningModeContext.CurrentId = "everyday"; }
    }
    [AvaloniaFact]
    public async Task CustomModesRememberTheirNamesAndChangesAcrossRestart()
    {
        using var persistence = new TestPersistenceService();
        var library = new FakeLibraryService(); var history = new History(); var player = new PlayerViewModel(new FakeAudioPlayer(), library, persistence, new FakeAnimatedCoverService());
        var settings = new SettingsViewModel(persistence, library, history); settings.SetPlayer(player); await settings.LoadAsync();
        var modes = new ListeningModesViewModel(settings, player, persistence.DataDirectory); await modes.InitializeAsync();
        modes.CustomName = "Late night"; modes.AddCustomCommand.Execute(null); await modes.PendingSwitch;
        var id = modes.ActiveId; player.Volume = 37; await modes.FlushAsync();
        var reloaded = new ListeningModesViewModel(settings, player, persistence.DataDirectory); await reloaded.InitializeAsync();
        Assert.Equal(id, reloaded.ActiveId); Assert.Equal("Late night", reloaded.SelectedMode!.Name);
        Assert.Equal(37, reloaded.SelectedMode.Volume);
        await reloaded.FlushAsync(); await modes.FlushAsync(); ListeningModeContext.CurrentId = "everyday";
    }
    [Fact]
    public void DiscoveryFeedbackIsIndependentForEachListeningMode()
    {
        var root = Path.Combine(Path.GetTempPath(), "undertone-mode-feedback-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var http = new HttpClient(); var service = new DiscoverService(http, root, "test");
            var everyday = new DiscoverPreferences { Enabled = true }; everyday.HiddenSongs.Add("A::B"); service.SavePreferences(everyday);
            var focus = service.LoadPreferences("focus"); Assert.True(focus.Enabled); Assert.Empty(focus.HiddenSongs);
            focus.HiddenSongs.Add("C::D"); focus.Adventure = 80; service.SavePreferences(focus, "focus");
            Assert.Contains("A::B", service.LoadPreferences().HiddenSongs); Assert.DoesNotContain("C::D", service.LoadPreferences().HiddenSongs);
            Assert.Equal(80, service.LoadPreferences("focus").Adventure); Assert.Empty(service.LoadPreferences("energy").HiddenSongs);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public async Task PlayHistoryRecordsTheModeWithoutReassigningOlderPlays()
    {
        var root = Path.Combine(Path.GetTempPath(), "undertone-mode-history-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var history = new PlayHistoryService(Path.Combine(root, "play_history.json"));
            ListeningModeContext.CurrentId = "focus";
            history.RecordPlay(new Track { Id = Guid.NewGuid(), Title = "Focus song", Artist = "A" });
            ListeningModeContext.CurrentId = "energy";
            history.RecordPlay(new Track { Id = Guid.NewGuid(), Title = "Energy song", Artist = "B" });
            await history.FlushAsync();
            Assert.Equal(new[] { "focus", "energy" }, history.Events.Select(e => e.ListeningModeId));
        }
        finally { ListeningModeContext.CurrentId = "everyday"; if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [AvaloniaFact]
    public async Task TopSwitcherListsAllModes_AndSurvivesEnablingGrayscale()
    {
        using var persistence = new TestPersistenceService();
        var library = new FakeLibraryService(); var history = new History();
        var player = new PlayerViewModel(new FakeAudioPlayer(), library, persistence, new FakeAnimatedCoverService());
        var settings = new SettingsViewModel(persistence, library, history); settings.SetPlayer(player); await settings.LoadAsync();
        var modes = new ListeningModesViewModel(settings, player, persistence.DataDirectory); await modes.InitializeAsync();
        var view = new Undertone.Views.ModeSwitcherView();
        var window = new Window { Width = 900, Height = 150, DataContext = modes, Content = view };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            Assert.Same(modes, view.DataContext);
            Assert.Equal(new[] { "Everyday", "Focus", "Energy", "Wind down", "Custom" }, modes.Profiles.Select(p => p.Name));
            await Select(modes, "focus");
            Assert.Same(modes, view.DataContext); Assert.Equal("focus", modes.ActiveId);
            await Select(modes, "everyday"); Assert.False(FocusGrayscale.Enabled);
        }
        finally { window.Close(); await modes.FlushAsync(); FocusGrayscale.SetEnabled(false); ListeningModeContext.CurrentId = "everyday"; }
    }
    [AvaloniaFact]
    public void FocusGrayscaleRendersArtworkWithoutFlatteningItsDetail()
    {
        if (!HeadlessTestApp.RealRendering) return;
        using var source = new SKBitmap(40, 40);
        using (var canvas = new SKCanvas(source)) { canvas.Clear(SKColors.Red); using var paint = new SKPaint { Color = SKColors.Blue }; canvas.DrawRect(20, 0, 20, 40, paint); }
        using var encoded = source.Encode(SKEncodedImageFormat.Png, 100); using var bitmap = new Bitmap(encoded.AsStream());
        var window = new Window { Width = 40, Height = 40, WindowDecorations = WindowDecorations.None, Content = new Image { Source = bitmap, Stretch = Stretch.Fill } };
        FocusGrayscale.Attach(); FocusGrayscale.SetEnabled(true);
        try
        {
            window.Show();
            for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Thread.Sleep(10); }
            using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
            using var stream = new MemoryStream(); frame.Save(stream); stream.Position = 0; using var result = SKBitmap.Decode(stream);
            var red = result.GetPixel(10, 20); var blue = result.GetPixel(30, 20);
            Assert.InRange(Math.Abs(red.Red - red.Green), 0, 2); Assert.InRange(Math.Abs(red.Red - red.Blue), 0, 2);
            Assert.InRange(Math.Abs(blue.Red - blue.Green), 0, 2); Assert.InRange(Math.Abs(blue.Red - blue.Blue), 0, 2);
            Assert.True(red.Red > blue.Red + 25, $"Artwork detail was lost: red={red}, blue={blue}");
        }
        finally { window.Close(); FocusGrayscale.SetEnabled(false); }
    }
}
