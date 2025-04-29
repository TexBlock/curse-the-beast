using CurseTheBeast.Download;
using CurseTheBeast.Services;
using CurseTheBeast.Services.Model;
using CurseTheBeast.Utils;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using static CurseTheBeast.Services.Model.FTBFileEntry;

namespace CurseTheBeast.Packs;


public class ModrinthModpackProcessor : IPackProcessor
{
    public string DefaultFileName => $"{PathUtils.EscapeFileName(_pack.Name)} v{_pack.Version.Name}{(_withAsset ? "" : " Light")}.zip";

    readonly FTBModpack _pack;
    readonly bool _withAsset;

    readonly FTBFileEntry[] _offlineAssets;
    readonly FTBFileEntry[] _onlineAssets;

    public ModrinthModpackProcessor(FTBModpack pack, bool withAsset)
    {
        this._pack = pack;
        this._withAsset = withAsset;
        if (_withAsset)
        {
            var groups = _pack.Files.GroupBy(f => f.IsMod).ToArray();
            _offlineAssets = groups.FirstOrDefault(g => !g.Key)?.ToArray() ?? [];
            _onlineAssets = groups.FirstOrDefault(g => g.Key)?.ToArray() ?? [];
        }
        else
        {
            _offlineAssets = [];
            _onlineAssets = _pack.Files;
        }
    }

    public async Task DownloadAsync(CancellationToken ct = default)
    {
        var downloadTask = Focused.Status("检查缓存", _ => _offlineAssets.Where(a => !a.Validate()).ToArray());
        using (var modrinth = new ModrinthService())
            await modrinth.FetchModFileUrlAsync(_onlineAssets.Concat(downloadTask).Where(f => f.IsMod), ct);
        await FileDownloadService.DownloadAsync("下载整合包文件", downloadTask, false, ct);

        if (_pack.Icon != null)
            await FileDownloadService.DownloadAsync("下载图标", [_pack.Icon], true, ct);

        Success.WriteLine($"√ 下载完成");
    }

    public Task ProcessAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task PackAsync(Stream stream, string dstHint, CancellationToken ct = default)
    {
        await Focused.StatusAsync("打包中", async ctx =>
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create, true, UTF8);
            await writeAssetsAsync(archive, ct);
            await writeManifestAsync(archive, ct);
            await setCommentAsync(archive, ct);
            await writeReadmeMdAsync(archive, ct);
            await writeIconAsync(archive, ct);
        });
        Success.WriteLine($"√ 打包完成：{dstHint}");
    }

    async Task writeManifestAsync(ZipArchive archive, CancellationToken ct)
    {
        var manifest = new JsonObject
        {
            ["formatVersion"] = 1,
            ["game"] = "minecraft",
            ["name"] = _pack.Name,
            ["summary"] = _pack.Summary,
            ["versionId"] = _pack.Version.Name,
            ["dependencies"] = new JsonObject()
            {
                ["minecraft"] = _pack.Runtime.GameVersion,
                [_pack.Runtime.ModLoaderType switch
                {
                    "fabric" => "fabric-loader",
                    "quilt" => "quilt-loader",
                    _ => _pack.Runtime.ModLoaderType
                }] = _pack.Runtime.ModLoaderVersion
            },
            ["files"] = new JsonArray(_onlineAssets.Select(f => new JsonObject()
            {
                ["path"] = f.ArchiveEntryName ?? throw new Exception("Archive entry name not set"),
                ["hashes"] = new JsonObject()
                {
                    ["sha1"] = f.Sha1 ?? throw new Exception("Sha1 not set"),
                    ["sha512"] = f.Sha512
                },
                ["env"] = new JsonObject()
                {
                    ["client"] = (f.Side, f.Optional) switch
                    {
                        (FileSide.Both or FileSide.Client, true) => "optional",
                        (FileSide.Both or FileSide.Client, false) => "required",
                        _ => "unsupported"
                    },
                    ["server"] = (f.Side, f.Optional) switch
                    {
                        (FileSide.Both or FileSide.Server, true) => "optional",
                        (FileSide.Both or FileSide.Server, false) => "required",
                        _ => "unsupported"
                    },
                },
                ["fileSize"] = f.Size ?? throw new Exception("File size not set"),
                ["downloads"] = f.Urls.Count != 0 ? new JsonArray(f.Urls.Select(url => JsonValue.Create(url)).ToArray())
                    : throw new Exception("File url not set"),
            }).ToArray())
        };

        await archive.WriteJsonFileAsync("modrinth.index.json", manifest, ct);
    }

    async Task writeAssetsAsync(ZipArchive archive, CancellationToken ct)
    {
        foreach (var file in _offlineAssets)
        {
            if (file.Side == FileSide.Both)
                await archive.WriteFileAsync(file.Side switch
                {
                    FileSide.Both => "overrides",
                    FileSide.Server => "server-overrides",
                    FileSide.Client => "client-overrides",
                    _ => throw new Exception("Unknown file side: " + file.Side)
                }, file, ct);
        }
    }

    Task setCommentAsync(ZipArchive archive, CancellationToken ct)
    {
        var comment = new StringBuilder();

        comment.AppendLine($"{_pack.Name} v{_pack.Version.Name}");
        if (_pack.Summary != null)
        {
            comment.AppendLine();
            comment.AppendLine(_pack.Summary);
        }
        comment.AppendLine();
        comment.Append(_pack.HomePageUrl);

        archive.Comment = comment.ToString();
        return Task.CompletedTask;
    }

    async Task writeReadmeMdAsync(ZipArchive archive, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_pack.ReadMe))
            return;

        await archive.WriteTextFileAsync("overrides/README.md", _pack.ReadMe, ct);
    }

    async Task writeIconAsync(ZipArchive archive, CancellationToken ct)
    {
        if (_pack.Icon == null || _pack.Icon.Unreachable)
            return;

        await archive.WriteFileAsync("overrides", _pack.Icon, ct);
    }

    public void Dispose()
    {
    }
}
