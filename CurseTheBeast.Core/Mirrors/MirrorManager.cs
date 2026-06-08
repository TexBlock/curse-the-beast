using CurseTheBeast.Core.Services;
using System.Net;

namespace CurseTheBeast.Core.Mirrors;


public static class MirrorManager
{
    static readonly IReadOnlyList<IMirror> Mirrors = new IMirror[]
    {
        BmclMirror.Instance,
        Lss233Mirror.Instance,
    };

    public static IEnumerable<Uri> GetUrls(Uri uri)
    {
        if (HttpConfigService.Proxy != null)
            return Mirrors.Where(m => !m.CN).SelectMany(m => m.GetMirrors(uri)).Append(uri);
        else
            return Mirrors.SelectMany(m => m.GetMirrors(uri)).Append(uri);
    }
    public static IWebProxy WrapWebProxy(IWebProxy proxy) => proxy; /* new BypassMirrorProxy(proxy); */

    class BypassMirrorProxy : IWebProxy
    {
        private readonly IWebProxy _internalProxy;

        public BypassMirrorProxy(IWebProxy internalProxy)
        {
            _internalProxy = internalProxy;
        }

        public ICredentials? Credentials
        {
            get => _internalProxy.Credentials;
            set => _internalProxy.Credentials = value;
        }

        public Uri? GetProxy(Uri destination) => _internalProxy.GetProxy(destination);

        public bool IsBypassed(Uri host) => Mirrors.Where(m => m.CN).Any(m => m.Hit(host)) || _internalProxy.IsBypassed(host);
    }
}
