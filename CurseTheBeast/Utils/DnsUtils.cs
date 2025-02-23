using System.Net;
using TurnerSoftware.DinoDNS.Protocol;
using TurnerSoftware.DinoDNS;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace CurseTheBeast.Utils;


public class DnsUtils
{
    private static readonly ConcurrentDictionary<string, (List<IPAddress> Addr, SemaphoreSlim Lock)> _cache = new();
    private static readonly DnsClient _client = new (
        [
            NameServers.Cloudflare.IPv4.GetPrimary(ConnectionType.DoH),
            new NameServer(IPAddress.Parse("208.67.222.222"), ConnectionType.DoH),  // OpenDNS
            new NameServer(IPAddress.Parse("223.5.5.5"), ConnectionType.DoH),       // 阿里
            new NameServer(IPAddress.Parse("119.29.29.29"), ConnectionType.Udp),    // 腾讯
        ], DnsMessageOptions.Default);

    public static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>>? ConnectCallback { get; } = async (ctx, ct) =>
    {
        if (!IPAddress.TryParse(ctx.DnsEndPoint.Host, out var ipAddress))
            ipAddress = await ResolveAsync(ctx.DnsEndPoint.Host, ct);
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };
        try
        {
            await socket.ConnectAsync(ipAddress, ctx.DnsEndPoint.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    };

    public static async ValueTask<IPAddress> ResolveAsync(string host, CancellationToken ct)
    {
        var entry = _cache.GetOrAdd(host, key => ([], new(1)));
        if (entry.Addr.Count == 0)
        {
            await entry.Lock.WaitAsync(ct);
            if (entry.Addr.Count == 0)
            {
                await DoResolveAsync(host, entry.Addr, ct);
            }
            entry.Lock.Release();
        }

        if (entry.Addr.Count == 1)
            return entry.Addr[0];
        else
            return entry.Addr[System.Random.Shared.Next(entry.Addr.Count)];
    }

    private static async ValueTask DoResolveAsync(string host, IList<IPAddress> list, CancellationToken ct)
    {
        var rsp = await _client.QueryAsync(host, DnsQueryType.A, cancellationToken: ct);
        if (host != "localhost")
        {
            foreach (var record in rsp.Answers.WithARecords())
                list.Add(record.ToIPAddress());
        }
        if (list.Count == 0)
        {
            foreach (var addr in await Dns.GetHostAddressesAsync(host, ct))
                list.Add(addr);
        }
        if (list.Count == 0)
            throw new Exception("无法解析域名 " + host);
    }
}
