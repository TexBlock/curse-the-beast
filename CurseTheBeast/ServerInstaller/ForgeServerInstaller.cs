using CurseTheBeast.Api.Forge;
using CurseTheBeast.Api.Mojang.Model;
using CurseTheBeast.Storage;
using CurseTheBeast.Utils;
using Semver;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CurseTheBeast.ServerInstaller;


public class ForgeServerInstaller : AbstractModServerInstaller
{
    static readonly SemVersion MinGameVersion = SemVersion.Parse("1.6.1", SemVersionStyles.Any);

    FileEntry _installer = null!;
    int _installerSpec = -1;
    string _serverJarPath = null!;
    string _loaderFileName = null!;
    IReadOnlyCollection<MavenFileEntry> _libraries = null!;
    FileEntry? _mappings;

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
        return SemVersion.Parse(GameVersion, SemVersionStyles.Any).ComparePrecedenceTo(MinGameVersion) >= 0;
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

            var libs = installerJson["versionInfo"]!["libraries"]!.AsArray()
                .Where(l => l!.AsObject().TryGetPropertyValue("serverreq", out var isServer) && (bool)isServer!)
                .Select(l => new MavenFileEntry(l!["name"]!.ToString())
                    .WithMavenRepo(replaceLegacyMavenUrl(l!["url"]?.ToString()) ?? "https://libraries.minecraft.net")
                    .WithMavenBaseArchiveEntryName());
            var opts = installerJson["optionals"]?.AsArray()
                .Where(l => l!.AsObject().TryGetPropertyValue("server", out var isServer) && (bool)isServer!)
                .Select(l => new MavenFileEntry(l!["artifact"]!.ToString())
                    .WithMavenRepo(replaceLegacyMavenUrl(l!["maven"]?.ToString()) ?? "https://libraries.minecraft.net")
                    .WithMavenBaseArchiveEntryName()) ?? [];

            _libraries = [.. libs, .. opts];
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
                    replaceLegacyMavenUrl(lib["downloads"]?["artifact"]?["url"]?.ToString())))
                .Where(lib => lib != null))
            .DistinctBy(lib => lib!.Artifact.Id)
            .ToArray()!;
        if (_installerSpec == 1 && manifest.downloads.server_mappings != null)
        {
            _mappings = new FileEntry(RepoType.ServerMappings, GameVersion)
                .SetDownloadable("server_mappings.txt", manifest.downloads.server_mappings!.url)
                .WithSize(manifest.downloads.server_mappings.size)
                .WithSha1(manifest.downloads.server_mappings.sha1);
            return [.. (_libraries as IReadOnlyCollection<FileEntry>), _mappings];
        }
        else
        {
            return _libraries;
        }
    }

    static string? replaceLegacyMavenUrl(string? url)
        => url?.Replace("//files.minecraftforge.net/maven/", "//maven.minecraftforge.net/");

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

        // 尝试生成允许 mojmap 缓存的安装包
        var modifiedInstaller = await generateModifiedInstaller(ct);

        // 执行安装
        var ret = await java.ExecuteJarAsync(modifiedInstaller ?? _installer.LocalPath, new[] { "--installServer", ".", "--offline" }, 
            _tempStorage.WorkSpace, ct);
        if (ret != 0)
            throw new Exception($"forge-{GameVersion}-{LoaderVersion} 服务端预安装失败");
        if (modifiedInstaller != null)
        {
            try
            {
                File.Delete(modifiedInstaller);
            }
            catch (Exception) { }
        }

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

    private async Task<string?> generateModifiedInstaller(CancellationToken ct)
    {
        if (_mappings == null)
            return null;

        // 打开原安装包
        using var srcZip = new ZipArchive(File.OpenRead(_installer.LocalPath), ZipArchiveMode.Read, false, UTF8);

        // 尝试解析并修改清单
        var profileEntry = srcZip.GetEntry("install_profile.json");
        if (profileEntry == null)
            return null;
        JsonNode? profileJson = null;
        string? mojmapArtifactName = null;
        using (var profileStream = profileEntry.Open())
        {
            profileJson = await JsonNode.ParseAsync(profileStream, cancellationToken: ct);
            if (profileJson == null)
                return null;
            foreach (var processorJson in profileJson!["processors"]!.AsArray())
            {
                var argsJson = processorJson!["args"]?.AsArray();
                if (argsJson?.Any(value => value?.GetValueKind() == JsonValueKind.String && value.GetValue<string>() == "DOWNLOAD_MOJMAPS") == true)
                {
                    argsJson.Add(JsonValue.Create("--skipIfExists"));
                    var valueJson = profileJson?["data"]?["MOJMAPS"]?["server"];
                    if (valueJson?.GetValueKind() == JsonValueKind.String)
                        mojmapArtifactName = valueJson.GetValue<string>().TrimStart('[').TrimEnd(']');
                    break;
                }
            }
        }
        if (profileJson == null || mojmapArtifactName == null)
            return null;

        // 生成修改版安装包文件
        var dstInstallerPath = Path.Combine(_tempStorage.WorkSpace, "installer.jar");
        await using var dstFs = File.Create(dstInstallerPath);
        using var dstZip = new ZipArchive(dstFs, ZipArchiveMode.Create, true, UTF8);

        // 添加修改后的清单
        using (var dstStream = dstZip.CreateEntry("install_profile.json", CompressionLevel.Fastest).Open())
            await JsonSerializer.SerializeAsync(dstStream, profileJson, JsonNodeContext.Default.JsonNode, cancellationToken: ct);

        // 复制除清单和签名以外的其他文件
        foreach (var entry in srcZip.Entries)
        {
            if (entry.Name == "install_profile.json" || (entry.FullName.StartsWith("META-INF") && (entry.Name.EndsWith(".SF") || entry.Name.EndsWith(".RSA"))))
                continue;
            using var srcStream = entry.Open();
            using var dstStream = dstZip.CreateEntry(entry.FullName, CompressionLevel.Fastest).Open();
            await srcStream.CopyToAsync(dstStream, ct);
        }

        // 复制 mojmap 文件至缓存位置
        var dstMappingFile = Path.Combine(_tempStorage.WorkSpace, "libraries", new MavenArtifact(mojmapArtifactName).FilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(dstMappingFile)!);
        File.Copy(_mappings.LocalPath, dstMappingFile, true);

        // 完成
        return dstInstallerPath;
    }

    public override void Dispose()
    {
        _tempStorage.Dispose();
    }
}
