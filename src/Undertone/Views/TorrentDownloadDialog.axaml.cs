using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Undertone.ViewModels;
using Undertone.Services;
using System.Diagnostics;

namespace Undertone.Views;

public partial class TorrentDownloadDialog : Window
{
    private async void CopyTorrentLink(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: TorrentSearchItem item }
            || DataContext is not TorrentDownloadViewModel vm) return;
        try
        {
            if (Clipboard == null) { vm.Status = "Clipboard is unavailable. Use Get magnet link to show it in the batch box."; return; }
            if (string.IsNullOrWhiteSpace(item.MagnetLink)) { vm.Status = "No magnet link was available. Open Source to get the torrent file."; return; }
            await Clipboard.SetTextAsync(item.MagnetLink);
            vm.Status = "Magnet link copied.";
        }
        catch (Exception) { vm.Status = "Could not copy. Use Get magnet link to show the link in the batch box."; }
    }

    private void OpenTorrentSource(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: TorrentSearchItem item }) return;
        try { Process.Start(new ProcessStartInfo(item.PageUrl) { UseShellExecute = true }); }
        catch (Exception) { if (DataContext is TorrentDownloadViewModel vm) vm.Status = "The source page could not be opened."; }
    }

    public TorrentDownloadDialog() => InitializeComponent();

    public TorrentDownloadDialog(TorrentDownloadViewModel vm) : this()
    {
        DataContext = vm;
        Closed += (_, _) => vm.Dispose();
    }

    private async void ChooseTorrentFiles(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TorrentDownloadViewModel vm) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose torrent files", AllowMultiple = true,
            FileTypeFilter = new[] { new FilePickerFileType("Torrent files") { Patterns = new[] { "*.torrent" } } },
        });
        foreach (var file in files)
            if (file.TryGetLocalPath() is { } path && !vm.TorrentFiles.Contains(path)) vm.TorrentFiles.Add(path);
    }

    private async void ChooseFolder(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TorrentDownloadViewModel vm) return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the torrent download folder", AllowMultiple = false,
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path) vm.SaveFolder = path;
    }

    private async void ChooseExecutable(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TorrentDownloadViewModel vm) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose qbittorrent.exe", AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("qBittorrent") { Patterns = new[] { "qbittorrent.exe" } } },
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) vm.Executable = path;
    }
}
