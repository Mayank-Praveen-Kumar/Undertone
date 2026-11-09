using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Undertone.Services;
using Undertone.Services.MetadataSearch;

namespace Undertone.ViewModels;

public partial class TorrentDownloadViewModel : ViewModelBase, IDisposable
{
    private readonly IMetadataSearchService? _metadata;
    private readonly CancellationTokenSource _closed = new();
    private readonly TorrentSearchService _torrentSearch = new();
    private CancellationTokenSource? _torrentRequest;
    private QBitTorrentApiClient? _qbitApi;
    private Task? _monitorTask;
    private readonly HashSet<string> _submittedHashes = new(StringComparer.OrdinalIgnoreCase);
    [ObservableProperty] private bool _downloadPanelVisible;
    [ObservableProperty] private string _downloadPanelStatus = "Connecting to qBittorrent…";
    public ObservableCollection<QBitDownloadItem> Downloads { get; } = new();
    [ObservableProperty] private string _query;
    [ObservableProperty] private string _magnetLinks = string.Empty;
    [ObservableProperty] private string _saveFolder;
    [ObservableProperty] private string _executable = QBitTorrentService.FindExecutable();
    [ObservableProperty] private string _status = "Find the album, then paste magnet links or choose torrent files to download.";
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _isSearchingTorrents;
    [ObservableProperty] private string _torrentStatus = string.Empty;
    [ObservableProperty] private MetadataCandidate? _selectedAlbum;
    public ObservableCollection<MetadataCandidate> Albums { get; } = new();
    public ObservableCollection<TorrentSearchItem> Torrents { get; } = new();
    public ObservableCollection<string> TorrentFiles { get; } = new();
    public string TorrentFileSummary => TorrentFiles.Count == 0 ? "No torrent files selected" : $"{TorrentFiles.Count} torrent file(s) selected";

    public TorrentDownloadViewModel(string query, string saveFolder, IMetadataSearchService? metadata)
    {
        _query = query;
        _saveFolder = saveFolder;
        _metadata = metadata;
        TorrentFiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TorrentFileSummary));
    }

    [RelayCommand]
    private async Task FindAlbums()
    {
        Albums.Clear();
        SelectedAlbum = null;
        SearchTorrents();
        if (!string.IsNullOrWhiteSpace(Query)) await FindTorrents();
    }

    public static string TorrentSearchUrl(string query, MetadataCandidate? album)
    {
        var text = TorrentQuery(query, album);
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Enter a song, album or artist first.");
        return TorrentSearchService.SearchUrl(text);
    }

    internal static string TorrentQuery(string query, MetadataCandidate? album) => album == null ? query.Trim()
        : $"{album.Artist} {Regex.Replace(album.Album, @"\s*[-–—]\s*(Single|EP)\s*$", string.Empty, RegexOptions.IgnoreCase)}".Trim();

    partial void OnQueryChanged(string value)
    {
        _torrentRequest?.Cancel();
        Torrents.Clear();
        TorrentStatus = string.Empty;
        SelectedAlbum = null;
        Albums.Clear();
    }

    partial void OnSelectedAlbumChanged(MetadataCandidate? value)
    {
        _torrentRequest?.Cancel();
        Torrents.Clear();
        TorrentStatus = value == null ? string.Empty : "Choose Find torrents to search this album.";
    }

    [RelayCommand]
    private async Task FindTorrents()
    {
        var query = TorrentQuery(Query, SelectedAlbum);
        if (string.IsNullOrWhiteSpace(query)) { TorrentStatus = "Enter a song, album or artist first."; return; }
        _torrentRequest?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(_closed.Token);
        _torrentRequest = request;
        IsSearchingTorrents = true;
        Torrents.Clear();
        TorrentStatus = "Searching EXT.to for FLAC torrents…";
        try
        {
            var result = await _torrentSearch.SearchAsync(query, request.Token);
            if (request.IsCancellationRequested) return;
            foreach (var item in result.Items) Torrents.Add(item);
            TorrentStatus = result.Items.Count == 0 ? "No matching FLAC torrent listings were returned. Try the artist and album name."
                : $"{result.Items.Count} FLAC listing(s). Sizes and seeders are reported by each source.";
            if (result.UnavailableSources.Count > 0)
                TorrentStatus += " Could not check: " + string.Join(", ", result.UnavailableSources) + ". Choose Find torrents to retry, or Open browser to search EXT.to and complete any site verification.";
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_torrentRequest, request))
            {
                _torrentRequest = null;
                IsSearchingTorrents = false;
            }
        }
    }

    [RelayCommand]
    private void UseTorrentLink(TorrentSearchItem? item)
    {
        if (item == null) return;
        if (string.IsNullOrWhiteSpace(item.MagnetLink)) { Status = "This listing did not expose a magnet link. Open Source to get its torrent file in your browser."; return; }
        var links = MagnetLinks.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!links.Contains(item.MagnetLink)) MagnetLinks = string.Join(Environment.NewLine, links.Append(item.MagnetLink));
        Status = "Magnet link added to the batch below. Choose Download in qBittorrent when ready.";
    }

    [RelayCommand]
    private void SearchTorrents()
    {
        try
        {
            Process.Start(new ProcessStartInfo(TorrentSearchUrl(Query, SelectedAlbum)) { UseShellExecute = true });
            Status = "Torrent search opened in your browser. Paste the chosen magnet links below, or choose downloaded .torrent files.";
        }
        catch (Exception ex) { Status = ex.Message; }
    }

    [RelayCommand]
    private void Download()
    {
        try
        {
            var items = QBitTorrentService.ParseMagnets(MagnetLinks).Concat(TorrentFiles).ToArray();
            QBitTorrentService.Send(Executable, SaveFolder, items);
            foreach (var item in items)
            {
                var match = Regex.Match(item, @"urn:btih:([a-fA-F0-9]{40})", RegexOptions.IgnoreCase);
                if (match.Success) _submittedHashes.Add(match.Groups[1].Value);
            }
            MagnetLinks = string.Empty;
            TorrentFiles.Clear();
            Status = $"Sent {items.Distinct(StringComparer.Ordinal).Count()} torrent(s) to qBittorrent.";
            ShowDownloads();
        }
        catch (Exception ex) { Status = ex.Message; }
    }

    [RelayCommand]
    private async Task ShowNative()
    {
        try
        {
            await QBitTorrentService.ShowNativeAsync(Executable);
            Status = "Opening the qBittorrent desktop window. Downloads keep running.";
        }
        catch (Exception ex) { Status = ex.Message; }
    }

    [RelayCommand]
    private void ShowDownloads()
    {
        DownloadPanelVisible = true;
        if (_monitorTask == null || _monitorTask.IsCompleted) _monitorTask = MonitorDownloadsAsync();
    }

    private async Task MonitorDownloadsAsync()
    {
        try
        {
            while (!_closed.IsCancellationRequested)
            {
                try { await RefreshDownloadsAsync(); }
                catch (OperationCanceledException) when (_closed.IsCancellationRequested) { break; }
                catch (Exception) { DownloadPanelStatus = "Waiting for qBittorrent’s local connection. Downloads continue in qBittorrent; this panel will reconnect automatically."; }
                await Task.Delay(TimeSpan.FromSeconds(2), _closed.Token);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task RefreshDownloadsAsync()
    {
        _qbitApi ??= QBitTorrentApiClient.LoadLocal();
        var items = (await _qbitApi.GetDownloadsAsync(_closed.Token))
            .Where(i => i.Category == "Undertone" || _submittedHashes.Contains(i.Hash)).OrderBy(i => i.Name).ToArray();
        if (_closed.IsCancellationRequested) return;
        if (!Downloads.SequenceEqual(items))
        {
            Downloads.Clear();
            foreach (var item in items) Downloads.Add(item);
        }
        DownloadPanelStatus = items.Length == 0 ? "Connected. Waiting for qBittorrent to add your download…"
            : $"{items.Length} download(s) · updated live";
    }

    [RelayCommand]
    private async Task PauseDownload(QBitDownloadItem? item) => await ChangeDownloadAsync(item, true);
    [RelayCommand]
    private async Task ResumeDownload(QBitDownloadItem? item) => await ChangeDownloadAsync(item, false);
    private async Task ChangeDownloadAsync(QBitDownloadItem? item, bool paused)
    {
        if (item == null || _closed.IsCancellationRequested) return;
        try
        {
            _qbitApi ??= QBitTorrentApiClient.LoadLocal();
            await _qbitApi.SetPausedAsync(item.Hash, paused, _closed.Token);
            await RefreshDownloadsAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception) { DownloadPanelStatus = "Could not update this download. Check the qBittorrent connection and try again."; }
    }

    [RelayCommand] private void ClearTorrentFiles() => TorrentFiles.Clear();
    public void Dispose() { _closed.Cancel(); _qbitApi?.Dispose(); }
}
