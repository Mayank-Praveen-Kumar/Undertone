using System.ComponentModel;
using Undertone.Models;
using Undertone.ViewModels;

namespace Undertone.Services;

/// <summary>Resumes only the pause owned by this preview, never a later user decision.</summary>
internal sealed class PreviewPlaybackLease : IDisposable
{
    private readonly PlayerViewModel _player;
    private readonly Guid? _track;
    private readonly TimeSpan _position;
    private bool _resume;
    public event Action? PlaybackTakenOver;
    public PreviewPlaybackLease(PlayerViewModel player)
    {
        _player = player;
        _track = player.CurrentTrack?.Id;
        _position = player.Position;
        _resume = player.IsPlaying;
        if (_resume) player.PlayPauseCommand.Execute(null);
        player.PropertyChanged += Changed;
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerViewModel.CurrentTrack)
            || (e.PropertyName == nameof(PlayerViewModel.State) && _player.State != PlaybackState.Paused))
        {
            _resume = false;
            PlaybackTakenOver?.Invoke();
        }
    }
    public void CancelResume() => _resume = false;
    public void Dispose()
    {
        _player.PropertyChanged -= Changed;
        if (_resume && _player.State == PlaybackState.Paused && _player.CurrentTrack?.Id == _track
            && Math.Abs((_player.Position - _position).TotalSeconds) < 2)
        { _resume = false; _player.PlayPauseCommand.Execute(null); }
    }
}
