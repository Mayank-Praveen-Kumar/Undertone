using System.Net;
using Undertone.Services;
using Undertone.ViewModels;
using Xunit;

namespace Undertone.Tests;

public class TorrentSearchTests
{
    private const string Hash = "a2448310ff58de423dff91f43fae804201fe2bd8";
    private static string Listing => $$"""<table><tr><th>Name</th><th>Size</th><th>Seeds</th></tr><tr><td><a href="/native-flac-123/">Native FLAC</a><a href="magnet:?xt=urn:btih:{{Hash}}&amp;dn=Native">Magnet</a></td><td>824.73 MB</td><td>9</td></tr></table>""";

    [Fact]
    public void ExtListingUsesPublishedFieldsAndDecodesMagnet()
    {
        var item = Assert.Single(TorrentSearchService.Parse(Listing));
        Assert.Equal("EXT.to", item.Source);
        Assert.Equal("https://ext.to/native-flac-123/", item.PageUrl);
        Assert.Equal(9, item.Seeders);
        Assert.Contains("MiB", item.SizeDisplay);
        Assert.Single(QBitTorrentService.ParseMagnets(item.MagnetLink));
        Assert.Contains("&dn=", item.MagnetLink);
    }

    [Fact]
    public void UnlistedCountsStayUnknownAndUntrustedLinksAreIgnored()
    {
        var html = Listing.Replace("<th>Seeds</th>", "<th>Other</th>").Replace("824.73 MB", "unknown")
            .Replace("/native-flac-123/", "https://evil.example/native");
        Assert.Empty(TorrentSearchService.Parse(html));
        var item = Assert.Single(TorrentSearchService.Parse(html.Replace("https://evil.example/native", "/native/")));
        Assert.Null(item.Seeders);
        Assert.Null(item.SizeBytes);
    }

    [Fact]
    public void VerificationAndUnknownPagesAreErrorsRatherThanEmptyResults()
    {
        Assert.Throws<FormatException>(() => TorrentSearchService.Parse("Performing security verification"));
        Assert.Throws<FormatException>(() => TorrentSearchService.Parse("<html>Something changed</html>"));
        Assert.Empty(TorrentSearchService.Parse("No results found"));
    }

    [Fact]
    public async Task SearchOnlyContactsExtAndReportsBlockedAccess()
    {
        using var http = new HttpClient(new ListingHandler());
        var response = await new TorrentSearchService(http).SearchAsync("Native", CancellationToken.None);
        Assert.Single(response.Items);
        Assert.Empty(response.UnavailableSources);
        using var blocked = new HttpClient(new ListingHandler(true));
        var failure = await new TorrentSearchService(blocked).SearchAsync("Native", CancellationToken.None);
        Assert.Empty(failure.Items);
        Assert.Contains("EXT.to", Assert.Single(failure.UnavailableSources));
    }

    [Fact]
    public void GetLinkDeduplicatesAndDoesNotAddUnavailableLinks()
    {
        using var vm = new TorrentDownloadViewModel("Native", "C:/Music", null);
        var item = Assert.Single(TorrentSearchService.Parse(Listing));
        vm.UseTorrentLinkCommand.Execute(item);
        vm.UseTorrentLinkCommand.Execute(item);
        Assert.Equal(item.MagnetLink, vm.MagnetLinks);
        vm.UseTorrentLinkCommand.Execute(item with { MagnetLink = "" });
        Assert.Equal(item.MagnetLink, vm.MagnetLinks);
    }

    private sealed class ListingHandler(bool blocked = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("ext.to", request.RequestUri!.Host);
            Assert.Contains("FLAC", request.RequestUri.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(blocked ? "Performing security verification" : Listing) });
        }
    }
}
