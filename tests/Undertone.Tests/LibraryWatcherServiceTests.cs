using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Undertone.Models;
using Undertone.Services;
using Xunit;

namespace Undertone.Tests;

public class LibraryWatcherServiceTests
{
    // Refresh is called from the UI thread (launch, music-folder changes, the Watch
    // Folders toggle). On Linux, enabling a recursive watcher walks the whole folder
    // tree on the calling thread, so the rebuild must not block the caller.
    [Fact]
    public async Task Refresh_ReturnsWhileTheRebuildIsStillRunning()
    {
        var ct = TestContext.Current.CancellationToken;
        var rebuildStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseRebuild = new ManualResetEventSlim();
        using var watcher = new LibraryWatcherService(new FakeLibraryService(), () =>
        {
            rebuildStarted.TrySetResult();
            releaseRebuild.Wait(TimeSpan.FromSeconds(30), ct);
            return new AppSettings { WatchFoldersEnabled = false };
        });

        var refresh = Task.Run(watcher.Refresh, ct);
        var returned = await Task.WhenAny(refresh, Task.Delay(TimeSpan.FromSeconds(5), ct)) == refresh;
        var started = await Task.WhenAny(rebuildStarted.Task, Task.Delay(TimeSpan.FromSeconds(5), ct)) == rebuildStarted.Task;
        releaseRebuild.Set();
        await refresh;

        Assert.True(started, "the watcher rebuild never ran");
        Assert.True(returned, "Refresh blocked the caller until the watcher rebuild finished");
    }

    // On Linux a folder that can't be watched (inotify watch limit reached, unreadable
    // subfolder) raises Error synchronously from inside EnableRaisingEvents, i.e. on the
    // rebuild thread while it holds the gate, once per failing folder. A rebuild hits the
    // same errors again, so rebuilding for them queued rebuilds without end.
    [Fact]
    public async Task ErrorRaisedDuringWatcherSetup_DoesNotQueueAnotherRebuild()
    {
        var ct = TestContext.Current.CancellationToken;
        var rebuilds = 0;
        using var watcher = new LibraryWatcherService(new FakeLibraryService(), () =>
        {
            Interlocked.Increment(ref rebuilds);
            return new AppSettings { WatchFoldersEnabled = false };
        });

        lock (Gate(watcher))
        {
            for (var i = 0; i < 3; i++)
                RaiseError(watcher, new IOException("The configured user limit (8192) on the number of inotify watches has been reached."));
        }
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);

        Assert.Equal(0, Volatile.Read(ref rebuilds));
    }

    // A watcher that fails while running (outside setup) still gets rebuilt.
    [Fact]
    public async Task ErrorRaisedByARunningWatcher_StillRebuilds()
    {
        var ct = TestContext.Current.CancellationToken;
        var rebuilt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new LibraryWatcherService(new FakeLibraryService(), () =>
        {
            rebuilt.TrySetResult();
            return new AppSettings { WatchFoldersEnabled = false };
        });

        RaiseError(watcher, new IOException("watched folder went away"));
        var ran = await Task.WhenAny(rebuilt.Task, Task.Delay(TimeSpan.FromSeconds(5), ct)) == rebuilt.Task;

        Assert.True(ran, "a runtime watcher error no longer rebuilds the watchers");
    }

    [Fact]
    public async Task RecoveryScansDoNotOverlapAndRespectDisabledWatching()
    {
        var library = new FakeLibraryService();
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        library.HoldScan = hold;
        var settings = new AppSettings { MusicFolders = { Path.GetTempPath() } };
        using var watcher = new LibraryWatcherService(library, () => settings);
        var recover = typeof(LibraryWatcherService).GetMethod("RunReconcilingScan", BindingFlags.NonPublic | BindingFlags.Instance)!;
        recover.Invoke(watcher, null);
        for (var i = 0; i < 100 && library.ScanPasses.Count == 0; i++) await Task.Delay(20);
        Assert.Single(library.ScanPasses);
        recover.Invoke(watcher, null);
        await Task.Delay(100);
        Assert.Single(library.ScanPasses);
        hold.SetResult();
        for (var i = 0; i < 100 && (int)typeof(LibraryWatcherService).GetField("_recoveryActive", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(watcher)! != 0; i++) await Task.Delay(20);
        settings.WatchFoldersEnabled = false;
        recover.Invoke(watcher, null);
        await Task.Delay(100);
        Assert.Single(library.ScanPasses);
    }

    private static object Gate(LibraryWatcherService watcher)
        => typeof(LibraryWatcherService)
            .GetField("_gate", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(watcher)!;

    private static void RaiseError(LibraryWatcherService watcher, Exception ex)
        => typeof(LibraryWatcherService)
            .GetMethod("OnError", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(watcher, [watcher, new ErrorEventArgs(ex)]);
}
