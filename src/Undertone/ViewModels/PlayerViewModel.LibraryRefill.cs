using Avalonia.Threading;
using Undertone.Models;

namespace Undertone.ViewModels;

public partial class PlayerViewModel
{
    private bool _emptyQueueRefillScheduled;

    private void ScheduleEmptyQueueRefill()
    {
        if (_emptyQueueRefillScheduled || UpNext.Count != 0 || CurrentTrack == null) return;
        _emptyQueueRefillScheduled = true;
        // Queue operations clear and rebuild Up Next in several steps. Wait until
        // that operation is finished, then recheck before adding anything.
        Dispatcher.UIThread.Post(() =>
        {
            _emptyQueueRefillScheduled = false;
            RefillEmptyQueueFromLibrary();
        }, DispatcherPriority.Background);
    }

    private void RefillEmptyQueueFromLibrary()
    {
        if (UpNext.Count != 0 || CurrentTrack == null ||
            StopAfterCurrentTrack || RepeatMode == RepeatMode.One || IsRadioActive) return;

        var candidates = _library.Tracks
            .Where(t => t.Id != CurrentTrack.Id && !t.SkipWhenShuffling && !IsBlockedExplicit(t))
            .DistinctBy(t => t.Id).ToList();
        if (candidates.Count == 0) return;
        var shuffled = Helpers.ShuffleHelper.WeightedShuffle(candidates, recentlyPlayed: _recentlyPlayed);
        if (shuffled.Count == 0) return;

        // Preserve the playing song, its position and history. Only Up Next changes.
        _repeatCycleTracks = new List<Track> { CurrentTrack };
        _repeatCycleTracks.AddRange(candidates);
        _originalQueue = new List<Track>(_repeatCycleTracks);
        IsShuffleEnabled = true;
        MarkQueueChanged();
        UpNext.AddRange(shuffled);
    }
}
