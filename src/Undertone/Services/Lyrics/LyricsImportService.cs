using System.Threading.Channels;
using Undertone.Models;

namespace Undertone.Services.Lyrics;

/// <summary>Searches lyrics for newly published library tracks in a background queue.</summary>
public sealed class LyricsImportService : IDisposable
{
    private readonly ILibraryService _library;
    private readonly ILyricsBulkService _lyrics;
    private readonly Func<AppSettings> _settings;
    private readonly IMetadataService? _metadata;
    private readonly SemaphoreSlim _fetchGate = new(1, 1);
    private readonly Channel<IReadOnlyList<Track>> _queue = Channel.CreateUnbounded<IReadOnlyList<Track>>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });
    private readonly CancellationTokenSource _shutdown = new();
    private Task _worker = Task.CompletedTask;
    private bool _attached;

    public LyricsImportService(ILibraryService library, ILyricsBulkService lyrics, Func<AppSettings> settings,
        IMetadataService? metadata = null)
    {
        _library = library;
        _lyrics = lyrics;
        _settings = settings;
        _metadata = metadata;
    }

    /// <summary>Creates sidecars at the selected source paths before import or relocation.</summary>
    public async Task PrepareAsync(IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        if (!_settings().LyricsFetchOnImport || _metadata == null) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdown.Token);
        await _fetchGate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            var tracks = await Task.Run(() =>
            {
                var found = new List<Track>();
                foreach (var path in DroppedFilesService.ExpandAudioFiles(paths))
                {
                    linked.Token.ThrowIfCancellationRequested();
                    try
                    {
                        if (_metadata.ReadTrackMetadata(path) is not { } track) continue;
                        track.Id = LibraryService.TrackIdForPath(path);
                        track.FilePath = path;
                        track.SourceType = SourceType.Local;
                        found.Add(track);
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Warn(DebugLogger.Category.Lyrics, "Import.ReadFailed", ex.Message);
                    }
                }
                return found;
            }, linked.Token).ConfigureAwait(false);
            if (tracks.Count > 0 && _settings().LyricsFetchOnImport)
                await _lyrics.FetchAsync(tracks,
                    new LyricsBulkOptions(Upgrade: true, SaveTarget: LyricsSaveTarget.Sidecar, PersistLibrary: false),
                    null, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            DebugLogger.Warn(DebugLogger.Category.Lyrics, "Import.PrepareFailed", ex.Message);
        }
        finally { _fetchGate.Release(); }
    }

    public void Attach()
    {
        if (_attached) return;
        _attached = true;
        _worker = Task.Run(ProcessAsync);
        _library.TracksAdded += OnTracksAdded;
    }

    private void OnTracksAdded(object? sender, IReadOnlyList<Track> tracks)
    {
        if (_settings().LyricsFetchOnImport && tracks.Count > 0)
            _queue.Writer.TryWrite(tracks.ToArray());
    }

    private async Task ProcessAsync()
    {
        try
        {
            await foreach (var tracks in _queue.Reader.ReadAllAsync(_shutdown.Token))
            {
                if (!_settings().LyricsFetchOnImport) continue;
                try
                {
                    await _fetchGate.WaitAsync(_shutdown.Token).ConfigureAwait(false);
                    try
                    {
                        await _lyrics.FetchAsync(tracks, new LyricsBulkOptions(Upgrade: true), null, _shutdown.Token)
                            .ConfigureAwait(false);
                    }
                    finally { _fetchGate.Release(); }
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    DebugLogger.Warn(DebugLogger.Category.Lyrics, "Import.FetchFailed", ex.Message);
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
    }

    public async Task StopAsync(TimeSpan timeout)
    {
        Dispose();
        try { await _worker.WaitAsync(timeout).ConfigureAwait(false); }
        catch (TimeoutException) { }
    }

    public void Dispose()
    {
        _library.TracksAdded -= OnTracksAdded;
        _queue.Writer.TryComplete();
        _shutdown.Cancel();
    }
}
