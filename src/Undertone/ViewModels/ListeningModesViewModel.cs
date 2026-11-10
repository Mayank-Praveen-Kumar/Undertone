using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Undertone.Models;
using Undertone.Services;

namespace Undertone.ViewModels;

public partial class ListeningModesViewModel : ViewModelBase
{
    private readonly SettingsViewModel _settings;
    private readonly PlayerViewModel _player;
    private readonly string _file;
    private bool _ready, _applying;
    private CancellationTokenSource? _saveRequest;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public ObservableCollection<ListeningMode> Profiles { get; } = new();
    [ObservableProperty] private ListeningMode? _selectedMode;
    [ObservableProperty] private string _customName = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;
    public string ActiveId => SelectedMode?.Id ?? "everyday";
    public bool IsCustom => SelectedMode?.IsCustom == true;
    public Task PendingSwitch { get; private set; } = Task.CompletedTask;
    public string Description => ActiveId switch
    {
        "focus" => "Grayscale, including artwork · exclusive audio on by default",
        "energy" => "Bright high contrast · Kawarp lyric background on by default",
        "wind-down" => "Soft nature colours · volume starts at 75%",
        "everyday" => "Your normal appearance and playback settings",
        _ => "Your own appearance, audio and lyric background settings"
    };
    public event EventHandler<string>? ModeChanged;
    public event EventHandler? SettingsRequested;
    public ListeningModesViewModel(SettingsViewModel settings, PlayerViewModel player, string dataRoot)
    {
        (_settings, _player, _file) = (settings, player, Path.Combine(dataRoot, "listening-modes.json"));
        settings.PropertyChanged += (_, _) => QueueSave();
        player.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(PlayerViewModel.Volume)) QueueSave(); };
        FocusGrayscale.Attach();
    }
    public async Task InitializeAsync()
    {
        ListeningModesState state;
        try { state = JsonSerializer.Deserialize<ListeningModesState>(await File.ReadAllTextAsync(_file)) ?? new(); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { state = new(); }
        state.Profiles ??= new();
        foreach (var (id, name) in new[] { ("everyday", "Everyday"), ("focus", "Focus"), ("energy", "Energy"), ("wind-down", "Wind down"), ("custom", "Custom") })
            Profiles.Add(state.Profiles.FirstOrDefault(p => p.Id == id) ?? new ListeningMode { Id = id, Name = name, IsCustom = id == "custom" });
        foreach (var profile in state.Profiles.Where(p => p.IsCustom && !Profiles.Any(x => x.Id == p.Id))) Profiles.Add(profile);
        SelectedMode = Profiles.FirstOrDefault(p => p.Id == state.ActiveId) ?? Profiles[0];
        CaptureSelected();
        ListeningModeContext.CurrentId = ActiveId;
        FocusGrayscale.SetEnabled(SelectedMode.Grayscale);
        _ready = true;
        ModeChanged?.Invoke(this, ActiveId);
        await SaveAsync();
    }
    partial void OnSelectedModeChanged(ListeningMode? oldValue, ListeningMode? newValue)
    {
        if (!_ready || newValue is null) return;
        if (_applying) { _selectedMode = oldValue; OnPropertyChanged(nameof(SelectedMode)); return; }
        PendingSwitch = SwitchAsync(oldValue, newValue);
    }
    private void Capture(ListeningMode mode)
    {
        var snapshot = _settings.CaptureListeningMode(mode.Id, mode.Name, mode.IsCustom, mode.Grayscale, _player.Volume);
        // Preserve the bound option object so ComboBox selection remains stable.
        mode.Theme = snapshot.Theme; mode.AccentHex = snapshot.AccentHex; mode.AccentName = snapshot.AccentName;
        mode.Icon = snapshot.Icon; mode.Volume = snapshot.Volume; mode.Exclusive = snapshot.Exclusive;
        mode.FlowingLyrics = snapshot.FlowingLyrics; mode.FlowingStyle = snapshot.FlowingStyle; mode.Initialized = true;
    }
    private void CaptureSelected() { if (SelectedMode is { } mode) Capture(mode); }
    private async Task SwitchAsync(ListeningMode? oldMode, ListeningMode next)
    {
        _applying = true; IsBusy = true;
        _saveRequest?.Cancel();
        try
        {
            if (oldMode is not null) Capture(oldMode);
            if (!next.Initialized)
            {
                if (next.IsCustom) { next.Grayscale = FocusGrayscale.Enabled; Capture(next); }
                else
                {
                    var baseline = Profiles.First(p => p.Id == "everyday");
                    next.Theme = baseline.Theme; next.AccentHex = baseline.AccentHex; next.AccentName = baseline.AccentName;
                    next.Icon = baseline.Icon; next.Volume = baseline.Volume; next.Exclusive = baseline.Exclusive;
                    next.FlowingLyrics = baseline.FlowingLyrics; next.FlowingStyle = baseline.FlowingStyle; next.Initialized = true;
                }
                if (next.Id == "focus") { next.Grayscale = true; next.Exclusive = true; next.Theme = "Gray"; next.AccentHex = "#707070"; next.AccentName = "Custom"; }
                if (next.Id == "energy")
                {
                    _settings.EnsureModeTheme("listening-energy", "Energy", "#FFF8DB", "#FFE17A", "#B92300");
                    next.Theme = "Custom:listening-energy"; next.AccentHex = "#B92300"; next.AccentName = "Custom";
                    next.Exclusive = false; next.FlowingLyrics = true; next.FlowingStyle = FlowingStyles.Kawarp;
                }
                if (next.Id == "wind-down")
                {
                    _settings.EnsureModeTheme("listening-nature", "Wind down", "#EBF3F6", "#DFE9CF", "#79543B");
                    next.Theme = "Custom:listening-nature"; next.AccentHex = "#79543B"; next.AccentName = "Custom";
                    next.Volume = 75; next.Exclusive = false;
                }
            }
            await _settings.ApplyListeningModeAsync(next);
            _player.Volume = Math.Clamp(next.Volume, 0, 100);
            FocusGrayscale.SetEnabled(next.Grayscale);
            ListeningModeContext.CurrentId = next.Id;
            OnPropertyChanged(nameof(ActiveId)); OnPropertyChanged(nameof(IsCustom)); OnPropertyChanged(nameof(Description));
            ModeChanged?.Invoke(this, next.Id);
            Status = next.Id == "focus" && !_settings.IsExclusiveAudioSupported ? "Exclusive audio is unavailable on this platform." : "";
            await SaveAsync();
        }
        catch (Exception ex) { Status = "Mode could not be fully applied: " + ex.Message; }
        finally { _applying = false; IsBusy = false; }
    }
    private async void QueueSave()
    {
        if (!_ready || _applying) return;
        _saveRequest?.Cancel();
        using var request = new CancellationTokenSource(); _saveRequest = request;
        try { await Task.Delay(350, request.Token); CaptureSelected(); await SaveAsync(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Status = "Mode changes could not be saved: " + ex.Message; }
        finally { if (ReferenceEquals(_saveRequest, request)) _saveRequest = null; }
    }
    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(new ListeningModesState { ActiveId = ActiveId, Profiles = Profiles.ToList() });
        await _gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            await File.WriteAllTextAsync(_file + ".tmp", json);
            File.Move(_file + ".tmp", _file, true);
        }
        finally { _gate.Release(); }
    }
    public async Task FlushAsync()
    {
        _saveRequest?.Cancel();
        await PendingSwitch;
        CaptureSelected();
        await SaveAsync();
    }
    [RelayCommand] private void AddCustom()
    {
        var name = CustomName.Trim(); if (name.Length == 0) { Status = "Give your mode a name."; return; }
        var mode = new ListeningMode { Id = "custom-" + Guid.NewGuid().ToString("N"), Name = name[..Math.Min(name.Length, 40)], IsCustom = true };
        Profiles.Add(mode); SelectedMode = mode; CustomName = "";
    }
    [RelayCommand] private void AdjustSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);
    [RelayCommand] private void ToggleExclusive() => _settings.ExclusiveAudioEnabled = !_settings.ExclusiveAudioEnabled;
    [RelayCommand] private void ToggleGrayscale()
    {
        if (!IsCustom || SelectedMode is null) return;
        SelectedMode.Grayscale = !SelectedMode.Grayscale; FocusGrayscale.SetEnabled(SelectedMode.Grayscale); QueueSave();
    }
}
