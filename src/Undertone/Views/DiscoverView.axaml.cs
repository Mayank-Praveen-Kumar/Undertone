using Avalonia.Controls;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Undertone.Models;
using Undertone.Services.MetadataSearch;
using Undertone.ViewModels;

namespace Undertone.Views;

public partial class DiscoverView : UserControl
{
    private DiscoverViewModel? _vm;
    private Window? _dialog;
    private CancellationTokenSource? _action;
    public DiscoverView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) { _vm.PreviewRequested -= Preview; _vm.AlbumRequested -= FindAlbum; }
            _vm = DataContext as DiscoverViewModel;
            if (_vm is not null) { _vm.PreviewRequested += Preview; _vm.AlbumRequested += FindAlbum; }
        };
        DetachedFromVisualTree += (_, _) => { _action?.Cancel(); _dialog?.Close(); _vm?.SavePreferences(); };
    }

    private async void Preview(object? sender, DiscoverCard card)
    {
        if (_dialog is not null || TopLevel.GetTopLevel(this) is not Window owner || _vm is null) return;
        var dialog = new DiscoverPreviewDialog(card, _vm.Player);
        _dialog = dialog;
        try { await dialog.ShowDialog(owner); }
        finally { _dialog = null; }
    }

    private async void FindAlbum(object? sender, DiscoverCard card)
    {
        if (_action is not null || _dialog is not null || _vm is null || TopLevel.GetTopLevel(this) is not Window owner) return;
        using var action = new CancellationTokenSource();
        _action = action;
        try
        {
            _vm.Status = "Finding album details…";
            var metadata = App.Services?.GetService<IMetadataSearchService>();
            MetadataCandidate? album = null;
            if (metadata is not null)
            {
                var response = await metadata.SearchAsync(new MetadataQuery { Title = card.Title, Artist = card.Artist, Providers = new[] { "MusicBrainz" } }, action.Token);
                album = response.Candidates.FirstOrDefault(c => c.Confidence >= 0.65 && !string.IsNullOrWhiteSpace(c.Album));
            }
            action.Token.ThrowIfCancellationRequested();
            var query = album is not null ? $"{album.Artist} {album.Album}" : card.LocalTrack is { Album.Length: > 0 } local
                ? $"{local.Artist} {local.Album}" : $"{card.Artist} {card.Title}";
            card.Album = album?.Album ?? card.LocalTrack?.Album ?? "Album not identified · refine the search";
            var settings = App.Services?.GetService<MainWindowViewModel>()?.Settings.GetSettings();
            var folder = settings?.MusicFolders.FirstOrDefault(Directory.Exists) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            var torrent = new TorrentDownloadViewModel(query, folder, metadata);
            _dialog = new TorrentDownloadDialog(torrent);
            _vm.Status = album is null ? "Album not confirmed. Check the artist and album before choosing a download." : $"Album found: {album.Album}";
            await _dialog.ShowDialog(owner);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _vm.Status = "Album search could not open: " + ex.Message; }
        finally { _dialog = null; _action = null; }
    }
}
