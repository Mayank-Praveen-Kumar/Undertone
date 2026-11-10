using Undertone.Models;
using Undertone.Services;

namespace Undertone.ViewModels;

public partial class PlayerViewModel
{
    private void RememberQueueEdit(string label)
    {
        if (UndoJournal.Shared.IsBusy) return;
        var anchor = CurrentTrack?.Id;
        var upcoming = UpNext.ToArray();
        var cycle = _repeatCycleTracks.ToArray();
        var original = _originalQueue.ToArray();
        var parked = _parkedExplicit.ToArray();
        var shuffle = IsShuffleEnabled;
        var radio = IsRadioActive;
        var seed = _radioSeed;
        UndoJournal.Shared.Record(label, () =>
        {
            CancelAutoMixTransition("undo queue edit");
            MarkQueueChanged();
            _repeatCycleTracks = cycle.ToList();
            _originalQueue = original.ToList();
            _parkedExplicit.Clear(); _parkedExplicit.AddRange(parked);
            IsShuffleEnabled = shuffle; IsRadioActive = radio; _radioSeed = seed;
            UpNext.ReplaceAll(upcoming);
            PruneBlockedExplicit(wholeQueue: true);
            OnPropertyChanged(nameof(HasContent));
            SaveQueueStateInBackground();
            return Task.CompletedTask;
        }, () => CurrentTrack?.Id == anchor);
    }

    public void ResumeNamedSession(IList<Track> tracks, TimeSpan position, bool shuffle, RepeatMode repeat)
    {
        if (tracks.Count == 0) return;
        _resumePositionMs = position > TimeSpan.Zero ? (long)position.TotalMilliseconds : -1;
        _resumeTrackId = tracks[0].Id;
        ReplaceQueueAndPlay(tracks, 0);
        IsShuffleEnabled = shuffle;
        if (shuffle) _originalQueue = tracks.ToList();
        RepeatMode = repeat;
        SaveQueueStateInBackground();
    }
}
