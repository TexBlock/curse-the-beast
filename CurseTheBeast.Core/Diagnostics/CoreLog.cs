using System.Diagnostics;

namespace CurseTheBeast.Core.Diagnostics;

public sealed record CoreLogEntry(DateTimeOffset Timestamp, TraceEventType Level, string Source, string Message);

public static class CoreLog
{
    public static event Action<CoreLogEntry>? MessageEmitted;

    public static void Write(TraceEventType level, string source, string message)
    {
        MessageEmitted?.Invoke(new CoreLogEntry(DateTimeOffset.Now, level, source, message));
    }

    public static void Info(string source, string message) => Write(TraceEventType.Information, source, message);
    public static void Warn(string source, string message) => Write(TraceEventType.Warning, source, message);
    public static void Error(string source, string message) => Write(TraceEventType.Error, source, message);
    public static void Verbose(string source, string message) => Write(TraceEventType.Verbose, source, message);
}
