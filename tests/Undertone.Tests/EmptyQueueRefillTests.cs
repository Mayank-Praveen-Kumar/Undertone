using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Undertone.Models;
using Undertone.ViewModels;
using Xunit;
namespace Undertone.Tests;

public class EmptyQueueRefillTests
{
    private static (PlayerViewModel vm, FakeAudioPlayer player, FakeLibraryService library) Create()
    {
        var library = new FakeLibraryService();
        library.TrackList.AddRange(Enumerable.Range(0, 5).Select(i => new Track
        {
            Id = Guid.NewGuid(), Title = $"Song {i}", Artist = "Test",
            FilePath = TestPaths.Primary("music", $"{i}.mp3"), Duration = TimeSpan.FromMinutes(3)
        }));
        var player = new FakeAudioPlayer();
        return (new PlayerViewModel(player, library, new TestPersistenceService(), new FakeAnimatedCoverService()), player, library);
    }

    [AvaloniaFact]
    public void PlayingSingleSongFillsUpNextWithoutRestartingTheSong()
    {
        var (vm, player, library) = Create();
        vm.ReplaceQueueAndPlay(new[] { library.TrackList[0] }, 0);
        var playing = vm.CurrentTrack;
        var history = vm.History.ToArray();
        var position = vm.Position;
        Dispatcher.UIThread.RunJobs();
        Assert.Same(playing, vm.CurrentTrack);
        Assert.Equal(position, vm.Position);
        Assert.Equal(history, vm.History);
        Assert.Equal(PlaybackState.Playing, vm.State);
        Assert.Equal(4, vm.UpNext.Count);
        Assert.Equal(4, vm.UpNext.Select(t => t.Id).Distinct().Count());
        Assert.DoesNotContain(vm.UpNext, t => t.Id == playing!.Id);
        Assert.True(vm.IsShuffleEnabled);
    }

    [AvaloniaFact]
    public void RestoredStoppedSongGetsUpcomingSongsWithoutStartingPlayback()
    {
        var (vm, _, library) = Create();
        vm.ReplaceQueueAndPlay(new[] { library.TrackList[0] }, 0);
        vm.State = PlaybackState.Stopped;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(4, vm.UpNext.Count);
        Assert.Equal(PlaybackState.Stopped, vm.State);
        Assert.Same(library.TrackList[0], vm.CurrentTrack);
    }

    [AvaloniaFact]
    public void TemporaryEmptyQueueDuringReplacementDoesNotAppendExtraSongs()
    {
        var (vm, _, library) = Create();
        vm.ReplaceQueueAndPlay(new[] { library.TrackList[0] }, 0);
        vm.ReplaceQueueAndPlay(library.TrackList.Take(3).ToArray(), 0);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(library.TrackList.Skip(1).Take(2).Select(t => t.Id), vm.UpNext.Select(t => t.Id));
    }

    [AvaloniaFact]
    public void ClearingQueueRefillsItAndRespectsShuffleExclusions()
    {
        var (vm, _, library) = Create();
        library.TrackList[4].SkipWhenShuffling = true;
        vm.ReplaceQueueAndPlay(library.TrackList.Take(3).ToArray(), 0);
        vm.ClearQueue();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, vm.UpNext.Count);
        Assert.DoesNotContain(vm.UpNext, t => t.Id == library.TrackList[4].Id);
    }

    [AvaloniaFact]
    public void StopAfterCurrentStillLeavesQueueEmpty()
    {
        var (vm, _, library) = Create();
        vm.StopAfterCurrentTrack = true;
        vm.ReplaceQueueAndPlay(new[] { library.TrackList[0] }, 0);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(vm.UpNext);
    }
}
