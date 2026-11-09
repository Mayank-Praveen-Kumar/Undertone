using System.Net;
using Undertone.Services;
using Xunit;
namespace Undertone.Tests;

public class QBitTorrentApiTests
{
    private const string Hash = "0123456789abcdef0123456789abcdef01234567";
    [Fact]
    public async Task LoginPollingAndControlsUseAuthenticatedLoopbackAndOneHash()
    {
        var handler = new Handler();
        using var api = new QBitTorrentApiClient("test", "secret", new HttpClient(handler));
        var item = Assert.Single(await api.GetDownloadsAsync(CancellationToken.None));
        Assert.Equal(25, item.Percent);
        Assert.Equal("Downloading", item.StateLabel);
        Assert.Contains("1.00 MiB/s", item.ProgressLabel);
        await api.SetPausedAsync(item.Hash, true, CancellationToken.None);
        await api.SetPausedAsync(item.Hash, false, CancellationToken.None);
        Assert.Equal(new[] { "/api/v2/auth/login", "/api/v2/torrents/info", "/api/v2/torrents/stop", "/api/v2/torrents/start" }, handler.Paths);
        await Assert.ThrowsAsync<ArgumentException>(() => api.SetPausedAsync("all", true, CancellationToken.None));
        Assert.Equal(4, handler.Paths.Count);
    }
    [Fact]
    public async Task RejectedAuthenticationDoesNotFetchOrControlDownloads()
    {
        var handler = new Handler { RejectLogin = true };
        using var api = new QBitTorrentApiClient("test", "wrong", new HttpClient(handler));
        await Assert.ThrowsAsync<IOException>(() => api.GetDownloadsAsync(CancellationToken.None));
        Assert.Single(handler.Paths);
    }
    [Theory]
    [InlineData("stoppedDL", "Paused")]
    [InlineData("stalledDL", "Waiting for peers")]
    [InlineData("uploading", "Completed · seeding")]
    public void ProgressReportsQbitStateAndUnknownEta(string state, string label)
    {
        var item = new QBitDownloadItem { State = state, Eta = 8640000 };
        Assert.Equal(label, item.StateLabel);
        Assert.Equal("Time remaining unknown", item.EtaLabel);
    }
    [Fact]
    public async Task Version53AcceptsNoContentLoginResponse()
    {
        using var api = new QBitTorrentApiClient("test", "secret", new HttpClient(new Handler { NoContentLogin = true }));
        Assert.Single(await api.GetDownloadsAsync(CancellationToken.None));
    }
    private sealed class Handler : HttpMessageHandler
    {
        public bool RejectLogin;
        public bool NoContentLogin;
        public List<string> Paths { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("127.0.0.1", request.RequestUri!.Host);
            Assert.Equal(18743, request.RequestUri.Port);
            Assert.Equal("http://127.0.0.1:18743/", request.Headers.Referrer!.AbsoluteUri);
            var path = request.RequestUri.AbsolutePath;
            Paths.Add(path);
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);
            if (path.EndsWith("/stop") || path.EndsWith("/start")) Assert.Equal("hashes=" + Hash, body);
            if (path.EndsWith("/login") && NoContentLogin) return new(HttpStatusCode.NoContent);
            var response = path.EndsWith("/login") ? (RejectLogin ? "Fails." : "Ok.") : path.EndsWith("/info")
                ? $$"""[{"hash":"{{Hash}}","name":"Test download","progress":0.25,"dlspeed":1048576,"state":"downloading","eta":120,"category":"Undertone"}]""" : "";
            return new(HttpStatusCode.OK) { Content = new StringContent(response) };
        }
    }
}
