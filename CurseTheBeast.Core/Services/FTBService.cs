using CurseTheBeast.Core.Api.FTB;
using CurseTheBeast.Core.Api.FTB.Model;
using CurseTheBeast.Core.Diagnostics;
using CurseTheBeast.Core.Packs;
using CurseTheBeast.Core.Services.Model;
using CurseTheBeast.Core.Storage;
using ModpackCache = CurseTheBeast.Core.Services.Model.ModpackCache;

namespace CurseTheBeast.Core.Services;

public class FTBService : IDisposable
{
    static readonly IReadOnlySet<int> BlackList = new HashSet<int>()
    {
        81,
        104,
        116,
        105
    };

    readonly FTBApiClient _ftb;

    public FTBService()
    {
        _ftb = new FTBApiClient();
    }

    public async Task<IReadOnlyList<(int Id, string Name)>> GetFeaturedModpacksAsync(CancellationToken ct = default)
    {
        CoreLog.Info(nameof(FTBService), "Loading featured modpacks.");
        var result = new List<(int, string)>();
        var featuredPackIds = (await _ftb.GetFeaturedAsync(ct)).packs.ToHashSet();

        await LocalStorage.Persistent.GetOrUpdateObject("list", async cache =>
        {
            cache ??= new();
            foreach (var (id, item) in cache.Items)
            {
                if (featuredPackIds.Remove(id))
                    result.Add((id, item.Name));
            }

            foreach (var id in featuredPackIds)
            {
                CoreLog.Verbose(nameof(FTBService), $"Fetching featured modpack #{id}.");
                var pack = await _ftb.GetInfoAsync(id, ct);
                cache.Items[pack.id] = new() { Name = pack.name };
                result.Add((pack.id, pack.name));
            }
            return cache;
        }, ModpackCache.ModpackCacheContext.Default.ModpackCache, ct);

        CoreLog.Info(nameof(FTBService), $"Featured modpacks ready: {result.Count} item(s).");
        return result;
    }

    public async Task<IReadOnlyList<(int Id, string Name)>> ListAsync(bool autoClear, CancellationToken ct)
    {
        CoreLog.Info(nameof(FTBService), "Loading full modpack list.");
        var idList = await _ftb.GetListAsync(ct);
        var cache = await LocalStorage.Persistent.GetObjectAsync<ModpackCache>("list", ModpackCache.ModpackCacheContext.Default.ModpackCache);
        cache ??= new();

        foreach (var id in idList.packs)
        {
            if (BlackList.Contains(id))
                continue;

            if (!cache.Items.ContainsKey(id))
            {
                CoreLog.Verbose(nameof(FTBService), $"Fetching modpack info #{id}.");
                var info = await _ftb.GetInfoAsync(id, ct);
                cache.Items[id] = new() { Name = info.name };
            }
        }

        await LocalStorage.Persistent.SaveObjectAsync("list", cache, ModpackCache.ModpackCacheContext.Default.ModpackCache, ct);
        CoreLog.Info(nameof(FTBService), $"Full modpack list ready: {cache.Items.Count} cached item(s).");
        return cache.Items
            .Where(item => !BlackList.Contains(item.Key))
            .Select(pair => (pair.Key, pair.Value.Name))
            .ToArray();
    }

    public async Task<IReadOnlyList<(int Id, string Name)>> SearchAsync(string keyword, CancellationToken ct = default)
    {
        CoreLog.Info(nameof(FTBService), $"Searching modpacks with keyword \"{keyword}\".");
        var result = await _ftb.SearchAsync(keyword, ct);
        var items = result.packs?.Select(p => (p.id, p.name)).ToArray() ?? [];
        CoreLog.Info(nameof(FTBService), $"Search finished: {items.Length} result(s).");
        return items;
    }

    public Task<ModpackInfo> GetModpackInfoAsync(int modpackId, CancellationToken ct = default)
    {
        CoreLog.Info(nameof(FTBService), $"Loading modpack info #{modpackId}.");
        return _ftb.GetInfoAsync(modpackId, ct);
    }

    public async Task<FTBModpack> GetModpackAsync(int modpackId, int versionId, CancellationToken ct = default)
    {
        return await GetModpackAsync(await GetModpackInfoAsync(modpackId, ct), versionId, ct);
    }

    public async Task<FTBModpack> GetModpackAsync(ModpackInfo info, int versionId, CancellationToken ct = default)
    {
        CoreLog.Info(nameof(FTBService), $"Preparing modpack {info.id} version {versionId}.");
        var version = info.versions.FirstOrDefault(v => v.id == versionId) ?? throw new Exception("Version id 不正确");

        var manifest = await LocalStorage.Persistent.GetOrUpdateObject($"manifest-{info.id}-{versionId}",
            async current =>
            {
                if (current?.files.All(f => f.hashes?.sha512 != null) == true)
                    return current;
                return await _ftb.GetManifestAsync(info.id, versionId, ct);
            },
            ModpackManifest.ModpackManifestContext.Default.ModpackManifest, ct);

        var files = manifest.files.Select(f => new FTBFileEntry(f)).ToArray();
        var iconFile = info.art.FirstOrDefault(a => a.type == "square");

        return new FTBModpack()
        {
            Id = info.id,
            Name = info.name,
            Authors = info.authors.Select(a => a.name).ToArray(),
            Summary = info.synopsis,
            ReadMe = info.description,
            HomePageUrl = $"https://www.feed-the-beast.com/modpacks/" + info.id,
            Icon = iconFile == null ? null : new FileEntry(RepoType.Icon, info.id.ToString())
                .WithArchiveEntryName("icon.png")
                .WithSize(iconFile.size == 0 ? null : iconFile.size)
                .SetUnrequired()
                .SetDownloadable("icon.png", iconFile.url),
            Version = new()
            {
                Id = manifest.id,
                Name = manifest.name,
                Type = version.type,
            },
            Runtime = new()
            {
                GameVersion = manifest.targets.First(t => t.type.Equals("game", StringComparison.OrdinalIgnoreCase)).version,
                ModLoaderType = manifest.targets.First(t => t.type.Equals("modloader", StringComparison.OrdinalIgnoreCase)).name,
                ModLoaderVersion = manifest.targets.First(t => t.type.Equals("modloader", StringComparison.OrdinalIgnoreCase)).version,
                JavaVersion = manifest.targets.FirstOrDefault(t => t.type.Equals("runtime", StringComparison.OrdinalIgnoreCase))?.version ?? "8.0.312",
                RecommendedRam = manifest.specs.recommended,
                MinimumRam = manifest.specs.minimum
            },
            Files = files
        };
    }

    public async Task DownloadModpackAsync(FTBModpack pack, bool server, bool full, string dstPath, CancellationToken ct = default)
    {
        CoreLog.Info(nameof(FTBService), $"Starting package export: {pack.Name} / {(server ? "server" : "client")}, destination \"{dstPath}\".");
        using IPackProcessor processor = server
            ? new ServerModpackProcessor(pack, full)
            : new ModrinthModpackProcessor(pack, full);

        await processor.DownloadAsync(ct);
        await processor.ProcessAsync(ct);

        string filePath;
        if (Directory.Exists(dstPath) || !dstPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(dstPath);
            filePath = Path.Combine(dstPath, processor.DefaultFileName);
        }
        else
        {
            var dir = Path.GetDirectoryName(dstPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            filePath = dstPath;
        }

        await using (var fs = File.Create(filePath))
            await processor.PackAsync(fs, filePath, ct);

        CoreLog.Info(nameof(FTBService), $"Package export finished: {filePath}");
    }

    public void Dispose()
    {
        _ftb.Dispose();
    }
}
