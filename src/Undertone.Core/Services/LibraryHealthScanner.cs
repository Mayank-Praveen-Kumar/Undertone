using Undertone.Models;

namespace Undertone.Services;

public sealed record LibraryHealthIssue(string Category, string Title, string Detail, Track Track);
public sealed record LibraryHealthReport(IReadOnlyList<LibraryHealthIssue> Issues, string Summary);

/// <summary>Read-only, single-worker inspection. Duplicate matches are candidates, not audio hashes.</summary>
public static class LibraryHealthScanner
{
    public static LibraryHealthReport Scan(IReadOnlyList<Track> tracks, CancellationToken ct,
        IProgress<string>? progress = null)
    {
        var issues = new List<LibraryHealthIssue>();
        var counts = new Dictionary<string, int>();
        void Add(string category, Track track, string detail)
        {
            counts[category] = counts.GetValueOrDefault(category) + 1;
            if (issues.Count < 2000) issues.Add(new(category, track.Title, detail, track));
        }
        var seen = new Dictionary<string, Track>();
        for (var i = 0; i < tracks.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var t = tracks[i];
            if (i % 50 == 0) progress?.Report($"Checking {i + 1:N0} of {tracks.Count:N0} songs…");
            if (t.IsRemoteStream) continue;
            if (!File.Exists(t.FilePath)) { Add("Missing file", t, t.FilePath); continue; }
            if (string.IsNullOrWhiteSpace(t.Title) || string.IsNullOrWhiteSpace(t.Artist)
                || t.Artist.Equals("Unknown Artist", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(t.Album) || t.Album.Equals("Unknown Album", StringComparison.OrdinalIgnoreCase))
                Add("Incomplete tags", t, $"{t.Artist} · {t.Album} · {t.FilePath}");
            var art = t.AlbumArtworkPath;
            if (string.IsNullOrWhiteSpace(art) || (!Uri.TryCreate(art, UriKind.Absolute, out var uri) || uri.IsFile) && !File.Exists(art))
                Add("Missing artwork", t, $"{t.Artist} · {t.Album}");
            if (!t.NoLyrics && string.IsNullOrWhiteSpace(t.Lyrics) && string.IsNullOrWhiteSpace(t.SyncedLyrics)
                && !new[] { ".lrc", ".ttml", ".lyricsfile" }.Any(ext => File.Exists(Path.ChangeExtension(t.FilePath, ext))))
                Add("Missing lyrics", t, $"{t.Artist} · No cached, embedded or sidecar lyrics were found.");
            if (!string.IsNullOrWhiteSpace(t.Title) && !string.IsNullOrWhiteSpace(t.Artist)
                && !t.Artist.Equals("Unknown Artist", StringComparison.OrdinalIgnoreCase))
            {
                var key = $"{t.Artist.Trim().ToUpperInvariant()}\n{t.Title.Trim().ToUpperInvariant()}\n{Math.Round(t.Duration.TotalSeconds / 2)}";
                if (seen.TryGetValue(key, out var first) && !first.FilePath.Equals(t.FilePath, StringComparison.OrdinalIgnoreCase))
                    Add("Possible duplicate", t, $"Similar title, artist and duration to {first.FilePath}");
                else seen[key] = t;
            }
        }
        var summary = $"Checked {tracks.Count:N0} songs. " + (counts.Count == 0 ? "No issues found." :
            string.Join(" · ", counts.Select(c => $"{c.Key}: {c.Value:N0}")));
        if (counts.Values.Sum() > issues.Count) summary += " · Showing the first 2,000 results.";
        return new(issues, summary);
    }
}
