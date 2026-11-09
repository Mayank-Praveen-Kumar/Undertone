using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Undertone.Models;
using Undertone.ViewModels;
using Xunit;

namespace Undertone.Tests;

public class EmptyQueueLibraryShuffleTests
{
    private static (PlayerViewModel vm, FakeLibraryService library) Create()
    {
        var library = new FakeLibraryService();
        library.TrackList.AddRange(Enumerable.Range(0, 8).Select(i => new Track
        {
            Id = Guid.NewGuid(), Title = $"Song {i}", Artist = "Test",
            FilePath = TestPaths.Primary("music", $"{i}.mp3"), Duration = TimeSpan.FromMinutes(3)
        }));
        return (new PlayerViewModel(new FakeAudioPlayer(), library, new TestPersistenceService(), new FakeAnimatedCoverService()), library);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyStoppedQueueShufflesTheWholeLibraryEvenWithAnOldTrackLoaded(bool oldTrackLoaded)
    {
        var (vm, library) = Create();
        if (oldTrackLoaded) vm.ReplaceQueueAndPlay(new[] { library.TrackList[0] }, 0);
        vm.State = PlaybackState.Stopped;
        vm.PlayPauseCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(PlaybackState.Playing, vm.State);
        Assert.True(vm.IsShuffleEnabled);
        Assert.NotNull(vm.CurrentTrack);
        var queued = vm.UpNext.Prepend(vm.CurrentTrack!).Select(t => t.Id).ToArray();
        Assert.Equal(library.TrackList.Count, queued.Distinct().Count());
        Assert.Equal(library.TrackList.Select(t => t.Id).Order(), queued.Order());
    }

    [Fact]
    public void PausedSongResumesWithoutChangingTheCurrentSong()
    {
        var (vm, library) = Create();
        var song = library.TrackList[0];
        vm.ReplaceQueueAndPlay(new[] { song }, 0);
        vm.PlayPauseCommand.Execute(null);
        vm.PlayPauseCommand.Execute(null);
        Assert.Equal(song.Id, vm.CurrentTrack?.Id);
        Assert.DoesNotContain(vm.UpNext, t => t.Id == song.Id);
        Assert.Equal(PlaybackState.Playing, vm.State);
    }

    [Fact]
    public void ExistingQueueIsNotReplacedByLibraryShuffle()
    {
        var (vm, library) = Create();
        vm.ReplaceQueueAndPlay(library.TrackList.Take(2).ToArray(), 0);
        vm.State = PlaybackState.Stopped;
        vm.PlayPauseCommand.Execute(null);
        Assert.Equal(library.TrackList[0].Id, vm.CurrentTrack?.Id);
        Assert.Equal(library.TrackList[1].Id, Assert.Single(vm.UpNext).Id);
        Assert.False(vm.IsShuffleEnabled);
    }
}
