using CurseTheBeast.Api.Curseforge;
using CurseTheBeast.Services.Model;

namespace CurseTheBeast.Services;


public class CurseforgeService
{
    private static readonly CurseforgeApiClient Api = new CurseforgeApiClient();

    [Obsolete]
    public static async Task FetchModInfoAsync(IEnumerable<FTBFileEntry> modFiles, CancellationToken ct = default)
    {
        await Focused.StatusAsync($"获取 Curseforge 模组信息", async ctx =>
        {
            var fileDict = modFiles.ToDictionary(f => f.Sha1!);
            if (fileDict.Count == 0) 
                return;
            var result = await Api.MatchFilesAsync(fileDict.Values.Select(f => f.CFMurmur), ct);
            foreach (var matchedFile in result.exactMatches)
            {
                var sha1 = matchedFile.file.hashes.FirstOrDefault(h => h.algo == 1)?.value;
                if (sha1 != null && fileDict.TryGetValue(sha1, out var file))
                {
                    file.WithCurseforgeInfo(matchedFile.file.modId, matchedFile.file.id);
                }
            }
        });
    }
}
