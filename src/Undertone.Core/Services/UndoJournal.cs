using System.Collections.ObjectModel;

namespace Undertone.Services;

public sealed class UndoEntry(string label, Func<Task> undo, Func<bool>? valid)
{
    public string Label { get; } = label;
    public DateTime CreatedAt { get; } = DateTime.Now;
    internal Func<Task> Undo { get; } = undo;
    internal bool IsValid => valid?.Invoke() ?? true;
}

/// <summary>Bounded, in-memory actions. Failed undo remains available for retry.</summary>
public sealed class UndoJournal
{
    public static UndoJournal Shared { get; } = new();
    public ObservableCollection<UndoEntry> Entries { get; } = new();
    public bool IsBusy { get; private set; }
    public event EventHandler? Changed;
    public void Record(string label, Func<Task> undo, Func<bool>? valid = null)
    {
        if (IsBusy) return;
        Entries.Insert(0, new UndoEntry(label, undo, valid));
        while (Entries.Count > 20) Entries.RemoveAt(Entries.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Prune()
    {
        // Older entries can become valid again after the newest action is
        // undone (for example two consecutive removals from one playlist).
        while (Entries.Count > 0 && !Entries[0].IsValid) Entries.RemoveAt(0);
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public async Task<bool> UndoAsync()
    {
        if (IsBusy) return false;
        Prune();
        if (Entries.Count == 0) return false;
        var entry = Entries[0];
        IsBusy = true;
        Changed?.Invoke(this, EventArgs.Empty);
        try
        {
            await entry.Undo();
            Entries.Remove(entry);
            Prune();
            return true;
        }
        finally { IsBusy = false; Changed?.Invoke(this, EventArgs.Empty); }
    }
}
