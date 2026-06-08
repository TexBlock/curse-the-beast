using CurseTheBeast.Core.Api.Fabric.Model;

namespace CurseTheBeast.Core.Api.Fabric;


public class FabricApiClient : BaseApiClient
{
    public FabricApiClient()
    {

    }

    public Task<ServerManifest> GetServerManifestAsync(string gameVersion, string fabricVersion, CancellationToken ct)
    {
        return GetAsync<ServerManifest>(new Uri($"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}/{fabricVersion}/server/json"), ServerManifest.ServerManifestContext.Default.ServerManifest, ct);
    }
    
    public async Task<InstallerMetadata[]> GetInstallerMetaAsync(CancellationToken ct = default)
    { 
        return await GetAsync<InstallerMetadata[]>(new Uri($"https://meta.fabricmc.net/v2/versions/installer"), InstallerMetadata.InstallerMetadataArrayContext.Default.InstallerMetadataArray, ct);
    }

    public async Task<string?> GetServerLoaderUrlAsync(string gameVersion, string fabricVersion, string fabricInstallerVersion, CancellationToken ct = default)
    {
        var url = $"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}/{fabricVersion}/{fabricInstallerVersion}/server/jar";
        if (await IsAvailableAsync(new Uri(url), ct))
            return url;
        else
            return null;
    }
}
