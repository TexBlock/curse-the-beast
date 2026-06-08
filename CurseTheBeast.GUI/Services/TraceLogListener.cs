using System.Diagnostics;

namespace CurseTheBeast.GUI.Services;

public sealed class TraceLogListener : TraceListener
{
    readonly string _source;

    public TraceLogListener(string source = "Trace")
    {
        _source = source;
    }

    public override void Write(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            LogStore.Add(TraceEventType.Information, _source, message.TrimEnd());
        }
    }

    public override void WriteLine(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            LogStore.Add(TraceEventType.Information, _source, message.TrimEnd());
        }
    }

    public override void TraceEvent(TraceEventCache? eventCache, string? source, TraceEventType eventType, int id, string? message)
    {
        var text = string.IsNullOrWhiteSpace(message) ? $"Event {id}" : message;
        LogStore.Add(eventType, source ?? _source, text);
    }

    public override void TraceData(TraceEventCache? eventCache, string? source, TraceEventType eventType, int id, object? data)
    {
        var text = data?.ToString() ?? $"Event {id}";
        LogStore.Add(eventType, source ?? _source, text);
    }
}
