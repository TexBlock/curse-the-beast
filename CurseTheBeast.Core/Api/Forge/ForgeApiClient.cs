namespace CurseTheBeast.Core.Api.Forge;


public class ForgeApiClient : BaseApiClient
{
    public ForgeApiClient()
    {

    }

    public async Task<string?> GetServerInstallerUrlAsync(string gameVersion, string forgeVersion, CancellationToken ct = default)
    {
        var url = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{gameVersion}-{forgeVersion}/forge-{gameVersion}-{forgeVersion}-installer.jar";
        if (await IsAvailableAsync(new Uri(url), ct))
            return url;

        // forge你在干什么？
        url = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{gameVersion}-{forgeVersion}-{gameVersion}/forge-{gameVersion}-{forgeVersion}-{gameVersion}-installer.jar";
        if (await IsAvailableAsync(new Uri(url), ct))
            return url;

        return null;
    }
}
