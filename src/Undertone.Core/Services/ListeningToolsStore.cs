using System.Text.Json;
using Undertone.Models;

namespace Undertone.Services;

public sealed class SessionTrack
{
    public Guid Id { get; set; }
    public string Path { get; set; } = "";
}
public sealed class NamedListeningSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public DateTime SavedAt { get; set; } = DateTime.Now;
    public double PositionSeconds { get; set; }
    public bool Shuffle { get; set; }
    public RepeatMode Repeat { get; set; }
    public List<SessionTrack> Tracks { get; set; } = new();
    public string Detail => $"{Tracks.Count} {(Tracks.Count == 1 ? "song" : "songs")} · saved {SavedAt:g}";
    public override string ToString() => Name;
}
public sealed class ListeningToolsState
{
    public bool LowPowerMode { get; set; }
    public List<NamedListeningSession> Sessions { get; set; } = new();
}
public sealed class ListeningToolsStore(string dataDirectory)
{
    private readonly string _path = Path.Combine(dataDirectory, "listening-tools.json");
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task<ListeningToolsState> LoadAsync()
    {
        if (!File.Exists(_path)) return new();
        await using var file = File.OpenRead(_path);
        var state = await JsonSerializer.DeserializeAsync<ListeningToolsState>(file)
            ?? throw new InvalidDataException("Saved listening tools are empty.");
        if (state.Sessions == null || state.Sessions.Any(s => s == null || s.Tracks == null
            || s.Id == Guid.Empty || string.IsNullOrWhiteSpace(s.Name) || s.Name.Length > 80
            || !double.IsFinite(s.PositionSeconds) || s.PositionSeconds < 0
            || s.Tracks.Count == 0 || s.Tracks.Any(t => t == null || t.Id == Guid.Empty)))
            throw new InvalidDataException("Saved sessions are invalid.");
        return state;
    }
    public async Task SaveAsync(ListeningToolsState state)
    {
        await _gate.WaitAsync();
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await using (var file = File.Create(temp))
                await JsonSerializer.SerializeAsync(file, state, new JsonSerializerOptions { WriteIndented = true });
            File.Move(temp, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
            _gate.Release();
        }
    }
}
