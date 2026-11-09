using Avalonia.Controls;
using Undertone.Plugins.AlbumTown.Town;
using Undertone.Plugins.AlbumTown.Views;

namespace Undertone.Plugins.AlbumTown;

/// <summary>
/// Album Town: your albums as little square creatures. Settings → Plugins → Album Town →
/// "Open Album Town" opens the town in a window of its own. By Jafezy JD (Undertone Discord).
/// </summary>
public sealed class AlbumTownPlugin : IUndertonePlugin, IPluginViewProvider
{
    public const string Id = "dev.undertone.plugins.albumtown";
    public const string Author = "Jafezy JD";
    public const string Version = "1.0.2";
    public const string About = "Your albums come to life in a little town.";

    private IPluginHost? _host;
    private IDisposable? _view;
    private readonly List<PluginTownHost> _townHosts = new();

    public PluginInfo Info { get; } = new(Id, "Album Town", Version, Author, About);

    public string Name => "Album Town";

    public void Initialize(IPluginHost host)
    {
        _host = host;
        _view = host.RegisterView(this);
    }

    public Control CreateView()
    {
        var host = _host ?? throw new InvalidOperationException("Album Town is not running.");
        var town = new PluginTownHost(host);
        _townHosts.Add(town);
        var view = new AlbumTownView(town);
        // The window closed: drop the host subscriptions this view held.
        view.DetachedFromVisualTree += (_, _) =>
        {
            town.Dispose();
            _townHosts.Remove(town);
            view.ReleaseCovers();
        };
        return view;
    }

    public void Shutdown()
    {
        _view?.Dispose();
        _view = null;
        foreach (var t in _townHosts) t.Dispose();
        _townHosts.Clear();
        _host = null;
    }
}
