using CurseTheBeast.Core.Services.Model;
using CurseTheBeast.Core.Utils;
using CurseTheBeast.Core.Storage;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using CurseTheBeast.Core.Services;
using static CurseTheBeast.Core.Services.Model.FTBFileEntry;

namespace CurseTheBeast.Core.Packs;


public class ServerModpackProcessor : IPackProcessor
{
    public string DefaultFileName => $"{PathUtils.EscapeFileName(_pack.Name)} v{_pack.Version.Name}{(_preinstall ? " Preinstalled" : "")}.zip";

    readonly FTBModpack _pack;
    readonly bool _preinstall;

    readonly string _name;
    readonly IReadOnlyCollection<FTBFileEntry> _assets;

    ServerModLoaderService? _modloader;
    IReadOnlyCollection<FileEntry>? _modLoaderFiles;

    public ServerModpackProcessor(FTBModpack pack, bool preinstall)
    {
        this._pack = pack;
        this._preinstall = preinstall;

        _name = $"{_pack.Name} v{_pack.Version.Name} Server{(preinstall ? " Preinstalled" : "")}";
        _assets = _pack.Files.Where(f => f.Side.HasFlag(FileSide.Server)).ToArray();
    }

    public async Task DownloadAsync(CancellationToken ct = default)
    {
        var downloadTask = _assets.Where(a => !a.Validate()).ToArray();
        using (var modrinth = new ModrinthService())
            await modrinth.FetchModFileUrlAsync(downloadTask.Where(f => f.IsMod), ct);
        await FileDownloadService.DownloadAsync("下载整合包文件", downloadTask, false, ct);

        if (_pack.Icon != null)
            await FileDownloadService.DownloadAsync("下载图标", [_pack.Icon], true, ct);
    }

    public async Task ProcessAsync(CancellationToken ct = default)
    {
        _modloader?.Dispose();
        _modloader = null;
        _modLoaderFiles = null;

        _modloader = new ServerModLoaderService(_pack, _preinstall);
        _modLoaderFiles = await _modloader.GetModLoaderFilesAsync(ct);
    }

    public async Task PackAsync(Stream stream, string dstHint, CancellationToken ct = default)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, true, MyEncoding.UTF8);
        await writeAssetsAsync(archive, ct);
        await writeManifestAsync(archive, ct);
        await setCommentAsync(archive, ct);
        await writeReadmeMdAsync(archive, ct);
        await writeIconAsync(archive, ct);
        await writeLoaderFilesAsync(archive, ct);
    }

    async Task writeManifestAsync(ZipArchive archive, CancellationToken ct)
    {
        // 瞎编的清单格式
        await archive.WriteJsonFileAsync("server-manifest.json", new JsonObject
        {
            ["name"] = _pack.Name,
            ["version"] = _pack.Version.Name,
            ["gameVersion"] = _pack.Runtime.GameVersion,
            ["modLoaderType"] = _pack.Runtime.ModLoaderType,
            ["modLoaderVersion"] = _pack.Runtime.ModLoaderVersion,
            ["javaVersion"] = _pack.Runtime.JavaVersion,
            ["recommendedRam"] = _pack.Runtime.RecommendedRam,
            ["minimumRam"] = _pack.Runtime.MinimumRam,
        }, ct);
    }

    async Task writeAssetsAsync(ZipArchive archive, CancellationToken ct)
    {
        foreach (var file in _assets)
        {
            await archive.WriteFileAsync(_name, file, ct);
        }
    }

    Task setCommentAsync(ZipArchive archive, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine(_name);
        if (_pack.Summary != null)
        {
            sb.AppendLine();
            sb.AppendLine(_pack.Summary);
        }
        sb.AppendLine();
        sb.Append(_pack.HomePageUrl);
        archive.Comment = sb.ToString();
        return Task.CompletedTask;
    }

    async Task writeReadmeMdAsync(ZipArchive archive, CancellationToken ct)
    {
        if (_pack.ReadMe == null)
            return;

        await archive.WriteTextFileAsync("README.md", _pack.ReadMe, ct);
    }

    async Task writeIconAsync(ZipArchive archive, CancellationToken ct)
    {
        if (_pack.Icon == null || _pack.Icon.Unreachable)
            return;

        await archive.WriteFileAsync(null, _pack.Icon, ct);
    }

    async Task writeLoaderFilesAsync(ZipArchive archive, CancellationToken ct)
    {
        if (_modLoaderFiles == null)
            return;

        foreach (var file in _modLoaderFiles)
            await archive.WriteFileAsync(_name, file, ct);

        if (Environment.OSVersion.Platform == PlatformID.Win32NT && _preinstall)
            await archive.WriteTextFileAsync("双击“run.bat”文件即可启动服务端", "", ct);
    }

    public void Dispose()
    {
        _modloader?.Dispose();
    }
}
