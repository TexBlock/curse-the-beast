using CurseTheBeast.Api.Forge;
using CurseTheBeast.Api.Mojang.Model;
using CurseTheBeast.Storage;
using CurseTheBeast.Utils;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CurseTheBeast.ServerInstaller;


public class ForgeServerInstaller : AbstractModServerInstaller
{
    // const string MinGameVersion = "1.12";
    const string MinGameVersion = "1.6.1";

    FileEntry _installer = null!;
    int _installerSpec = -1;
    string _serverJarPath = null!;
    string _loaderFileName = null!;
    IReadOnlyCollection<MavenFileEntry> _libraries = null!;

    readonly LocalStorage _tempStorage = LocalStorage.GetTempStorage("forge-install");


    public override async Task<IReadOnlyCollection<FileEntry>> ResolveStandaloneLoaderJarAsync(CancellationToken ct = default)
    {
        var installerFileName = $"forge-installer-{GameVersion}-{LoaderVersion}.jar";
        var file = new FileEntry(RepoType.ModLoaderJar, installerFileName)
            .WithArchiveEntryName(installerFileName);
        if (file.Validate())
            return new[] { file };

        using var api = new ForgeApiClient();
        var url = await api.GetServerInstallerUrlAsync(GameVersion, LoaderVersion, ct);
        if (url == null)
            return Array.Empty<FileEntry>();

        file.SetDownloadable(installerFileName, url);
        return new[] { file };
    }

    public override bool IsPreinstallationSupported()
    {
        return new Version(GameVersion) >= MinGameVersion;
    }

    public override async Task<IReadOnlyCollection<FileEntry>> ResolveInstallerAsync(CancellationToken ct = default)
    {
        var installers = await ResolveStandaloneLoaderJarAsync(ct);
        if (installers.Count == 0)
            throw new Exception($"无法获取 forge-{GameVersion}-{LoaderVersion} 安装器下载链接");
        _installer = installers.First();
        return installers;
    }

    public override async Task<IReadOnlyCollection<FileEntry>> ResolveInstallerDependenciesAsync(GameManifest manifest, CancellationToken ct = default)
    {
        using var zip = ZipFile.OpenRead(_installer.LocalPath);
        var installerJson = await getJsonInZip(zip, "install_profile.json");

        if(installerJson.AsObject().TryGetPropertyValue("spec", out var specNode))
            _installerSpec = (int)specNode!;
        else
            _installerSpec = -1;

        // 超低版本特殊处理
        if (_installerSpec == -1)
        {
            _serverJarPath = $"minecraft_server.{GameVersion}.jar";
            _loaderFileName = installerJson["install"]!["filePath"]!.ToString();

            _libraries = installerJson["versionInfo"]!["libraries"]!.AsArray()
                .Where(l => l!.AsObject().TryGetPropertyValue("serverreq", out var isServer) && (bool)isServer!)
                .Select(l => new MavenFileEntry(l!["name"]!.ToString())
                    .WithMavenRepo(l!["url"]?.ToString() ?? "https://libraries.minecraft.net")
                    .WithMavenBaseArchiveEntryName())
                .ToArray();
            return _libraries;
        }

        // 主流版本
        var versionJson = await getJsonInZip(zip, installerJson["json"]!.ToString().TrimStart('.').Trim('/'));
        // ( ,1.16.5]
        if (_installerSpec == 0)
        {
            _serverJarPath = $"minecraft_server.{GameVersion}.jar";
            _loaderFileName = new MavenArtifact(installerJson["path"]!.ToString()).FileName;
        }
        // [1.17.1, )
        else if(_installerSpec == 1)
        {
            _serverJarPath = installerJson["serverJarPath"]!.ToString()
                .Replace("{LIBRARY_DIR}", "libraries")
                .Replace("{MINECRAFT_VERSION}", GameVersion)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart('.')
                .Trim(Path.DirectorySeparatorChar);
        }
        else
        {
            throw new Exception($"不支持 forge-{GameVersion}-{LoaderVersion} 服务端预安装");
        }

        _libraries = new[] { installerJson, versionJson }
            .SelectMany(json => json["libraries"]!
                .AsArray()
                .Select(lib => getMavenLib(lib!["name"]!.ToString(), 
                    lib["downloads"]?["artifact"]?["path"]?.ToString(), 
                    lib["downloads"]?["artifact"]?["url"]?.ToString()))
                .Where(lib => lib != null))
            .DistinctBy(lib => lib!.Artifact.Id)
            .ToArray()!;
        return _libraries;
    }

    static MavenFileEntry? getMavenLib(string artifactId, string? path, string? providedUrl)
    {
        if (string.IsNullOrWhiteSpace(providedUrl))
            return null;

        var mavenFile = new MavenFileEntry(artifactId)
            .WithMavenUrl(providedUrl)
            .WithMavenBaseArchiveEntryName();
        if (string.IsNullOrWhiteSpace(path))
            mavenFile.WithMavenBaseArchiveEntryName();
        else
            mavenFile.WithArchiveEntryName("libraries", path);

        return mavenFile;
    }

    async ValueTask<JsonNode> getJsonInZip(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName) ?? throw new Exception($"不支持 forge-{GameVersion}-{LoaderVersion} 服务端预安装");
        using var stream = entry.Open();
        return (await JsonSerializer.DeserializeAsync(stream, JsonNodeContext.Default.JsonNode))!;
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
        var ret = await java.ExecuteJarAsync(_installer.LocalPath, new[] { "--installServer", ".", "--offline" }, 
            _tempStorage.WorkSpace, ct);
        if (ret != 0)
            throw new Exception($"forge-{GameVersion}-{LoaderVersion} 服务端预安装失败");

        var title = ServerName ?? $"Forge Server {GameVersion} {LoaderVersion}";
        var files = new List<FileEntry>(64);
        string? launcherScriptName = null;

        // 自行创建脚本
        if(_installerSpec == -1 || _installerSpec == 0)
        {
            if (File.Exists(Path.Combine(_tempStorage.WorkSpace, _loaderFileName)))
            {
                launcherScriptName = JarLauncherUtils.GenerateScript(_tempStorage.WorkSpace, java.DistName, _loaderFileName, title, Ram).ArchiveEntryName;
                files.AddRange(await java.GetJreFilesAsync(ct));
            }
        }
        // 修改forge自带脚本
        else
        {
            launcherScriptName = JarLauncherUtils.InjectForgeScript(_tempStorage.WorkSpace, java.DistName, title, Ram);
            if (launcherScriptName != null)
            {
                files.AddRange(await java.GetJreFilesAsync(ct));
            }
        }

        // 生成EULA同意文件
        await GenerateEulaAgreementFileAsync(_tempStorage.WorkSpace, ct);

        files.AddRange(_tempStorage.GetWorkSpaceFiles());
        if (launcherScriptName != null && Environment.OSVersion.Platform != PlatformID.Win32NT)
            files.First(f => f.ArchiveEntryName == launcherScriptName).SetUnixExecutable();
        return files;
    }

    public override void Dispose()
    {
        _tempStorage.Dispose();
    }
}
