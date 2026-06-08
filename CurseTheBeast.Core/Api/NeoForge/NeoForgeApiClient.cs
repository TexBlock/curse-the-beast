namespace CurseTheBeast.Core.Api.NeoForge;


public class NeoForgeApiClient : BaseApiClient
{
    public NeoForgeApiClient()
    {

    }

    public async Task<string?> GetServerInstallerUrlAsync(string gameVersion, string neoforgeVersion, CancellationToken ct = default)
    {
        string url;
        if (gameVersion == "1.20.1")
            url = $"https://maven.neoforged.net/net/neoforged/forge/1.20.1-{neoforgeVersion}/forge-1.20.1-{neoforgeVersion}-installer.jar";
        else
            url = $"https://maven.neoforged.net/net/neoforged/neoforge/{neoforgeVersion}/neoforge-{neoforgeVersion}-installer.jar";

        if (await IsAvailableAsync(new Uri(url), ct))
            return url;

        return null;
    }
}
