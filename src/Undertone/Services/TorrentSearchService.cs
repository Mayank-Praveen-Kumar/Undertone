using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Undertone.Services;

public sealed record TorrentSearchItem(string Name, string Source, string SourceUrl, string PageUrl,
    string MagnetLink, long? SizeBytes, long? Seeders)
{
    public string SizeDisplay => SizeBytes is not { } bytes ? "Size not listed"
        : bytes >= 1073741824 ? $"{bytes / 1073741824d:0.##} GiB"
        : bytes >= 1048576 ? $"{bytes / 1048576d:0.##} MiB" : $"{bytes:N0} bytes";
    public string SeedersDisplay => Seeders is { } count ? $"{count:N0} seeders (listed)" : "Seeders not listed";
}

public sealed record TorrentSearchResponse(IReadOnlyList<TorrentSearchItem> Items, IReadOnlyList<string> UnavailableSources);

/// <summary>Reads published torrent listings. Seeder figures are supplied by the index.</summary>
public sealed class TorrentSearchService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly HttpClient _http;
    public TorrentSearchService(HttpClient? http = null) => _http = http ?? Client;


    internal static string SearchUrl(string query)
    {
        var term = query.Trim();
        if (!term.Contains("FLAC", StringComparison.OrdinalIgnoreCase)) term += " FLAC";
        return "https://ext.to/browse/?cat=3&q=" + Uri.EscapeDataString(term);
    }

    public async Task<TorrentSearchResponse> SearchAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return new([], []);
        try
        {
            var html = await _http.GetStringAsync(SearchUrl(query), ct).ConfigureAwait(false);
            var items = Parse(html);
            // Detail pages are read only to obtain the selected listing's magnet.
            // Seeder counts remain the figures published on the search page.
            using var limit = new SemaphoreSlim(4);
            var resolved = await Task.WhenAll(items.Take(30).Select(async item =>
            {
                if (item.MagnetLink.Length > 0) return item;
                await limit.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var detail = await _http.GetStringAsync(item.PageUrl, ct).ConfigureAwait(false);
                    return item with { MagnetLink = Magnet(detail) };
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch { return item; }
                finally { limit.Release(); }
            })).ConfigureAwait(false);
            return new(resolved.OrderByDescending(i => i.Seeders ?? -1).ToArray(), []);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return new([], ["EXT.to (connection failed, verification required, or page format unavailable)"]); }
    }

    private static readonly RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.Singleline;
    private static MatchCollection Matches(string html, string pattern) =>
        Regex.Matches(html, pattern, Options, TimeSpan.FromSeconds(1));
    private static string Text(string html) => WebUtility.HtmlDecode(
        Regex.Replace(html, "<[^>]+>", " ", Options, TimeSpan.FromSeconds(1))).Trim();
    private static IEnumerable<(string Href, string Name)> Links(string html) =>
        Matches(html, "<a\\b[^>]*href\\s*=\\s*[\\\"'](?<url>[^\\\"']+)[\\\"'][^>]*>(?<name>.*?)</a>")
        .Select(m => (WebUtility.HtmlDecode(m.Groups["url"].Value), Text(m.Groups["name"].Value)));
    internal static string Magnet(string html) => Links(html)
        .Select(l => l.Href).FirstOrDefault(ValidMagnet) ?? string.Empty;
    private static bool ValidMagnet(string href)
    {
        if (!href.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase)) return false;
        try { return QBitTorrentService.ParseMagnets(href).Count > 0; }
        catch (ArgumentException) { return false; }
    }

    internal static IReadOnlyList<TorrentSearchItem> Parse(string html)
    {
        if (html.Contains("cf-chl-", StringComparison.OrdinalIgnoreCase)
            || html.Contains("security verification", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Just a moment", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("EXT.to requires browser verification.");
        var results = new List<TorrentSearchItem>();
        var recognizedTable = false;
        foreach (Match table in Matches(html, @"<table\b[^>]*>(.*?)</table>"))
        {
            var rows = Matches(table.Value, @"<tr\b[^>]*>(.*?)</tr>").ToArray();
            var headers = rows.Select(r => Matches(r.Value, @"<th\b[^>]*>(.*?)</th>")
                .Select(m => Text(m.Groups[1].Value)).ToArray()).FirstOrDefault(h => h.Length > 0);
            if (headers == null) continue;
            var sizeIndex = Array.FindIndex(headers, h => h.Contains("Size", StringComparison.OrdinalIgnoreCase));
            var seedIndex = Array.FindIndex(headers, h => Regex.IsMatch(h, @"^(Seeds?|Seeders?|S)$", RegexOptions.IgnoreCase));
            if (sizeIndex < 0) continue;
            recognizedTable = true;
            foreach (var row in rows)
            {
                var link = Links(row.Value).FirstOrDefault(l => l.Name.Contains("FLAC", StringComparison.OrdinalIgnoreCase)
                    && Uri.TryCreate(new Uri("https://ext.to"), l.Href, out var uri)
                    && uri.Scheme == "https" && uri.Host == "ext.to");
                if (string.IsNullOrEmpty(link.Href)) continue;
                var cells = Matches(row.Value, @"<td\b[^>]*>(.*?)</td>").Select(m => Text(m.Groups[1].Value)).ToArray();
                var size = sizeIndex < cells.Length ? ParseSize(cells[sizeIndex]) : null;
                long? seeds = seedIndex >= 0 && seedIndex < cells.Length
                    && long.TryParse(cells[seedIndex].Replace(",", ""), out var n) && n >= 0 ? n : null;
                results.Add(new(link.Name, "EXT.to", "https://ext.to", new Uri(new Uri("https://ext.to"), link.Href).AbsoluteUri,
                    Magnet(row.Value), size, seeds));
            }
        }
        if (!recognizedTable && !Regex.IsMatch(Text(html), @"no (torrents|results|matches) (found|were found)", RegexOptions.IgnoreCase))
            throw new FormatException("Unrecognized EXT.to results page.");
        return results.DistinctBy(i => i.PageUrl).ToArray();
    }

    internal static long? ParseSize(string text)
    {
        var match = Regex.Match(text, @"([\d.,]+)\s*(KiB|MiB|GiB|TiB|KB|MB|GB|TB|bytes|B)\b", RegexOptions.IgnoreCase);
        if (!match.Success || !double.TryParse(match.Groups[1].Value.Replace(",", ""), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var amount)) return null;
        var power = "KMGT".IndexOf(char.ToUpperInvariant(match.Groups[2].Value[0])) + 1;
        var bytes = amount * Math.Pow(1024, power);
        return bytes >= 0 && bytes <= long.MaxValue ? (long)bytes : null;
    }
}
