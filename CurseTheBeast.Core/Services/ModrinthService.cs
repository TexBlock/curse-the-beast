using CurseTheBeast.Core.Api.Modrinth;
using CurseTheBeast.Core.Diagnostics;
using CurseTheBeast.Core.Services.Model;
using CurseTheBeast.Core.Storage;
using ModrinthCache = CurseTheBeast.Core.Services.Model.ModrinthCache;

namespace CurseTheBeast.Core.Services;

public class ModrinthService : IDisposable
{
    private readonly ModrinthApiClient api = new();

    public async Task FetchModFileUrlAsync(IEnumerable<FTBFileEntry> modFileEnum, CancellationToken ct = default)
    {
        CoreLog.Info(nameof(ModrinthService), "Resolving Modrinth download URLs.");
        await LocalStorage.Persistent.GetOrUpdateObject<ModrinthCache>("modrinth-file", async cache =>
        {
            var requestMods = cache == null ? modFileEnum.ToHashSet() : modFileEnum.Where(m =>
            {
                if (!cache.Urls.TryGetValue(m.Sha1!, out var url))
                    return true;
                if (!string.IsNullOrWhiteSpace(url))
                {
                    m.SetDownloadable(m.DisplayName!, [url, .. m.Urls]);
                    CoreLog.Verbose(nameof(ModrinthService), $"Cache hit for {m.DisplayName}.");
                }
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
                            CoreLog.Verbose(nameof(ModrinthService), $"Resolved {file.DisplayName}.");
                        }
                    }
                }
            }
            foreach (var missedMods in requestMods)
                cache.Urls.TryAdd(missedMods.Sha1!, null);

            return cache;
        }, ModrinthCache.ModrinthCacheContext.Default.ModrinthCache);
        CoreLog.Info(nameof(ModrinthService), "Modrinth URL resolution finished.");
    }

    public void Dispose()
    {
        api.Dispose();
    }
}
