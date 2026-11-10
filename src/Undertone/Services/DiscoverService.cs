using System.Globalization;
using System.Text.Json;
using Undertone.Models;

namespace Undertone.Services;

public sealed record DiscoverSong(string Title, string Artist, string Mbid = "", double Match = 0)
{
    public string Key => KeyFor(Artist, Title);
    public static string KeyFor(string artist, string title) => artist.Trim().ToLowerInvariant() + "::" + title.Trim().ToLowerInvariant();
}

public sealed class DiscoverPreferences
{
    public bool Enabled { get; set; }
    public double Adventure { get; set; } = 35;
    public HashSet<string> HiddenSongs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> ArtistFeedback { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Only seed titles/artists leave the device. Personal history and ranking stay local.</summary>
public sealed class DiscoverService
{
    private readonly HttpClient _http;
    private readonly string _root;
    private readonly string _apiKey;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private sealed record Cached(DateTime FetchedUtc, List<DiscoverSong> Songs);
    public DiscoverService(HttpClient http, string root, string? apiKey = null)
        => (_http, _root, _apiKey) = (http, Path.Combine(root, "discover"), apiKey ?? LastFmApi.ApiKey);

    private string PreferencesPath(string modeId) => modeId == "everyday" ? Path.Combine(_root, "preferences.json")
        : Path.Combine(_root, "preferences", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(modeId))) + ".json");
    public DiscoverPreferences LoadPreferences(string modeId = "everyday")
    {
        try
        {
            var value = JsonSerializer.Deserialize<DiscoverPreferences>(File.ReadAllText(PreferencesPath(modeId))) ?? new();
            value.HiddenSongs = new(value.HiddenSongs ?? [], StringComparer.OrdinalIgnoreCase);
            value.ArtistFeedback = new(value.ArtistFeedback ?? [], StringComparer.OrdinalIgnoreCase);
            value.Adventure = double.IsFinite(value.Adventure) ? Math.Clamp(value.Adventure, 0, 100) : 35;
            if (modeId != "everyday") value.Enabled = LoadPreferences().Enabled;
            return value;
        }
        catch { return new() { Enabled = modeId != "everyday" && LoadPreferences().Enabled }; }
    }

    public void SavePreferences(DiscoverPreferences value, string modeId = "everyday")
    {
        Directory.CreateDirectory(_root);
        var file = PreferencesPath(modeId);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(value));
        File.Move(file + ".tmp", file, true);
        if (modeId != "everyday") { var master = LoadPreferences(); master.Enabled = value.Enabled; SavePreferences(master); }
    }

    public async Task<(IReadOnlyList<DiscoverSong> Songs, bool Offline)> SimilarAsync(Track seed, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(DiscoverSong.KeyFor(seed.Artist, seed.Title))));
            var path = Path.Combine(_root, key + ".json");
            Cached? cached = null;
            try { cached = JsonSerializer.Deserialize<Cached>(await File.ReadAllTextAsync(path, ct)); } catch (IOException) { } catch (JsonException) { }
            if (cached is not null && DateTime.UtcNow - cached.FetchedUtc < TimeSpan.FromDays(7)) return (cached.Songs, false);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                var url = LastFmApi.ApiBase + "?method=track.getSimilar&format=json&autocorrect=1&limit=60&api_key="
                    + Uri.EscapeDataString(_apiKey) + "&artist=" + Uri.EscapeDataString(seed.Artist) + "&track=" + Uri.EscapeDataString(seed.Title);
                using var response = await _http.GetAsync(url, timeout.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                var songs = ParseSimilar(json).Where(s => s.Key != DiscoverSong.KeyFor(seed.Artist, seed.Title)).ToList();
                Directory.CreateDirectory(_root);
                // Cache failures must not discard a successful online result.
                try
                {
                    await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(new Cached(DateTime.UtcNow, songs)), ct);
                    File.Move(path + ".tmp", path, true);
                    foreach (var old in new DirectoryInfo(_root).GetFiles("*.json").Where(f => f.Name != "preferences.json")
                        .OrderByDescending(f => f.LastWriteTimeUtc).Skip(100)) old.Delete();
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
                return (songs, false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
            {
                if (cached is not null) return (cached.Songs, true);
                throw new InvalidOperationException("Last.fm could not supply recommendations. Check your connection and try again.", ex);
            }
        }
        finally { _gate.Release(); }
    }

    internal static IReadOnlyList<DiscoverSong> ParseSimilar(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out _)) throw new InvalidOperationException("Last.fm rejected the request.");
        if (!root.TryGetProperty("similartracks", out var similar) || !similar.TryGetProperty("track", out var tracks))
            throw new JsonException("Missing similar tracks.");
        var entries = tracks.ValueKind == JsonValueKind.Array ? tracks.EnumerateArray().ToArray()
            : tracks.ValueKind == JsonValueKind.Object ? new[] { tracks } : Array.Empty<JsonElement>();
        return entries.Select(t =>
        {
            var title = Text(t, "name");
            var artist = t.TryGetProperty("artist", out var a) ? a.ValueKind == JsonValueKind.String ? a.GetString() ?? "" : Text(a, "name") : "";
            double.TryParse(Text(t, "match"), NumberStyles.Float, CultureInfo.InvariantCulture, out var match);
            return new DiscoverSong(title, artist, Text(t, "mbid"), double.IsFinite(match) ? Math.Clamp(match, 0, 1) : 0);
        }).Where(s => s.Title.Length > 0 && s.Artist.Length > 0).DistinctBy(s => s.Key).Take(60).ToArray();
    }

    private static string Text(JsonElement e, string key) => e.TryGetProperty(key, out var v)
        && v.ValueKind is JsonValueKind.String or JsonValueKind.Number ? v.ToString().Trim() : "";

    internal static IReadOnlyList<(DiscoverSong Song, string Reason)> Rank(IEnumerable<DiscoverSong> candidates,
        IReadOnlyList<Track> library, IReadOnlyList<PlayHistoryEvent> history, DiscoverPreferences preferences, string seedTitle, bool useLibraryAffinity = true)
    {
        var artistScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in library.Where(t => useLibraryAffinity && (t.IsFavorite || t.Rating > 0)))
            artistScores[t.Artist] = artistScores.GetValueOrDefault(t.Artist) + (t.IsFavorite ? 2 : 0) + t.Rating / 5d;
        foreach (var e in history.TakeLast(2000))
        {
            var recency = Math.Exp(-Math.Max(0, (DateTime.UtcNow - e.PlayedAtUtc).TotalDays) / 30);
            artistScores[e.Artist] = artistScores.GetValueOrDefault(e.Artist) + (e.Skipped ? -0.75 : 0.25) * recency;
        }
        var blocked = library.Where(t => t.IsDisliked || t.IsSnoozed).Select(t => DiscoverSong.KeyFor(t.Artist, t.Title)).ToHashSet();
        var recent = history.TakeLast(30).Select(e => DiscoverSong.KeyFor(e.Artist, e.Title)).ToHashSet();
        var adventure = Math.Clamp(preferences.Adventure, 0, 100) / 100;
        var scored = candidates.DistinctBy(s => s.Key).Where(s => !preferences.HiddenSongs.Contains(s.Key) && !blocked.Contains(s.Key))
            .Select(s =>
            {
                var affinity = Math.Clamp(artistScores.GetValueOrDefault(s.Artist), -5, 10) / 10;
                var score = s.Match * 0.6 + affinity * (1 - adventure) * 0.5 + (affinity <= 0 ? adventure * 0.35 : 0)
                    + preferences.ArtistFeedback.GetValueOrDefault(s.Artist) * 0.15 - (recent.Contains(s.Key) ? 0.4 : 0);
                var reason = affinity > 0.2 ? $"Similar to {seedTitle} · an artist you return to" : $"Similar to {seedTitle}";
                return (Song: s, Reason: reason, Score: score);
            }).OrderByDescending(x => x.Score);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(DiscoverSong, string)>();
        foreach (var item in scored)
        {
            if (counts.GetValueOrDefault(item.Song.Artist) >= 2) continue;
            counts[item.Song.Artist] = counts.GetValueOrDefault(item.Song.Artist) + 1;
            result.Add((item.Song, item.Reason));
            if (result.Count == 24) break;
        }
        return result;
    }
}
