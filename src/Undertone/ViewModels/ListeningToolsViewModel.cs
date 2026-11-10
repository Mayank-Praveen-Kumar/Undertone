using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Undertone.Helpers;
using Undertone.Models;
using Undertone.Services;

namespace Undertone.ViewModels;

public partial class ListeningToolsViewModel : ViewModelBase
{
    private readonly PlayerViewModel _player;
    private readonly ILibraryService _library;
    private readonly ListeningToolsStore _store;
    private readonly SemaphoreSlim _stateGate = new(1, 1);
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveSessionCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameSessionCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSessionCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreSessionCommand))]
    private bool _isSavingChanges;
    private bool _ready;
    private bool _loading = true;
    private IReadOnlyList<LibraryHealthIssue> _allIssues = Array.Empty<LibraryHealthIssue>();
    public ObservableCollection<NamedListeningSession> Sessions { get; } = new();
    public ObservableCollection<LibraryHealthIssue> HealthIssues { get; } = new();
    public ObservableCollection<UndoEntry> UndoEntries => UndoJournal.Shared.Entries;
    public IReadOnlyList<string> HealthFilters { get; } = new[] { "All issues", "Missing file", "Possible duplicate", "Missing artwork", "Missing lyrics", "Incomplete tags" };
    [ObservableProperty] private bool _isLowPowerMode;
    [ObservableProperty] private string _sessionName = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _modeStatus = "";
    [ObservableProperty] private string _healthSummary = "Run a check when you want an overview. Files are never changed by this scan.";
    [ObservableProperty] private string _selectedHealthFilter = "All issues";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameSessionCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSessionCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreSessionCommand))]
    private NamedListeningSession? _selectedSession;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevealIssueCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayIssueCommand))]
    private LibraryHealthIssue? _selectedIssue;
    private bool HasSelectedSession => SelectedSession != null && !IsSavingChanges;
    private bool CanSaveSession => _ready && !IsSavingChanges && (_player.CurrentTrack != null || _player.UpNext.Count > 0);
    public bool CanUndo => !UndoJournal.Shared.IsBusy && UndoEntries.Count > 0;
    public string UndoLabel => UndoEntries.FirstOrDefault()?.Label is { } label ? $"Undo: {label}" : "Nothing to undo";

    public ListeningToolsViewModel(PlayerViewModel player, ILibraryService library, IPersistenceService persistence)
    {
        _player = player; _library = library;
        _store = new ListeningToolsStore(persistence.DataDirectory);
        player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PlayerViewModel.HasContent) or nameof(PlayerViewModel.CurrentTrack))
                SaveSessionCommand.NotifyCanExecuteChanged();
        };
        UndoJournal.Shared.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(UndoLabel));
            UndoLastCommand.NotifyCanExecuteChanged();
        };
    }
    public async Task InitializeAsync()
    {
        try
        {
            var state = await _store.LoadAsync();
            IsLowPowerMode = state.LowPowerMode;
            ListeningPowerMode.Enabled = state.LowPowerMode;
            foreach (var session in state.Sessions.OrderByDescending(s => s.SavedAt)) Sessions.Add(session);
            _ready = true;
            SaveSessionCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex) { Status = $"Couldn't load saved listening tools. The existing file was kept. {ex.Message}"; }
        finally { _loading = false; }
    }
    public async Task FlushAsync()
    {
        await _stateGate.WaitAsync();
        _stateGate.Release();
    }
    partial void OnIsLowPowerModeChanged(bool value)
    {
        ListeningPowerMode.Enabled = value;
        if (!_loading) _ = SavePowerModeAsync();
    }
    private async Task SavePowerModeAsync()
    {
        await _stateGate.WaitAsync();
        IsSavingChanges = true;
        try
        {

            try { await PersistAsync(Sessions.ToList()); ModeStatus = IsLowPowerMode ? "Low-power mode is on." : "Low-power mode is off."; }
            catch (Exception ex) { ModeStatus = $"Mode changed for this run, but couldn't be saved: {ex.Message}"; }

        }
        finally { IsSavingChanges = false; _stateGate.Release(); }
    }
    private Task PersistAsync(List<NamedListeningSession> sessions)
    {
        if (!_ready) throw new InvalidOperationException("Saved listening tools have not loaded successfully.");
        return _store.SaveAsync(new ListeningToolsState { LowPowerMode = IsLowPowerMode, Sessions = sessions });
    }
    private string? CheckedName(NamedListeningSession? replacing = null)
    {
        var name = SessionName.Trim();
        if (name.Length is < 1 or > 80) { Status = "Enter a session name between 1 and 80 characters."; return null; }
        if (Sessions.Any(s => s.Id != replacing?.Id && s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        { Status = "That session name is already used. Choose another name."; return null; }
        return name;
    }
    [RelayCommand(CanExecute = nameof(CanSaveSession))]
    private async Task SaveSession()
    {
        await _stateGate.WaitAsync();
        IsSavingChanges = true;
        try
        {

            var name = CheckedName(); if (name == null) return;
            var tracks = (_player.CurrentTrack == null ? Enumerable.Empty<Track>() : new[] { _player.CurrentTrack }).Concat(_player.UpNext).ToList();
            if (tracks.Count == 0) { Status = "Play a song or add songs to the queue first."; return; }
            var session = new NamedListeningSession
            {
                Name = name, PositionSeconds = _player.CurrentTrack == null ? 0 : _player.Position.TotalSeconds,
                Shuffle = _player.IsShuffleEnabled, Repeat = _player.RepeatMode,
                Tracks = tracks.Select(t => new SessionTrack { Id = t.Id, Path = t.FilePath }).ToList()
            };
            try
            {
                var list = Sessions.ToList(); list.Insert(0, session); await PersistAsync(list);
                Sessions.Insert(0, session); UndoJournal.Shared.Prune(); SelectedSession = session; SessionName = ""; Status = $"Saved “{name}”.";
            }
            catch (Exception ex) { Status = $"Couldn't save the session: {ex.Message}"; }

        }
        finally { IsSavingChanges = false; _stateGate.Release(); }
    }
    [RelayCommand(CanExecute = nameof(HasSelectedSession))]
    private async Task RestoreSession()
    {
        if (SelectedSession is not { } session) { Status = "Select a session to resume."; return; }
        try
        {
            var saved = session.Tracks.ToArray();
            var metadata = App.Services?.GetService(typeof(IMetadataService)) as IMetadataService;
            var resolved = await Task.Run(() => saved.Select(s => _library.GetTrackById(s.Id) ??
                (File.Exists(s.Path) ? metadata?.ReadTrackMetadata(s.Path) : null))
                .Where(t => t != null && (t.IsRemoteStream || File.Exists(t.FilePath))).Cast<Track>().ToList());
            if (resolved.Count == 0) { Status = "None of this session's songs are currently available."; return; }
            var position = resolved[0].Id == saved[0].Id ? TimeSpan.FromSeconds(Math.Max(0, session.PositionSeconds)) : TimeSpan.Zero;
            _player.ResumeNamedSession(resolved, position, session.Shuffle, session.Repeat);
            Status = $"Resumed “{session.Name}”." + (resolved.Count < saved.Length ? $" Skipped {saved.Length - resolved.Count} unavailable songs." : "");
        }
        catch (Exception ex) { Status = $"Couldn't resume the session: {ex.Message}"; }
    }
    [RelayCommand(CanExecute = nameof(HasSelectedSession))]
    private async Task RenameSession()
    {
        await _stateGate.WaitAsync();
        IsSavingChanges = true;
        try
        {

            if (SelectedSession is not { } session) { Status = "Select a session to rename."; return; }
            var name = CheckedName(session); if (name == null) return;
            var old = session.Name; session.Name = name;
            try
            {
                await PersistAsync(Sessions.ToList());
                var index = Sessions.IndexOf(session); Sessions.RemoveAt(index); Sessions.Insert(index, session); SelectedSession = session;
                SessionName = ""; Status = $"Renamed to “{name}”.";
            }
            catch (Exception ex) { session.Name = old; Status = $"Couldn't rename the session: {ex.Message}"; }

        }
        finally { IsSavingChanges = false; _stateGate.Release(); }
    }
    [RelayCommand(CanExecute = nameof(HasSelectedSession))]
    private async Task DeleteSession()
    {
        await _stateGate.WaitAsync();
        IsSavingChanges = true;
        try
        {

            if (SelectedSession is not { } session) { Status = "Select a session to delete."; return; }
            try
            {
                await PersistAsync(Sessions.Where(s => s.Id != session.Id).ToList());
                Sessions.Remove(session); SelectedSession = null;
                UndoJournal.Shared.Record($"Delete session “{session.Name}”",
                () => RestoreDeletedSessionAsync(session),
                () => !Sessions.Any(s => s.Id == session.Id || s.Name.Equals(session.Name, StringComparison.OrdinalIgnoreCase)));
                Status = "Session deleted. You can undo this change.";
            }
            catch (Exception ex) { Status = $"Couldn't delete the session: {ex.Message}"; }

        }
        finally { IsSavingChanges = false; _stateGate.Release(); }
    }
    private async Task RestoreDeletedSessionAsync(NamedListeningSession session)
    {
        await _stateGate.WaitAsync();
        IsSavingChanges = true;
        try
        {
            if (Sessions.Any(s => s.Id == session.Id || s.Name.Equals(session.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("That session already exists.");
            var list = Sessions.ToList(); list.Insert(0, session);
            await PersistAsync(list); Sessions.Insert(0, session);
        }
        finally { IsSavingChanges = false; _stateGate.Release(); }
    }
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task UndoLast()
    {
        try { Status = await UndoJournal.Shared.UndoAsync() ? "Change undone." : "Nothing remains to undo."; }
        catch (Exception ex) { Status = $"Couldn't undo the change: {ex.Message}"; }
    }
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ScanHealth(CancellationToken ct)
    {
        var tracks = _library.AllTracks.ToArray();
        HealthSummary = "Checking your library…";
        var progress = new Progress<string>(s => HealthSummary = s);
        try
        {
            var report = await Task.Run(() => LibraryHealthScanner.Scan(tracks, ct, progress), ct);
            _allIssues = report.Issues; ApplyHealthFilter(); HealthSummary = report.Summary;
        }
        catch (OperationCanceledException) { HealthSummary = "Check cancelled. Previous results were kept."; }
        catch (Exception ex) { HealthSummary = $"Couldn't finish the check: {ex.Message}"; }
    }
    partial void OnSelectedHealthFilterChanged(string value) => ApplyHealthFilter();
    private void ApplyHealthFilter()
    {
        SelectedIssue = null; HealthIssues.Clear();
        foreach (var issue in _allIssues.Where(i => SelectedHealthFilter == "All issues" || i.Category == SelectedHealthFilter)) HealthIssues.Add(issue);
    }
    private bool CanRevealIssue => SelectedIssue != null;
    private bool CanPlayIssue => SelectedIssue is { Track: { } t } && (t.IsRemoteStream || File.Exists(t.FilePath));
    [RelayCommand(CanExecute = nameof(CanRevealIssue))]
    private void RevealIssue() { if (SelectedIssue != null) PlatformHelper.ShowInFileManager(SelectedIssue.Track.FilePath); }
    [RelayCommand(CanExecute = nameof(CanPlayIssue))]
    private void PlayIssue() { if (SelectedIssue != null) _player.ReplaceQueueAndPlay(new[] { SelectedIssue.Track }, 0); }
}
