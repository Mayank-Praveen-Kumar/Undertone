using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Undertone.Models;
using Undertone.Services;

namespace Undertone.ViewModels;

public partial class DiscoverCard : ObservableObject
{
    public DiscoverSong Song { get; }
    public string Title => Song.Title;
    public string Artist => Song.Artist;
    public string Reason { get; }
    public Track? LocalTrack { get; }
    public bool IsInLibrary => LocalTrack is not null;
    [ObservableProperty] private string _album = "Album details available on request";
    public DiscoverCard(DiscoverSong song, string reason, Track? localTrack)
        => (Song, Reason, LocalTrack) = (song, reason, localTrack);
}

public partial class DiscoverViewModel : ViewModelBase
{
    private readonly ILibraryService _library;
    private readonly IPlayHistoryService _history;
    private readonly DiscoverService _service;
    private DiscoverPreferences _preferences;
    private string _listeningModeId = "everyday";
    private IReadOnlyList<PlayHistoryEvent> ModeHistory => _history.Events.TakeLast(20000).Where(e => e.ListeningModeId == _listeningModeId).ToArray();
    private CancellationTokenSource? _request;
    private List<DiscoverSong> _candidates = new();
    private string _seedTitle = "your listening";
    private bool _active;
    private bool _loaded;
    public PlayerViewModel Player { get; }
    public ListeningModesViewModel? Modes { get; }
    public ObservableCollection<DiscoverCard> Songs { get; } = new();
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = "Play a song, then start Discover.";
    [ObservableProperty] private string _subtitle = "Recommendations shaped by your listening.";
    [ObservableProperty] private int _mode;
    [ObservableProperty] private double _adventure;
    [ObservableProperty] private bool _enabled;
    public bool NeedsStart => !Enabled;
    public bool IsRadio => Mode == 0;
    public bool IsForYou => Mode == 1;
    public bool IsRediscover => Mode == 2;
    public event EventHandler<DiscoverCard>? PreviewRequested;
    public event EventHandler<DiscoverCard>? AlbumRequested;

    public DiscoverViewModel(PlayerViewModel player, ILibraryService library, IPlayHistoryService history, DiscoverService service, ListeningModesViewModel? modes = null)
    {
        (Player, _library, _history, _service) = (player, library, history, service);
        _preferences = service.LoadPreferences();
        _enabled = _preferences.Enabled;
        _adventure = _preferences.Adventure;
        Modes = modes;
        if (modes is not null) modes.ModeChanged += (_, id) => ChangeListeningMode(id);
        Player.PropertyChanged += PlayerChanged;
    }
    private void ChangeListeningMode(string id)
    {
        if (_listeningModeId == id) return;
        _request?.Cancel(); SavePreferences();
        _listeningModeId = id;
        _preferences = _service.LoadPreferences(id);
        _enabled = _preferences.Enabled; _adventure = _preferences.Adventure;
        OnPropertyChanged(nameof(Enabled)); OnPropertyChanged(nameof(NeedsStart)); OnPropertyChanged(nameof(Adventure));
        _candidates.Clear(); Songs.Clear(); _loaded = false;
        if (_active && Enabled) _ = RefreshAsync();
    }

    public void SetActive(bool active)
    {
        _active = active;
        if (!active) { _request?.Cancel(); return; }
        if (Enabled && !_loaded) _ = RefreshAsync();
    }

    private async void PlayerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PlayerViewModel.CurrentTrack)) return;
        _loaded = false;
        if (!_active || !Enabled || Mode != 0) return;
        _request?.Cancel();
        var request = new CancellationTokenSource();
        _request = request;
        try
        {
            await Task.Delay(700, request.Token);
            if (_active && ReferenceEquals(_request, request)) await RefreshAsync();
        }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(_request, request)) _request = null; request.Dispose(); }
    }

    partial void OnEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(NeedsStart));
        _preferences.Enabled = value;
        SavePreferences();
        if (!value) { _request?.Cancel(); Songs.Clear(); _loaded = false; Status = "Discover is off."; }
    }
    partial void OnAdventureChanged(double value)
    {
        _preferences.Adventure = value;
        if (Mode != 2) Rerank();
    }
    public void SavePreferences()
    {
        try { _service.SavePreferences(_preferences, _listeningModeId); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Status = "Your discovery preferences could not be saved."; }
    }
    partial void OnModeChanged(int value)
    {
        OnPropertyChanged(nameof(IsRadio)); OnPropertyChanged(nameof(IsForYou)); OnPropertyChanged(nameof(IsRediscover));
        _loaded = false;
        if (_active && Enabled) _ = RefreshAsync();
    }
    [RelayCommand] private async Task Start() { Enabled = true; await RefreshAsync(); }
    [RelayCommand] private void SetMode(string mode) => Mode = int.TryParse(mode, out var n) ? Math.Clamp(n, 0, 2) : 0;
    [RelayCommand] private void TurnOff() => Enabled = false;
    [RelayCommand] private Task Refresh() => RefreshAsync();
    [RelayCommand] private void Preview(DiscoverCard card) => PreviewRequested?.Invoke(this, card);
    [RelayCommand] private void PlayLocal(DiscoverCard card)
    {
        if (card.LocalTrack is { } track) Player.PlayFrom(track, TimeSpan.Zero);
    }
    [RelayCommand] private void FindAlbum(DiscoverCard card) => AlbumRequested?.Invoke(this, card);
    [RelayCommand] private void LessLikeThis(DiscoverCard card)
    {
        _preferences.HiddenSongs.Add(card.Song.Key);
        _preferences.ArtistFeedback[card.Artist] = Math.Max(-4, _preferences.ArtistFeedback.GetValueOrDefault(card.Artist) - 1);
        SavePreferences();
        Songs.Remove(card);
        if (Mode != 2) Rerank();
    }
    [RelayCommand] private void ResetFeedback()
    {
        _preferences.HiddenSongs.Clear(); _preferences.ArtistFeedback.Clear(); SavePreferences();
        _loaded = false; if (Enabled) _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (!Enabled) return;
        _request?.Cancel();
        using var request = new CancellationTokenSource();
        _request = request;
        IsBusy = true;
        try
        {
            Songs.Clear();
            var history = ModeHistory;
            var modeCounts = history.Where(e => !e.Skipped).GroupBy(e => e.TrackId).ToDictionary(g => g.Key, g => g.Count());
            // Legacy play counters are useful for cold starts, but must not import plays
            // from another listening mode once mode-tagged history exists.
            var useLegacyCounters = _listeningModeId == "everyday" && history.Count == 0
                && !_history.Events.Any(e => e.ListeningModeId != "everyday");
            if (Mode == 2)
            {
                Subtitle = "Old favourites, ready for another listen.";
                var lastInMode = history.Where(e => !e.Skipped).GroupBy(e => e.TrackId).ToDictionary(g => g.Key, g => g.Max(e => e.PlayedAtUtc));
                var tracks = _library.Tracks.Where(t => !t.IsDisliked && !t.IsSnoozed
                    && (useLegacyCounters ? t.PlayCount > 0 && (t.LastPlayed is null || t.LastPlayed < DateTime.UtcNow.AddDays(-14))
                        : lastInMode.TryGetValue(t.Id, out var last) && last < DateTime.UtcNow.AddDays(-14)))
                    .Where(t => !_preferences.HiddenSongs.Contains(DiscoverSong.KeyFor(t.Artist, t.Title)))
                    .OrderByDescending(t => (t.IsFavorite ? 5 : 0) + t.Rating + Math.Log(1 + (useLegacyCounters ? t.PlayCount : modeCounts.GetValueOrDefault(t.Id))))
                    .ThenBy(t => t.LastPlayed).Take(24);
                foreach (var track in tracks)
                    Songs.Add(new(new(track.Title, track.Artist), "From your library · not played in the last two weeks", track) { Album = track.Album });
                Status = Songs.Count == 0 ? "No older listens to rediscover yet." : $"{Songs.Count} songs from your library · offline";
                _loaded = true;
                return;
            }
            var current = Player.CurrentTrack;
            var seeds = Mode == 0 ? (current is null ? Array.Empty<Track>() : new[] { current })
                : _library.Tracks.Where(t => !t.IsDisliked && !t.IsSnoozed && (modeCounts.ContainsKey(t.Id)
                    || _listeningModeId == "everyday" && (t.IsFavorite || useLegacyCounters && t.PlayCount > 0)))
                    .OrderByDescending(t => Math.Log(1 + (useLegacyCounters ? t.PlayCount : modeCounts.GetValueOrDefault(t.Id)))
                        + (_listeningModeId == "everyday" ? (t.IsFavorite ? 4 : 0) + t.Rating : 0))
                    .DistinctBy(t => t.Artist, StringComparer.OrdinalIgnoreCase).Take(3).ToArray();
            if (seeds.Length == 0 && current is not null) seeds = new[] { current };
            if (seeds.Length == 0) { Status = "Play a song or favourite a few tracks to get started."; return; }
            _seedTitle = Mode == 0 ? seeds[0].Title : "your recent favourites";
            Subtitle = Mode == 0 ? $"Song radio · {seeds[0].Artist} — {seeds[0].Title}" : "For you · shaped by favourites, repeats and skips";
            Status = "Finding songs on Last.fm…";
            var candidates = new List<DiscoverSong>();
            var offline = false;
            foreach (var seed in seeds)
            {
                var response = await _service.SimilarAsync(seed, request.Token);
                candidates.AddRange(response.Songs); offline |= response.Offline;
            }
            request.Token.ThrowIfCancellationRequested();
            if (!_active) return;
            var seedKeys = seeds.Select(s => DiscoverSong.KeyFor(s.Artist, s.Title)).ToHashSet();
            _candidates = candidates.Where(s => !seedKeys.Contains(s.Key)).ToList();
            Rerank(); _loaded = true;
            Status = Songs.Count == 0 ? "No suggestions found for this selection. Try another song or reset feedback."
                : $"{Songs.Count} suggestions · ranked on this computer" + (offline ? " · saved results (offline)" : " · Last.fm");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (ReferenceEquals(_request, request)) Status = ex.Message; }
        finally { if (ReferenceEquals(_request, request)) { IsBusy = false; _request = null; } }
    }

    private void Rerank()
    {
        Songs.Clear();
        var local = _library.Tracks.GroupBy(t => DiscoverSong.KeyFor(t.Artist, t.Title)).ToDictionary(g => g.Key, g => g.First());
        foreach (var item in DiscoverService.Rank(_candidates, _library.Tracks, ModeHistory, _preferences, _seedTitle, _listeningModeId == "everyday"))
        {
            local.TryGetValue(item.Song.Key, out var track);
            Songs.Add(new(item.Song, item.Reason, track) { Album = track?.Album ?? "Find album details" });
        }
    }
}
