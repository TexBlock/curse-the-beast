using System;
using System.Diagnostics;

namespace CurseTheBeast.GUI.Models;

public sealed class LogEntry
{
    public DateTimeOffset Timestamp { get; }
    public TraceEventType Level { get; }
    public string Source { get; }
    public string Message { get; }

    public string TimeText => Timestamp.ToLocalTime().ToString("HH:mm:ss");
    public string LevelText => Level.ToString();

    public LogEntry(DateTimeOffset timestamp, TraceEventType level, string source, string message)
    {
        Timestamp = timestamp;
        Level = level;
        Source = source;
        Message = message;
    }
}
