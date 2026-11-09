using Undertone.Services;
using Undertone.Services.MetadataSearch;
using Undertone.ViewModels;
using Xunit;

namespace Undertone.Tests;

public class TorrentDownloadTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("Undertone-qbt-").FullName;
    private const string Magnet = "magnet:?xt=urn:btih:0123456789abcdef0123456789abcdef01234567&dn=Album%20Name";

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void BatchUsesSeparateArgumentsAndTheSelectedSaveFolder()
    {
        var exe = Path.Combine(_root, "qbittorrent.exe");
        File.WriteAllText(exe, "test only");
        var torrent = Path.Combine(_root, "album & title.torrent");
        File.WriteAllText(torrent, "test only");
        var folder = Path.Combine(_root, "music folder");

        var batch = Assert.Single(QBitTorrentService.BuildBatch(exe, folder, new[] { Magnet, torrent }));

        Assert.False(batch.UseShellExecute);
        Assert.False(batch.CreateNoWindow);
        Assert.Equal(System.Diagnostics.ProcessWindowStyle.Minimized, batch.WindowStyle);
        Assert.Equal(new[] { "--skip-dialog=true", "--add-stopped=false", "--save-path=" + folder, Magnet, torrent }, batch.ArgumentList);
        Assert.False(Directory.Exists(folder)); // validation is read-only
    }

    [Fact]
    public void NativeLaunchUsesNormalWindowAndNoTorrentArguments()
    {
        var exe = Path.Combine(_root, "qbittorrent.exe");
        File.WriteAllText(exe, "test only");
        var launch = QBitTorrentService.BuildNativeLaunch(exe);
        Assert.Equal(System.Diagnostics.ProcessWindowStyle.Normal, launch.WindowStyle);
        Assert.False(launch.CreateNoWindow);
        Assert.Empty(launch.ArgumentList);
    }

    [Theory]
    [InlineData("https://example.com/download.exe")]
    [InlineData("--save-path=C:/elsewhere")]
    [InlineData("magnet:?xt=urn:btih:invalid")]
    public void InvalidInputRejectsTheWholeBatchBeforeLaunching(string invalid)
    {
        var exe = Path.Combine(_root, "qbittorrent.exe");
        File.WriteAllText(exe, "test only");
        Assert.Throws<ArgumentException>(() => QBitTorrentService.BuildBatch(exe, _root, new[] { Magnet, invalid }));
    }

    [Fact]
    public void MagnetInputAcceptsNewlinesAndDeduplicates()
    {
        Assert.Equal(new[] { Magnet }, QBitTorrentService.ParseMagnets($"  {Magnet}\r\n{Magnet}\n"));
    }

    [Fact]
    public void LargeBatchesSplitIntoCommandsBelowTheWindowsLimit()
    {
        var exe = Path.Combine(_root, "qbittorrent.exe");
        File.WriteAllText(exe, "test only");
        var magnets = Enumerable.Range(0, 30).Select(i => Magnet + "&tr=" + new string('a', 1000) + i).ToArray();
        var batches = QBitTorrentService.BuildBatch(exe, _root, magnets, stopped: true);
        Assert.True(batches.Count > 1);
        Assert.Equal(magnets, batches.SelectMany(b => b.ArgumentList.Skip(3)));
        Assert.All(batches, b => Assert.Contains("--add-stopped=true", b.ArgumentList));
    }

    [Fact]
    public void TorrentQueryUsesTheChosenAlbumInsteadOfTheSong()
    {
        var url = TorrentDownloadViewModel.TorrentSearchUrl("Do I Wanna Know?",
            new MetadataCandidate { Album = "AM", Artist = "Arctic Monkeys" });
        Assert.Equal("https://ext.to/browse/?cat=3&q=Arctic%20Monkeys%20AM%20FLAC", url);
    }

    [Fact]
    public void EditingQueryDiscardsAnOldAlbumSelection()
    {
        using var vm = new TorrentDownloadViewModel("old", _root, null);
        var album = new MetadataCandidate { Album = "old album" };
        vm.Albums.Add(album);
        vm.SelectedAlbum = album;
        vm.Query = "different song";
        Assert.Null(vm.SelectedAlbum);
        Assert.Empty(vm.Albums);
    }

    [Fact]
    public void SingleSuffixIsExcludedFromTorrentSearch()
    {
        Assert.Equal("Chaar Diwaari Jhaag", TorrentDownloadViewModel.TorrentQuery("Jhaag",
            new MetadataCandidate { Artist = "Chaar Diwaari", Album = "Jhaag - Single" }));
    }
}
