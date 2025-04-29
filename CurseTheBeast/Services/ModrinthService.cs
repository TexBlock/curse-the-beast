using CurseTheBeast.Api.Modrinth;
using CurseTheBeast.Services.Model;
using CurseTheBeast.Storage;

namespace CurseTheBeast.Services;

public class ModrinthService : IDisposable
{
    private readonly ModrinthApiClient api = new();

    public async Task FetchModFileUrlAsync(IEnumerable<FTBFileEntry> modFileEnum, CancellationToken ct = default)
    {
        await Focused.StatusAsync($"获取 Modrinth 模组链接", async ctx =>
        {
            await LocalStorage.Persistent.GetOrUpdateObject<ModrinthCache>("modrinth-file", async cache =>
            {
                var requestMods = cache == null ? modFileEnum.ToHashSet() : modFileEnum.Where(m =>
                {
                    if (!cache.Urls.TryGetValue(m.Sha1!, out var url))
                        return true;
                    if (!string.IsNullOrWhiteSpace(url))
                        m.SetDownloadable(m.DisplayName!, [url, .. m.Urls]);
                    return false;
                }).ToHashSet();

                cache ??= new();

                if (requestMods.Count > 0)
                {
                    var result = await api.MatchFilesAsync(requestMods.Select(f => f.Sha1!), ct);
                    foreach (var file in requestMods)
                    {
                        if (result.TryGetValue(file.Sha1!, out var matchedInfo))
                        {
                            var matchedFile = matchedInfo.files.FirstOrDefault(f => f.hashes.sha1 == file.Sha1);
                            if (matchedFile != null && requestMods.Remove(file))
                            {
                                file.SetDownloadable(file.DisplayName!, [matchedFile.url, .. file.Urls]);
                                cache.Urls.TryAdd(file.Sha1!, matchedFile.url);
                            }
                        }
                    }
                }
                foreach (var missedMods in requestMods)
                    cache.Urls.TryAdd(missedMods.Sha1!, null);

                return cache;
            }, ModrinthCache.ModrinthCacheContext.Default.ModrinthCache);
        });
    }

    public void Dispose()
    {
        api.Dispose();
    }
}
