using System.Net;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using Undertone.Models;
using Undertone.Services;
using Undertone.Services.YouTube;
using Undertone.ViewModels;
using Undertone.Views;
using Xunit;

namespace Undertone.Tests;

public class DiscoverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "undertone-discover-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }
    private const string Similar = """{"similartracks":{"track":[{"name":"One","artist":{"name":"Artist"},"match":"0.9"},{"name":"One","artist":"Artist","match":"0.8"},{"name":"Two","artist":{"name":"Other"},"match":"0.6"},{"name":"Missing artist"}]}}""";
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls;
        public bool Offline;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Assert.DoesNotContain("session", request.RequestUri!.Query);
            Assert.DoesNotContain("username", request.RequestUri!.Query);
            if (Offline) throw new HttpRequestException("offline");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Similar) });
        }
    }
    [Fact]
    public void ParsesAndDeduplicates_AndDoesNotTurnProviderErrorsIntoEmptyResults()
    {
        var songs = DiscoverService.ParseSimilar(Similar);
        Assert.Equal(2, songs.Count);
        Assert.Equal("Artist", songs[0].Artist);
        Assert.Equal(0.9, songs[0].Match);
        Assert.Throws<InvalidOperationException>(() => DiscoverService.ParseSimilar("""{"error":10,"message":"Invalid API key"}"""));
    }
    [Fact]
    public async Task CachedCandidatesWorkOffline_WhileTheTasteProfileStaysLocal()
    {
        var handler = new Handler(); using var http = new HttpClient(handler);
        var service = new DiscoverService(http, _root, "test-key");
        var seed = new Track { Title = "Seed", Artist = "Seed artist" };
        var first = await service.SimilarAsync(seed, default);
        handler.Offline = true;
        Assert.Equal(2, (await service.SimilarAsync(seed, default)).Songs.Count);
        Assert.Equal(1, handler.Calls);
        var cache = Directory.GetFiles(Path.Combine(_root, "discover"), "*.json").Single();
        var data = File.ReadAllText(cache);
        using var json = JsonDocument.Parse(data);
        File.WriteAllText(cache, data.Replace(json.RootElement.GetProperty("FetchedUtc").GetString()!, DateTime.UtcNow.AddDays(-8).ToString("O")));
        var stale = await service.SimilarAsync(seed, default);
        Assert.True(stale.Offline); Assert.Equal(2, stale.Songs.Count);
        Assert.Equal(2, handler.Calls);
    }
    [Fact]
    public void RankingHonoursDislikesAndFeedback_AndLimitsArtistRepetition()
    {
        var preferences = new DiscoverPreferences();
        preferences.HiddenSongs.Add(DiscoverSong.KeyFor("A", "Hidden"));
        var candidates = new[] { new DiscoverSong("Hidden", "A", Match: 1), new("Blocked", "B", Match: 1),
            new("One", "A", Match: .9), new("Two", "A", Match: .8), new("Three", "A", Match: .7), new("Other", "C", Match: .6) };
        var ranked = DiscoverService.Rank(candidates, new[] { new Track { Artist = "B", Title = "Blocked", IsDisliked = true } }, [], preferences, "Seed");
        Assert.DoesNotContain(ranked, r => r.Song.Title is "Hidden" or "Blocked");
        Assert.Equal(2, ranked.Count(r => r.Song.Artist == "A"));
        preferences.ArtistFeedback["A"] = -4;
        Assert.Equal("C", DiscoverService.Rank(candidates, new[] { new Track { Artist = "B", Title = "Blocked", IsDisliked = true } }, [], preferences, "Seed")[0].Song.Artist);
    }
    [Fact]
    public void AdventureChangesRanking_WithoutUploadingHistory()
    {
        var candidates = new[] { new DiscoverSong("Known", "Familiar", Match: .7), new DiscoverSong("New", "Unknown", Match: .7) };
        var library = new[] { new Track { Artist = "Familiar", IsFavorite = true, Rating = 5 } };
        var prefs = new DiscoverPreferences { Adventure = 0 };
        Assert.Equal("Familiar", DiscoverService.Rank(candidates, library, [], prefs, "Seed")[0].Song.Artist);
        prefs.Adventure = 100;
        Assert.Equal("Unknown", DiscoverService.Rank(candidates, library, [], prefs, "Seed")[0].Song.Artist);
    }
    [Fact]
    public void PreferencesPersist_AndFeedbackCanBeReset()
    {
        using var http = new HttpClient(); var service = new DiscoverService(http, _root, "test");
        Assert.False(service.LoadPreferences().Enabled);
        var prefs = new DiscoverPreferences { Enabled = true, Adventure = 70 };
        prefs.HiddenSongs.Add("ARTIST::SONG"); service.SavePreferences(prefs);
        var loaded = service.LoadPreferences();
        Assert.True(loaded.Enabled); Assert.Equal(70, loaded.Adventure); Assert.Contains("artist::song", loaded.HiddenSongs);
        loaded.HiddenSongs.Clear(); service.SavePreferences(loaded);
        Assert.Empty(service.LoadPreferences().HiddenSongs);
    }
    [Theory]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=20", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com.evil.test/watch?v=dQw4w9WgXcQ", null)]
    [InlineData("https://youtube.com/watch?v=bad%22%3E", null)]
    [InlineData("file:///C:/anything", null)]
    public void PreviewAcceptsOnlyYouTubeVideoIds(string url, string? expected) => Assert.Equal(expected, YtDlpTool.PreviewVideoId(url));

    [Fact]
    public async Task PreviewSearchFetchesVideoIdsWithoutDownloadingAudio()
    {
        Directory.CreateDirectory(_root);
        var exe = Path.Combine(_root, "yt-dlp.exe"); File.WriteAllText(exe, "test fixture");
        using var http = new HttpClient();
        var tool = new YtDlpTool(http, _root, () => exe);
        tool.Runner = (_, args, _, ct) =>
        {
            Assert.Contains("--skip-download", args); Assert.Contains("--flat-playlist", args);
            Assert.StartsWith("ytsearch3:", args.Last());
            return Task.FromResult((0, """{"entries":[{"id":"M7lc1UVf-VE"}]}""", ""));
        };
        Assert.Equal("M7lc1UVf-VE", await tool.FindPreviewVideoAsync("Artist", "Song", default));
    }

    [AvaloniaFact]
    public void ClosingPreviewResumesOwnedPause_ButPreservesAnExistingPause()
    {
        using var persistence = new TestPersistenceService();
        var player = new PlayerViewModel(new FakeAudioPlayer(), new FakeLibraryService(), persistence, new FakeAnimatedCoverService());
        player.CurrentTrack = new Track { Id = Guid.NewGuid(), Title = "Track" };
        player.State = PlaybackState.Playing;
        using (var lease = new PreviewPlaybackLease(player)) Assert.Equal(PlaybackState.Paused, player.State);
        Assert.Equal(PlaybackState.Playing, player.State);
        player.State = PlaybackState.Paused;
        using (var lease = new PreviewPlaybackLease(player)) { }
        Assert.Equal(PlaybackState.Paused, player.State);
    }
    [AvaloniaFact]
    public void ChangingTracksDuringPreviewDoesNotResumeTheOldTrack()
    {
        using var persistence = new TestPersistenceService();
        var player = new PlayerViewModel(new FakeAudioPlayer(), new FakeLibraryService(), persistence, new FakeAnimatedCoverService());
        player.CurrentTrack = new Track { Id = Guid.NewGuid() }; player.State = PlaybackState.Playing;
        using (var lease = new PreviewPlaybackLease(player)) player.CurrentTrack = new Track { Id = Guid.NewGuid() };
        Assert.Equal(PlaybackState.Paused, player.State);
    }
    [AvaloniaFact]
    public void DiscoverViewLoadsWithoutStartingNetworkOrBrowser()
    {
        var view = new DiscoverView();
        Assert.NotNull(view);
    }
}
