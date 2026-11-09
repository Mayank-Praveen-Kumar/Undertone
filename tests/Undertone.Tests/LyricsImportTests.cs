using Undertone.Models;
using Undertone.Services.Lyrics;
using Xunit;

namespace Undertone.Tests;

public class LyricsImportTests
{
    [Fact]
    public async Task ToggleAppliesToNewImportsAndAllowsLyricUpgrades()
    {
        var library = new FakeLibraryService();
        var settings = new AppSettings();
        var received = new TaskCompletionSource<Track>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var lyrics = new FakeLyrics(async (tracks, options, ct) =>
        {
            Interlocked.Increment(ref calls);
            Assert.True(options.Upgrade);
            received.SetResult(Assert.Single(tracks));
            await Task.CompletedTask;
        });
        using var service = new LyricsImportService(library, lyrics, () => settings);
        service.Attach();
        service.Attach(); // Reattaching must not duplicate imports.
        library.RaiseTracksAdded(new[] { new Track { Title = "disabled" } });
        settings.LyricsFetchOnImport = true;
        var added = new Track { Title = "new song" };
        library.RaiseTracksAdded(new[] { added });

        Assert.Same(added, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await service.StopAsync(TimeSpan.FromSeconds(5));
        library.RaiseTracksAdded(new[] { new Track { Title = "after shutdown" } });
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task FailedBatchDoesNotPreventTheNextImportFromBeingSearched()
    {
        var library = new FakeLibraryService();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var lyrics = new FakeLyrics((tracks, options, ct) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                throw new IOException("Search unavailable");
            finished.SetResult();
            return Task.CompletedTask;
        });
        using var service = new LyricsImportService(library, lyrics,
            () => new AppSettings { LyricsFetchOnImport = true });
        service.Attach();
        library.RaiseTracksAdded(new[] { new Track() });
        library.RaiseTracksAdded(new[] { new Track() });
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, calls);
    }

    private sealed class FakeLyrics(Func<IReadOnlyList<Track>, LyricsBulkOptions, CancellationToken, Task> fetch)
        : ILyricsBulkService
    {
        public Task<LyricsBulkSummary> FetchAsync(IReadOnlyList<Track> tracks, IProgress<LyricsBulkProgress>? progress, CancellationToken ct)
            => FetchAsync(tracks, new LyricsBulkOptions(), progress, ct);

        public async Task<LyricsBulkSummary> FetchAsync(IReadOnlyList<Track> tracks, LyricsBulkOptions options,
            IProgress<LyricsBulkProgress>? progress, CancellationToken ct)
        {
            await fetch(tracks, options, ct);
            return new LyricsBulkSummary(0, 0, 0, 0, 0);
        }

        public Task<int> RemoveAsync(IReadOnlyList<Track> tracks, IProgress<LyricsBulkProgress>? progress, CancellationToken ct)
            => Task.FromResult(0);
    }
}
