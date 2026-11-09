using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Undertone.Helpers;

namespace Undertone.Services;

public sealed record QBitDownloadItem
{
    [JsonPropertyName("hash")] public string Hash { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("progress")] public double Progress { get; init; }
    [JsonPropertyName("dlspeed")] public long DownloadSpeed { get; init; }
    [JsonPropertyName("state")] public string State { get; init; } = "";
    [JsonPropertyName("eta")] public long Eta { get; init; }
    [JsonPropertyName("category")] public string Category { get; init; } = "";
    public double Percent => Math.Clamp(Progress * 100, 0, 100);
    public string ProgressLabel => $"{Percent:0.0}% · {DownloadSpeed / 1048576d:0.00} MiB/s";
    public string StateLabel => State switch
    {
        "metaDL" or "forcedMetaDL" => "Finding torrent information",
        "downloading" or "forcedDL" => "Downloading",
        "stalledDL" => "Waiting for peers", "queuedDL" => "Queued",
        "stoppedDL" or "pausedDL" => "Paused",
        "stoppedUP" or "pausedUP" => "Completed · paused",
        "uploading" or "stalledUP" or "forcedUP" => "Completed · seeding",
        "checkingDL" or "checkingUP" or "checkingResumeData" => "Checking files",
        "error" => "Download error", "missingFiles" => "Files missing", "moving" => "Moving files",
        _ => State
    };
    public string EtaLabel => Progress >= 1 ? "Complete" : Eta < 0 || Eta >= 8640000 ? "Time remaining unknown"
        : $"About {TimeSpan.FromSeconds(Eta).TotalMinutes:0} min remaining";
}

/// <summary>Authenticated loopback-only qBittorrent v5 connection.</summary>
public sealed class QBitTorrentApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _username;
    private readonly string _password;
    private bool _authenticated;
    private bool _authenticationRejected;
    public QBitTorrentApiClient(string username, string password, HttpClient? http = null)
    {
        _username = username; _password = password;
        _http = http ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
        _http.BaseAddress = new Uri("http://127.0.0.1:18743/");
        _http.Timeout = TimeSpan.FromSeconds(5);
        _http.DefaultRequestHeaders.Referrer = _http.BaseAddress;
        _http.DefaultRequestHeaders.Add("Origin", "http://127.0.0.1:18743");
    }

    public static QBitTorrentApiClient LoadLocal()
    {
        if (!OperatingSystem.IsWindows()) throw new IOException("The local qBittorrent connection requires Windows.");
        var path = Path.Combine(AppPaths.DataRoot, "qbittorrent-connection.bin");
        if (!File.Exists(path)) throw new IOException("The qBittorrent connection has not been set up for this profile.");
        var text = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser));
        var credentials = JsonSerializer.Deserialize<Dictionary<string, string>>(text)!;
        return new(credentials["username"], credentials["password"]);
    }

    private async Task AuthenticateAsync(CancellationToken ct)
    {
        if (_authenticated) return;
        if (_authenticationRejected) throw new IOException("Reconnect qBittorrent to update the local credentials.");
        using var response = await _http.PostAsync("api/v2/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["username"] = _username, ["password"] = _password }), ct);
        response.EnsureSuccessStatusCode();
        if (response.StatusCode != HttpStatusCode.NoContent && (await response.Content.ReadAsStringAsync(ct)).Trim() != "Ok.")
        {
            _authenticationRejected = true;
            throw new IOException("qBittorrent rejected the local connection credentials.");
        }
        _authenticated = true;
    }

    public async Task<IReadOnlyList<QBitDownloadItem>> GetDownloadsAsync(CancellationToken ct)
    {
        await AuthenticateAsync(ct);
        using var response = await _http.GetAsync("api/v2/torrents/info", ct);
        if (response.StatusCode == HttpStatusCode.Forbidden) _authenticated = false;
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<List<QBitDownloadItem>>(await response.Content.ReadAsStringAsync(ct)) ?? [];
    }

    public async Task SetPausedAsync(string hash, bool paused, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(hash, "^([a-fA-F0-9]{40}|[a-fA-F0-9]{64})$")) throw new ArgumentException("Invalid torrent identifier.");
        await AuthenticateAsync(ct);
        using var response = await _http.PostAsync("api/v2/torrents/" + (paused ? "stop" : "start"),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["hashes"] = hash }), ct);
        if (response.StatusCode == HttpStatusCode.Forbidden) _authenticated = false;
        response.EnsureSuccessStatusCode();
    }
    public void Dispose() => _http.Dispose();
}
