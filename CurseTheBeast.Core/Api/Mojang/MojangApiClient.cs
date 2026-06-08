using CurseTheBeast.Core.Api.Mojang.Model;

namespace CurseTheBeast.Core.Api.Mojang;


public class MojangApiClient : BaseApiClient
{
    public MojangApiClient()
    {

    }

    public async Task<GameVersions> GetGameVersionListAsync(CancellationToken ct = default)
    { 
        return await GetAsync<GameVersions>(new Uri($"https://piston-meta.mojang.com/mc/game/version_manifest.json"), GameVersions.GameVersionsContext.Default.GameVersions, ct);
    }

    public async Task<GameManifest> GetGameManifestAsync(string manifestUrl, CancellationToken ct = default)
    {
        return await GetAsync<GameManifest>(new Uri(manifestUrl), GameManifest.GameManifestContext.Default.GameManifest, ct);
    }
}
