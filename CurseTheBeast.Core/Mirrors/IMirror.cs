using System.Collections.Concurrent;

namespace CurseTheBeast.Core.Mirrors;


public interface IMirror
{
    bool CN { get; }
    IEnumerable<Uri> GetMirrors(Uri originalUri);
    bool Hit(Uri originalUri);
}

public abstract class HostReplacementMirror : IMirror
{
    public virtual bool CN => true;

    readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _replaceTable;

    public HostReplacementMirror(IReadOnlyDictionary<string, IReadOnlyList<string>> replaceTable)
    {
        _replaceTable = new ConcurrentDictionary<string, IReadOnlyList<string>>(replaceTable);
    }

    public HostReplacementMirror(IReadOnlyDictionary<IReadOnlyList<string>, IReadOnlyList<string>> replaceTable)
    {
        _replaceTable = new ConcurrentDictionary<string, IReadOnlyList<string>>(replaceTable
            .SelectMany(pair => pair.Key.Select(k => (k, v: pair.Value)))
            .ToDictionary(pair => pair.k, pair => pair.v));
    }

    public HostReplacementMirror(IReadOnlyDictionary<IReadOnlyList<string>, string> replaceTable)
    {
        _replaceTable = new ConcurrentDictionary<string, IReadOnlyList<string>>(replaceTable
            .SelectMany(pair => pair.Key.Select(k => (k, v: pair.Value)))
            .ToDictionary(pair => pair.k, pair => new[] { pair.v } as IReadOnlyList<string>));
    }

    public HostReplacementMirror(string originalHost, params string[] mirrorHost)
    {
        _replaceTable = new ConcurrentDictionary<string, IReadOnlyList<string>>() 
        {
            [originalHost] = mirrorHost
        };
    }

    public HostReplacementMirror(string[] originalHost, params string[] mirrorHost)
    {
        _replaceTable = new ConcurrentDictionary<string, IReadOnlyList<string>>(
            originalHost.ToDictionary(h => h, _ => mirrorHost as IReadOnlyList<string>));
    }

    public virtual bool Hit(Uri originalUri)
    {
        return _replaceTable.ContainsKey(originalUri.Host);
    }

    public virtual IEnumerable<Uri> GetMirrors(Uri originalUri)
    {
        if (!_replaceTable.TryGetValue(originalUri.Host, out var hostList))
            return Enumerable.Empty<Uri>();
        return hostList.Select(host => ReplaceHost(originalUri, host));
    }

    protected static Uri ReplaceHost(Uri originalUri, string newHost) 
        => new Uri($"{originalUri.Scheme}://{newHost}{originalUri.PathAndQuery}");
}