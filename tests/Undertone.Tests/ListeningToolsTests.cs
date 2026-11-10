using System.Text.Json;
using Avalonia.Headless.XUnit;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Undertone.Helpers;
using Undertone.Models;
using Undertone.Services;
using Undertone.ViewModels;
using Undertone.Views;
using Xunit;

namespace Undertone.Tests;

public class ListeningToolsTests
{
    private sealed class NoOpHistory : IPlayHistoryService
    {
        public IReadOnlyList<PlayHistoryEvent> Events => Array.Empty<PlayHistoryEvent>();
        public Task PreloadAsync() => Task.CompletedTask;
        public void RecordPlay(Track track) { }
        public void RecordSkip(Track track) { }
        public Task FlushAsync() => Task.CompletedTask;
    }
    [AvaloniaFact]
    public async Task Settings_EmbedsHealthAndSessions_InTheRequestedTabs()
    {
        var app = Application.Current!;
        if (!app.Resources.TryGetResource("HeartFillIcon", null, out _))
        {
            app.Resources["InterSemiBold"] = FontFamily.Default;
            app.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Undertone/")) { Source = new Uri("avares://Undertone.UI/Assets/Icons.axaml") });
            app.Styles.Add(new StyleInclude(new Uri("avares://Undertone/")) { Source = new Uri("avares://Undertone.UI/Assets/Styles.axaml") });
        }
        using var persistence = new TestPersistenceService(); var library = new FakeLibraryService();
        var player = new PlayerViewModel(new FakeAudioPlayer(), library, persistence, new FakeAnimatedCoverService());
        var tools = new ListeningToolsViewModel(player, library, persistence); await tools.InitializeAsync();
        var settings = new SettingsViewModel(persistence, library, new NoOpHistory()) { ListeningTools = tools };
        var view = new SettingsView { DataContext = settings };
        var window = new Window { Width = 1000, Height = 700, Content = view };
        try
        {
            window.Show();
            settings.SelectedSettingsTab = SettingsViewModel.TabDiagnostics;
            Dispatcher.UIThread.RunJobs();
            Assert.True(settings.IsDiagnosticsTabVisible);
            Assert.Equal("Diagnostics", settings.SelectedTabTitle);
            Assert.Same(tools, Assert.Single(view.GetVisualDescendants().OfType<LibraryHealthView>()).DataContext);
            Assert.Same(tools, Assert.Single(view.GetVisualDescendants().OfType<UndoHistoryView>()).DataContext);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<ListeningSessionsView>(), c => c.IsEffectivelyVisible);
            settings.SelectedSettingsTab = SettingsViewModel.TabPlayer;
            Dispatcher.UIThread.RunJobs();
            Assert.False(settings.IsDiagnosticsTabVisible);
            Assert.Same(tools, Assert.Single(view.GetVisualDescendants().OfType<ListeningSessionsView>()).DataContext);
        }
        finally { window.Close(); }
    }
    [Fact]
    public async Task Undo_ConsecutiveDependentEdits_CanBothBeRestored()
    {
        var journal = new UndoJournal(); var value = 2;
        journal.Record("First edit", () => { value = 1; return Task.CompletedTask; }, () => value == 2);
        value = 3;
        journal.Record("Second edit", () => { value = 2; return Task.CompletedTask; }, () => value == 3);
        Assert.True(await journal.UndoAsync()); Assert.Equal(2, value);
        Assert.True(await journal.UndoAsync()); Assert.Equal(1, value);
        Assert.Empty(journal.Entries);
    }
    [Fact]
    public async Task Undo_FailureKeepsEntry_AndAllowsRetry()
    {
        var journal = new UndoJournal(); var attempts = 0;
        journal.Record("Retry me", () => ++attempts == 1 ? Task.FromException(new IOException("busy")) : Task.CompletedTask);
        await Assert.ThrowsAsync<IOException>(() => journal.UndoAsync());
        Assert.Single(journal.Entries); Assert.False(journal.IsBusy);
        Assert.True(await journal.UndoAsync()); Assert.Empty(journal.Entries);
    }
    [Fact]
    public async Task Undo_SkipsStaleActions_AndDoesNotRecordDuringReplay()
    {
        var journal = new UndoJournal(); var ran = false;
        journal.Record("Valid", () => { ran = true; journal.Record("recursive", () => Task.CompletedTask); return Task.CompletedTask; });
        journal.Record("Stale", () => throw new Exception("must not run"), () => false);
        Assert.True(await journal.UndoAsync()); Assert.True(ran); Assert.Empty(journal.Entries);
    }
    [Fact]
    public void Undo_HistoryIsBounded()
    {
        var journal = new UndoJournal();
        for (var i = 0; i < 100; i++) journal.Record(i.ToString(), () => Task.CompletedTask);
        Assert.Equal(20, journal.Entries.Count); Assert.Equal("99", journal.Entries[0].Label);
    }
    [Fact]
    public async Task Sessions_RoundTrip_PreservesQueueDuplicatesModesAndPosition()
    {
        using var persistence = new TestPersistenceService();
        var store = new ListeningToolsStore(persistence.DataDirectory);
        var id = Guid.NewGuid();
        await store.SaveAsync(new ListeningToolsState
        {
            LowPowerMode = true,
            Sessions = new() { new NamedListeningSession
            {
                Name = "Study", PositionSeconds = 81.5, Shuffle = true, Repeat = RepeatMode.All,
                Tracks = new() { new SessionTrack { Id = id, Path = "song.flac" }, new SessionTrack { Id = id, Path = "song.flac" } }
            } }
        });
        var state = await store.LoadAsync(); var session = Assert.Single(state.Sessions);
        Assert.True(state.LowPowerMode); Assert.Equal("Study", session.Name); Assert.Equal(81.5, session.PositionSeconds);
        Assert.True(session.Shuffle); Assert.Equal(RepeatMode.All, session.Repeat); Assert.Equal(2, session.Tracks.Count);
        Assert.Empty(Directory.GetFiles(persistence.DataDirectory, "*.tmp"));
    }
    [Fact]
    public async Task Sessions_CorruptFileIsLeftUntouched()
    {
        using var persistence = new TestPersistenceService();
        var path = Path.Combine(persistence.DataDirectory, "listening-tools.json");
        await File.WriteAllTextAsync(path, "{broken");
        await Assert.ThrowsAsync<JsonException>(() => new ListeningToolsStore(persistence.DataDirectory).LoadAsync());
        Assert.Equal("{broken", await File.ReadAllTextAsync(path));
    }
    [Fact]
    public async Task Sessions_InvalidPositionsAreRejected()
    {
        using var persistence = new TestPersistenceService();
        var store = new ListeningToolsStore(persistence.DataDirectory);
        await store.SaveAsync(new ListeningToolsState { Sessions = new() { new NamedListeningSession
        {
            Name = "Bad", PositionSeconds = -3, Tracks = new() { new SessionTrack { Id = Guid.NewGuid() } }
        } } });
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }
    [Fact]
    public void Health_MissingFileIsReported_WithoutTreatingStreamsAsMissing()
    {
        var local = new Track { Title = "Offline", FilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".flac") };
        var remote = new Track { Title = "Stream", FilePath = "https://example.invalid/song", SourceType = SourceType.Navidrome };
        var report = LibraryHealthScanner.Scan(new[] { local, remote }, CancellationToken.None);
        var issue = Assert.Single(report.Issues); Assert.Equal("Missing file", issue.Category); Assert.Same(local, issue.Track);
    }
    [Fact]
    public void Health_FindsCandidatesAndSidecars_WithoutChangingFiles()
    {
        using var persistence = new TestPersistenceService();
        var a = Path.Combine(persistence.DataDirectory, "a.mp3"); var b = Path.Combine(persistence.DataDirectory, "b.mp3");
        File.WriteAllText(a, "original-a"); File.WriteAllText(b, "original-b");
        File.WriteAllText(Path.ChangeExtension(a, ".lrc"), "[00:01]lyrics");
        var one = new Track { Title = "Song", Artist = "Artist", Album = "Album", FilePath = a, Duration = TimeSpan.FromSeconds(180) };
        var two = new Track { Title = "Song", Artist = "Artist", Album = "Album", FilePath = b, Duration = TimeSpan.FromSeconds(180) };
        var report = LibraryHealthScanner.Scan(new[] { one, two }, CancellationToken.None);
        Assert.Single(report.Issues.Where(i => i.Category == "Possible duplicate"));
        Assert.DoesNotContain(report.Issues, i => i.Category == "Missing lyrics" && i.Track == one);
        Assert.Contains(report.Issues, i => i.Category == "Missing lyrics" && i.Track == two);
        Assert.Equal("original-a", File.ReadAllText(a)); Assert.Equal("original-b", File.ReadAllText(b));
    }
    [Fact]
    public void Health_CancellationStopsTheCheck()
    {
        using var ct = new CancellationTokenSource(); ct.Cancel();
        Assert.Throws<OperationCanceledException>(() => LibraryHealthScanner.Scan(new[] { new Track() }, ct.Token));
    }
    [Fact]
    public void PowerMode_LimitsVisualUpdates_WithoutChangingNormalCadence()
    {
        try
        {
            ListeningPowerMode.Enabled = false; Assert.True(ListeningPowerMode.ShouldRender(1, 0));
            ListeningPowerMode.Enabled = true;
            var frames = 0; var last = 0.0;
            for (var i = 1; i <= 120; i++)
            {
                var now = i * 1000.0 / 60;
                if (ListeningPowerMode.ShouldRender(now, last)) { frames++; last = now; }
            }
            Assert.InRange(frames, 20, 30);
        }
        finally { ListeningPowerMode.Enabled = false; }
    }
    private static Track Song(string name) => new() { Id = Guid.NewGuid(), Title = name, Artist = "Test", FilePath = name + ".mp3", Duration = TimeSpan.FromMinutes(3) };
    [AvaloniaFact]
    public async Task QueueUndo_RestoresClearAndRemoval_WithoutRestartingPlayback()
    {
        UndoJournal.Shared.Entries.Clear();
        using var persistence = new TestPersistenceService(); var library = new FakeLibraryService(); var audio = new FakeAudioPlayer();
        var player = new PlayerViewModel(audio, library, persistence, new FakeAnimatedCoverService());
        var tracks = new[] { Song("a"), Song("b"), Song("c") };
        player.ReplaceQueueAndPlay(tracks, 0); var playing = player.CurrentTrack; var starts = audio.PlayedPaths.Count;
        player.RemoveFromQueue(0); player.ClearQueue();
        Assert.True(await UndoJournal.Shared.UndoAsync()); Assert.Equal(new[] { tracks[2] }, player.UpNext);
        Assert.True(await UndoJournal.Shared.UndoAsync()); Assert.Equal(tracks.Skip(1), player.UpNext);
        Assert.Same(playing, player.CurrentTrack); Assert.Equal(starts, audio.PlayedPaths.Count);
        UndoJournal.Shared.Entries.Clear();
    }
    [AvaloniaFact]
    public void NamedSession_ResumesAtSavedPosition_WithSavedQueueAndModes()
    {
        UndoJournal.Shared.Entries.Clear();
        using var persistence = new TestPersistenceService(); var library = new FakeLibraryService(); var audio = new FakeAudioPlayer();
        var player = new PlayerViewModel(audio, library, persistence, new FakeAnimatedCoverService());
        var tracks = new[] { Song("a"), Song("b"), Song("c") };
        player.ResumeNamedSession(tracks, TimeSpan.FromSeconds(42), true, RepeatMode.All);
        Assert.Same(tracks[0], player.CurrentTrack); Assert.Equal(tracks.Skip(1), player.UpNext);
        Assert.Equal(42000, audio.PendingSeekMs); Assert.True(player.IsShuffleEnabled); Assert.Equal(RepeatMode.All, player.RepeatMode);
        UndoJournal.Shared.Entries.Clear();
    }
    [AvaloniaFact]
    public async Task SavingSessionAndTogglingModeTogether_DoesNotLoseEitherChange()
    {
        UndoJournal.Shared.Entries.Clear();
        using var persistence = new TestPersistenceService(); var library = new FakeLibraryService();
        var player = new PlayerViewModel(new FakeAudioPlayer(), library, persistence, new FakeAnimatedCoverService());
        player.ReplaceQueueAndPlay(new[] { Song("a"), Song("b") }, 0);
        var tools = new ListeningToolsViewModel(player, library, persistence); await tools.InitializeAsync();
        tools.SessionName = "Focus"; var save = tools.SaveSessionCommand.ExecuteAsync(null); tools.IsLowPowerMode = true;
        await save; await tools.FlushAsync();
        var state = await new ListeningToolsStore(persistence.DataDirectory).LoadAsync();
        Assert.True(state.LowPowerMode); Assert.Equal("Focus", Assert.Single(state.Sessions).Name);
        tools.IsLowPowerMode = false; await tools.FlushAsync(); UndoJournal.Shared.Entries.Clear();
    }
    [AvaloniaFact]
    public async Task DeletingSessionCanBeUndone_AndDuplicateNamesAreNotOverwritten()
    {
        UndoJournal.Shared.Entries.Clear();
        using var persistence = new TestPersistenceService(); var library = new FakeLibraryService();
        var player = new PlayerViewModel(new FakeAudioPlayer(), library, persistence, new FakeAnimatedCoverService());
        player.AddToQueue(Song("a"));
        var tools = new ListeningToolsViewModel(player, library, persistence); await tools.InitializeAsync();
        tools.SessionName = "Study"; await tools.SaveSessionCommand.ExecuteAsync(null);
        tools.SessionName = "STUDY"; await tools.SaveSessionCommand.ExecuteAsync(null); Assert.Single(tools.Sessions);
        tools.SelectedSession = tools.Sessions[0]; await tools.DeleteSessionCommand.ExecuteAsync(null); Assert.Empty(tools.Sessions);
        Assert.True(await UndoJournal.Shared.UndoAsync()); Assert.Single(tools.Sessions);
        Assert.Single((await new ListeningToolsStore(persistence.DataDirectory).LoadAsync()).Sessions);
        UndoJournal.Shared.Entries.Clear();
    }
}
