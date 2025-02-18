using CurseTheBeast.Api.Mojang.Model;
using CurseTheBeast.Api.NeoForge;
using CurseTheBeast.Storage;
using CurseTheBeast.Utils;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CurseTheBeast.ServerInstaller;


public class NeoForgeServerInstaller : AbstractModServerInstaller
{
    const string MinGameVersion = "1.20.1";

    FileEntry _installer = null!;
    string _serverJarPath = null!;
    IReadOnlyCollection<MavenFileEntry> _libraries = null!;
    FileEntry _mappings = null!;

    readonly LocalStorage _tempStorage = LocalStorage.GetTempStorage("neoforge-install");


    public override async Task<IReadOnlyCollection<FileEntry>> ResolveStandaloneLoaderJarAsync(CancellationToken ct = default)
    {
        var installerFileName = $"neoforge-installer-{GameVersion}-{LoaderVersion}.jar";
        var file = new FileEntry(RepoType.ModLoaderJar, installerFileName)
            .WithArchiveEntryName(installerFileName);
        if (file.Validate())
            return [file];

        using var api = new NeoForgeApiClient();
        var url = await api.GetServerInstallerUrlAsync(GameVersion, LoaderVersion, ct);
        if (url == null)
            return Array.Empty<FileEntry>();

        file.SetDownloadable(installerFileName, url);
        return [file];
    }

    public override bool IsPreinstallationSupported()
    {
        return new Version(GameVersion) >= MinGameVersion;
    }

    public override async Task<IReadOnlyCollection<FileEntry>> ResolveInstallerAsync(CancellationToken ct = default)
    {
        var installers = await ResolveStandaloneLoaderJarAsync(ct);
        if (installers.Count == 0)
            throw new Exception($"无法获取 neoforge-{GameVersion}-{LoaderVersion} 安装器下载链接");
        _installer = installers.First();
        return installers;
    }

    public override async Task<IReadOnlyCollection<FileEntry>> ResolveInstallerDependenciesAsync(GameManifest manifest, CancellationToken ct = default)
    {
        using var zip = ZipFile.OpenRead(_installer.LocalPath);
        var installerJson = await getJsonInZip(zip, "install_profile.json");
        var versionJson = await getJsonInZip(zip, installerJson["json"]!.ToString().TrimStart('.').Trim('/'));

        if (installerJson["spec"]?.GetValue<int>() != 1)
            throw new Exception($"不支持 neoforge-{GameVersion}-{LoaderVersion} 服务端预安装");

        _serverJarPath = installerJson["serverJarPath"]!.ToString()
            .Replace("{LIBRARY_DIR}", "libraries")
            .Replace("{MINECRAFT_VERSION}", GameVersion)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart('.')
            .Trim(Path.DirectorySeparatorChar);

        _libraries = new[] { installerJson, versionJson }
            .SelectMany(json => json["libraries"]!
                .AsArray()
                .Select(lib => getMavenLib(lib!["name"]!.ToString(), 
                    lib["downloads"]?["artifact"]?["path"]?.ToString(), 
                    lib["downloads"]?["artifact"]?["url"]?.ToString()))
                .Where(lib => lib != null))
            .DistinctBy(lib => lib!.Artifact.Id)
            .ToArray()!;
        if (manifest.downloads.server_mappings == null)
            return _libraries;

        _mappings = new FileEntry(RepoType.ServerMappings, GameVersion)
            .SetDownloadable("server_mappings.txt", manifest.downloads.server_mappings.url)
            .WithSize(manifest.downloads.server_mappings.size)
            .WithSha1(manifest.downloads.server_mappings.sha1);
        return [.. (_libraries as IReadOnlyCollection<FileEntry>), _mappings];
    }

    static MavenFileEntry? getMavenLib(string artifactId, string? path, string? providedUrl)
    {
        if (string.IsNullOrWhiteSpace(providedUrl))
            return null;

        var mavenFile = new MavenFileEntry(artifactId)
            .WithMavenUrl(providedUrl.Replace("//maven.neoforged.net/releases/", "//maven.neoforged.net/"))
            .WithMavenBaseArchiveEntryName();
        if (string.IsNullOrWhiteSpace(path))
            mavenFile.WithMavenBaseArchiveEntryName();
        else
            mavenFile.WithArchiveEntryName("libraries", path);

        return mavenFile;
    }

    async ValueTask<JsonNode> getJsonInZip(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName) ?? throw new Exception($"不支持 neoforge-{GameVersion}-{LoaderVersion} 服务端预安装");
        using var stream = entry.Open();
        return (await JsonSerializer.DeserializeAsync<JsonNode>(stream, JsonNodeContext.Default.JsonNode))!;
    }

    public override async Task<IReadOnlyCollection<FileEntry>> PreInstallAsync(JavaRuntime java, FileEntry serverJar, CancellationToken ct = default)
    {
        // 复制服务端本体
        var serverJarPath = Path.Combine(_tempStorage.WorkSpace, _serverJarPath);
        Directory.CreateDirectory(Path.GetDirectoryName(serverJarPath)!);
        File.Copy(serverJar.LocalPath, serverJarPath, true);

        // 复制依赖
        foreach(var lib in _libraries)
        {
            var libJarPath = Path.Combine(_tempStorage.WorkSpace, "libraries", lib.Artifact.FilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(libJarPath)!);
            File.Copy(lib.LocalPath, libJarPath, true);
        }

        // 执行安装
        var fatInstallerPath = _mappings == null ? _installer.LocalPath : await generateFatInstaller();
        var ret = await java.ExecuteJarAsync(fatInstallerPath, ["--installServer", ".", "--offline"],
            _tempStorage.WorkSpace, ct);
        if (ret != 0)
            throw new Exception($"neoforge-{GameVersion}-{LoaderVersion} 服务端预安装失败 ");
        try
        {
            File.Delete(fatInstallerPath);
        } catch(Exception) { }

        var title = ServerName ?? $"NeoForge Server {GameVersion} {LoaderVersion}";
        var files = new List<FileEntry>(64);

        var launcherScriptName = JarLauncherUtils.InjectForgeScript(_tempStorage.WorkSpace, java.DistName, title, Ram);
        if (launcherScriptName != null)
            files.AddRange(await java.GetJreFilesAsync(ct));

        // 生成EULA同意文件
        await GenerateEulaAgreementFileAsync(_tempStorage.WorkSpace, ct);

        files.AddRange(_tempStorage.GetWorkSpaceFiles());
        if (launcherScriptName != null && Environment.OSVersion.Platform != PlatformID.Win32NT)
            files.First(f => f.ArchiveEntryName == launcherScriptName).SetUnixExecutable();
        return files;
    }

    private async Task<string> generateFatInstaller()
    {
        var path = Path.Combine(_tempStorage.WorkSpace, "installer.jar");
        using var srcFs = File.OpenRead(_installer.LocalPath);
        using var srcZip = new ZipArchive(srcFs, ZipArchiveMode.Read, true, UTF8);
        await using var dstFs = File.Create(path);
        using var dstZip = new ZipArchive(dstFs, ZipArchiveMode.Create, true, UTF8);
        foreach (var entry in srcZip.Entries)
        {
            var dstEntry = dstZip.CreateEntry(entry.FullName, CompressionLevel.Fastest);
            using var srcStream = entry.Open();
            using var dstStream = dstEntry.Open();
            await srcStream.CopyToAsync(dstStream);
        }

        var mappingEntry = dstZip.CreateEntry($"maven/minecraft/{GameVersion}/server_mappings.txt", CompressionLevel.Fastest);
        using var mappingStream = File.OpenRead(_mappings.LocalPath);
        using var entryStream = mappingEntry.Open();
        await mappingStream.CopyToAsync(entryStream);

        return path;
    }

    public override void Dispose()
    {
        _tempStorage.Dispose();
    }
}
