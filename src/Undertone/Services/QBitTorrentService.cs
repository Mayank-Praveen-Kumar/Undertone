using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Undertone.Services;

/// <summary>Passes validated torrent batches to the installed desktop client.</summary>
public static class QBitTorrentService
{
    public static string FindExecutable()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        };
        return roots.Select(root => Path.Combine(root, "qBittorrent", "qbittorrent.exe"))
            .FirstOrDefault(File.Exists) ?? string.Empty;
    }

    public static IReadOnlyList<string> ParseMagnets(string text)
    {
        var links = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var link in links)
        {
            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme != "magnet"
                || !Regex.IsMatch(Uri.UnescapeDataString(uri.Query.TrimStart('?')),
                    @"(^|&)xt=urn:(btih:([a-f0-9]{40}|[a-z2-7]{32})|btmh:1220[a-f0-9]{64})(&|$)", RegexOptions.IgnoreCase))
                throw new ArgumentException("Paste complete magnet links, one per line.");
        }
        return links.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<ProcessStartInfo> BuildBatch(string executable, string saveFolder,
        IEnumerable<string> torrents, bool stopped = false)
    {
        if (!File.Exists(executable) || !Path.GetFileName(executable).Equals("qbittorrent.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose the installed qbittorrent.exe first.");
        if (string.IsNullOrWhiteSpace(saveFolder)) throw new ArgumentException("Choose a download folder first.");
        var folder = Path.GetFullPath(saveFolder);
        var items = torrents.Distinct(StringComparer.Ordinal).ToArray();
        if (items.Length == 0) throw new ArgumentException("Paste a magnet link or choose a .torrent file first.");
        foreach (var item in items)
        {
            if (item.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase)) ParseMagnets(item);
            else if (!Path.IsPathFullyQualified(item) || !File.Exists(item)
                || !Path.GetExtension(item).Equals(".torrent", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Only magnet links and existing .torrent files can be added.");
            if (item.Length > 20000) throw new ArgumentException("A torrent link is too long to send to qBittorrent.");
        }

        ProcessStartInfo NewBatch()
        {
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = false, WindowStyle = ProcessWindowStyle.Minimized };
            start.ArgumentList.Add("--skip-dialog=true");
            start.ArgumentList.Add($"--add-stopped={stopped.ToString().ToLowerInvariant()}");
            start.ArgumentList.Add($"--save-path={folder}");
            return start;
        }

        var batches = new List<ProcessStartInfo>();
        var current = NewBatch();
        var length = executable.Length + current.ArgumentList.Sum(a => a.Length * 2 + 3);
        var baseLength = length;
        foreach (var item in items)
        {
            var cost = item.Length * 2 + 3;
            if (cost + baseLength > 30000) throw new ArgumentException("A torrent link is too long to send to qBittorrent.");
            if (length + cost > 30000)
            {
                batches.Add(current);
                current = NewBatch();
                length = baseLength;
            }
            current.ArgumentList.Add(item);
            length += cost;
        }
        batches.Add(current);
        return batches;
    }

    public static ProcessStartInfo BuildNativeLaunch(string executable)
    {
        if (!File.Exists(executable) || !Path.GetFileName(executable).Equals("qbittorrent.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose the installed qbittorrent.exe first.");
        // With no torrent arguments, qBittorrent activates its existing window via
        // its own single-instance IPC rather than starting another download client.
        return new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = false, WindowStyle = ProcessWindowStyle.Normal };
    }

    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);

    public static async Task ShowNativeAsync(string executable)
    {
        var launch = BuildNativeLaunch(executable);
        using var started = Process.Start(launch) ?? throw new IOException("qBittorrent could not be opened.");
        if (!OperatingSystem.IsWindows()) return;
        // A running Qt instance can remain minimized after its activation message.
        // Restore only the window belonging to the chosen qBittorrent executable.
        for (var attempt = 0; attempt < 60; attempt++)
        {
            foreach (var process in Process.GetProcessesByName("qbittorrent"))
            {
                using (process)
                {
                    try
                    {
                        if (!string.Equals(process.MainModule?.FileName, Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase)) continue;
                        var window = process.MainWindowHandle;
                        if (window == IntPtr.Zero) continue;
                        ShowWindowAsync(window, 9); // SW_RESTORE
                        SetForegroundWindow(window);
                        return;
                    }
                    catch (System.ComponentModel.Win32Exception) { }
                    catch (InvalidOperationException) { }
                }
            }
            await Task.Delay(250);
        }
        throw new IOException("qBittorrent is starting. Try Show native again in a moment.");
    }

    public static void Send(string executable, string saveFolder, IEnumerable<string> torrents)
    {
        var batches = BuildBatch(executable, saveFolder, torrents);
        Directory.CreateDirectory(Path.GetFullPath(saveFolder));
        foreach (var batch in batches)
        {
            batch.ArgumentList.Insert(3, "--category=Undertone");
            using var process = Process.Start(batch) ?? throw new IOException("qBittorrent could not be opened.");
        }
    }
}
