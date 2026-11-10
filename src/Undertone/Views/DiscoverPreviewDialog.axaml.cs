using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Undertone.Controls;
using Undertone.Services;
using Undertone.Services.YouTube;
using Undertone.ViewModels;

namespace Undertone.Views;

public partial class DiscoverPreviewDialog : Window
{
    private readonly CancellationTokenSource _closed = new();
    private readonly DiscoverCard? _card;
    private readonly PlayerViewModel? _player;
    private PreviewPlaybackLease? _lease;
    private bool _busy;
    private WindowBase? _previewOwner;
    public DiscoverPreviewDialog()
    {
        InitializeComponent();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty && WindowState == Avalonia.Controls.WindowState.Minimized) Close();
        };
        Opened += (_, _) => _previewOwner = Owner;
        Closed += (_, _) =>
        {
            _closed.Cancel();
            foreach (var host in PlayerHost.Children.OfType<YouTubePreviewHost>()) host.Stop();
            PlayerHost.Children.Clear();
            if (_previewOwner?.IsVisible != true) _lease?.CancelResume();
            _lease?.Dispose(); _lease = null;
        };
    }
    public DiscoverPreviewDialog(DiscoverCard card, PlayerViewModel player) : this()
    {
        (_card, _player) = (card, player);
        SongTitle.Text = card.Title; ArtistTitle.Text = card.Artist;
        Opened += async (_, _) => await FindVideo();
    }
    private async Task FindVideo()
    {
        if (_busy || _card is null) return;
        _busy = true;
        try
        {
            var tool = App.Services?.GetService<YtDlpTool>();
            if (tool is null) throw new InvalidOperationException("Paste a YouTube link below to preview this song.");
            EnableSearchButton.IsVisible = !tool.IsAvailable;
            var video = await tool.FindPreviewVideoAsync(_card.Artist, _card.Title, _closed.Token);
            if (video is null) throw new InvalidOperationException("No matching video was found. Paste a YouTube link below.");
            if (!_closed.IsCancellationRequested) ShowVideo(video);
        }
        catch (OperationCanceledException) { if (!_closed.IsCancellationRequested) { PreviewPlaceholder.Text = "Paste a video link to preview"; PreviewStatus.Text = "YouTube search timed out. You can paste a link below."; } }
        catch (Exception ex) { PreviewPlaceholder.Text = "Enable search or paste a video link"; PreviewStatus.Text = ex.Message; }
        finally { _busy = false; }
    }
    private void UseLink(object? sender, RoutedEventArgs e)
    {
        if (_busy) { PreviewStatus.Text = "Wait for the current search to finish, then use your link."; return; }
        var id = YtDlpTool.PreviewVideoId(YouTubeLink.Text ?? "");
        if (id is null) { PreviewStatus.Text = "Enter a valid HTTPS YouTube or YouTube Music video link."; return; }
        ShowVideo(id);
    }
    private async void EnableSearch(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true; EnableSearchButton.IsEnabled = false;
        try
        {
            var tool = App.Services?.GetService<YtDlpTool>() ?? throw new InvalidOperationException("Search could not be enabled.");
            PreviewStatus.Text = "Downloading the YouTube search helper from its official release…";
            await tool.InstallAsync(null, _closed.Token);
            EnableSearchButton.IsVisible = false;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { PreviewStatus.Text = "Search setup failed: " + ex.Message; }
        finally { _busy = false; EnableSearchButton.IsEnabled = true; }
        if (!_closed.IsCancellationRequested && App.Services?.GetService<YtDlpTool>()?.IsAvailable == true) await FindVideo();
    }
    private void ShowVideo(string id)
    {
        if (_player is null || _closed.IsCancellationRequested) return;
        foreach (var old in PlayerHost.Children.OfType<YouTubePreviewHost>()) old.Stop();
        PlayerHost.Children.Clear();
        if (_lease is null)
        {
            _lease = new PreviewPlaybackLease(_player);
            _lease.PlaybackTakenOver += () => Close();
        }
        var host = new YouTubePreviewHost { VideoId = id };
        host.StatusChanged += status => PreviewStatus.Text = status;
        host.Finished += () => Close();
        host.Failed += () => { _lease?.Dispose(); _lease = null; };
        PlayerHost.Children.Add(host);
    }
    private void ClosePreview(object? sender, RoutedEventArgs e) => Close();
}
