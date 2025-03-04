using CurseTheBeast.Api.Modrinth;
using CurseTheBeast.Services.Model;

namespace CurseTheBeast.Services;

public class ModrinthService
{
    private static readonly ModrinthApiClient api = new();

    public static async Task FetchModFileUrlAsync(IEnumerable<FTBFileEntry> modFileEnum, CancellationToken ct = default)
    {
        await Focused.StatusAsync($"获取 Modrinth 模组链接", async ctx =>
        {
            var modFiles = modFileEnum.Where(f => !f.Validate()).ToHashSet();
            if (modFiles.Count == 0)
                return;
            var result = await api.MatchFilesAsync(modFiles.Select(f => f.Sha1!), ct);
            foreach (var file in modFiles)
            {
                if (result.TryGetValue(file.Sha1!, out var matchedInfo))
                {
                    var matchedFile = matchedInfo.files.FirstOrDefault(f => f.hashes.sha1 == file.Sha1);
                    if (matchedFile != null && modFiles.Remove(file))
                    {
                        file.SetDownloadable(file.DisplayName!, [matchedFile.url, .. file.Urls]);
                    }
                }
            }
        });
    }
}
