using Avalonia.Threading;
using CurseTheBeast.GUI.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CurseTheBeast.GUI.Services;

public static class LogStore
{
    const int MaxEntries = 500;
    static readonly object Sync = new();

    public static ObservableCollection<LogEntry> Entries { get; } = [];

    public static void Add(TraceEventType level, string source, string message)
    {
        var entry = new LogEntry(DateTimeOffset.Now, level, source, message);
        if (Dispatcher.UIThread.CheckAccess())
        {
            Append(entry);
        }
        else
        {
            Dispatcher.UIThread.Post(() => Append(entry));
        }
    }

    public static void Clear()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Entries.Clear();
        }
        else
        {
            Dispatcher.UIThread.Post(Entries.Clear);
        }
    }

    static void Append(LogEntry entry)
    {
        lock (Sync)
        {
            Entries.Add(entry);
            while (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(0);
            }
        }
    }
}
