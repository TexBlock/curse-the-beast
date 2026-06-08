using CurseTheBeast.Core.Mirrors;
using System.Net;

namespace CurseTheBeast.Core.Services;

public class HttpConfigService
{
    public static IWebProxy? Proxy { get; private set; }
    public static string UserAgent { get; private set; } = $"{AppInfo.Name}/{AppInfo.Version}";
    public static int Thread { get; private set; } = 8;
    public static string CurseforgeKey { get; private set; } = "$2a$10$KauzeIBqTRY2jwkx64A.Cep7cmWFGGYVncpqvfOCOee/90YPgkgfy";

    public static void SetupHttp(int? thread, string? curseforgeKey, bool noProxy, string? proxyUri, string? userAgent)
    {
        if (thread != null)
            Thread = thread.Value;
        if (curseforgeKey != null)
            CurseforgeKey = curseforgeKey;
        if (userAgent != null)
            UserAgent = userAgent;

        SetupHttpProxy(noProxy, proxyUri);
    }

    public static void SetupHttpProxy(bool noProxy, string? proxyUri)
    {
        if (noProxy)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(proxyUri))
        {
            Proxy = MirrorManager.WrapWebProxy(new WebProxy(proxyUri));
            return;
        }

        var proxy = WebRequest.DefaultWebProxy;
        if (proxy?.GetProxy(new Uri("https://api.modpacks.ch/")) != null)
        {
            Proxy = MirrorManager.WrapWebProxy(proxy);
            return;
        }

        proxyUri = Environment.GetEnvironmentVariable("HTTP_PROXY");
        if (!string.IsNullOrWhiteSpace(proxyUri))
        {
            Proxy = MirrorManager.WrapWebProxy(new WebProxy(proxyUri));
        }
    }
}
