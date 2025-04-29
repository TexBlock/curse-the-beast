using CurseTheBeast.Api.Azul;
using CurseTheBeast.Api.Azul.Model;
using CurseTheBeast.Api.Mojang;
using CurseTheBeast.Api.Mojang.Model;
using CurseTheBeast.ServerInstaller;
using CurseTheBeast.Services.Model;
using CurseTheBeast.Storage;
using Semver;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace CurseTheBeast.Services;


public class ServerModLoaderService : IDisposable
{
    // 此版本的8在绝大多数情况下不会有问题
    const string DefaultJava8Version = "8u312";

    public bool PreinstallSupported { get; }

    readonly AbstractModServerInstaller? _installer = null!;
    readonly FTBModpack _pack;
    readonly bool _preinstall;

    JavaRuntime? _java;

    public ServerModLoaderService(FTBModpack pack, bool preinstall)
    {
        _pack = pack;
        _preinstall = preinstall;
        _installer = GetInstaller();
        PreinstallSupported = _installer?.IsPreinstallationSupported() ?? false;
    }

    public async Task<IReadOnlyCollection<FileEntry>> GetModLoaderFilesAsync(CancellationToken ct = default)
    {
        // 不预安装，只带一个jar
        if (!_preinstall)
            return await GetStandaloneLoaderJarAsync(ct);

        if (!PreinstallSupported || _installer == null)
            throw new Exception($"不支持预安装 {_pack.Runtime.ModLoaderType}-{_pack.Runtime.GameVersion}-{_pack.Runtime.ModLoaderVersion} 服务端");

        _java ??= await GetJavaRuntimeAsync(ct);
        var manifest = await GetGameManifestAsync(ct);
        var serverJar = await GetServerJarAsync(manifest, ct);
        var loaderFiles = await GetModLoaderFilesAsync(_java, manifest, serverJar, ct);

        return loaderFiles;
    }

    public async Task<IReadOnlyCollection<FileEntry>> GetStandaloneLoaderJarAsync(CancellationToken ct)
    {
        if (_installer != null)
        {
            var installerJar = await Focused.StatusAsync($"获取 {_pack.Runtime.ModLoaderType} 加载器", async ctx => await _installer.ResolveStandaloneLoaderJarAsync(ct));
            if (installerJar != null)
            {
                try
                {
                    await FileDownloadService.DownloadAsync($"下载 {_pack.Runtime.ModLoaderType} 加载器", installerJar, true, ct);
                    return installerJar;
                }
                catch (Exception)
                {

                }
            }
        }
        // 不支持、获取失败、下载失败就不带了，自己下载去
        return [];
    }

    public async Task<IReadOnlyCollection<FileEntry>> GetModLoaderFilesAsync(JavaRuntime java, GameManifest manifest, FileEntry serverJar, CancellationToken ct = default)
    {
        var installerJar = await Focused.StatusAsync($"解析 {_pack.Runtime.ModLoaderType} 安装器", async ctx => await _installer!.ResolveInstallerAsync(ct));
        if (installerJar.Count > 0)
            await FileDownloadService.DownloadAsync($"下载 {_pack.Runtime.ModLoaderType} 安装器", installerJar, true, ct);

        var deps = await Focused.StatusAsync($"解析 {_pack.Runtime.ModLoaderType} 依赖", 
            async ctx => await _installer!.ResolveInstallerDependenciesAsync(manifest, ct));
        if(deps.Count > 0)
            await FileDownloadService.DownloadAsync($"下载 {_pack.Runtime.ModLoaderType} 依赖", deps, true, ct);

        return await Focused.StatusAsync($"预安装 {_pack.Runtime.ModLoaderType} 服务端", 
            async ctx => await _installer!.PreInstallAsync(java, serverJar));
    }

    public AbstractModServerInstaller? GetInstaller()
	{
        var installer = _pack.Runtime.ModLoaderType.ToLower() switch
        {
            "forge" => new ForgeServerInstaller(),
            "fabric" => new FabricServerInstaller() as AbstractModServerInstaller,
            "neoforge" => new NeoForgeServerInstaller(),
            _ => null,
        };
        if(installer != null)
        {
            installer.GameVersion = _pack.Runtime.GameVersion;
            installer.LoaderVersion = _pack.Runtime.ModLoaderVersion;
            installer.ServerName = $"{_pack.Name} v{_pack.Version.Name} Server";
            installer.Ram = _pack.Runtime.RecommendedRam;
        }
        return installer;
    }

    public async Task<GameManifest> GetGameManifestAsync(CancellationToken ct = default)
    {
        return await LocalStorage.Persistent.GetOrSaveObject($"game-{_pack.Runtime.GameVersion}", async () =>
        {
            return await Focused.StatusAsync("解析版本清单", async ctx =>
            {
                using var api = new MojangApiClient();
                var list = await api.GetGameVersionListAsync(ct);
                var version = list.versions.FirstOrDefault(v => v.id == _pack.Runtime.GameVersion)
                    ?? throw new Exception("未知的 MC 版本：" + _pack.Runtime.GameVersion);
                return await api.GetGameManifestAsync(version.url, ct);
            });
        }, GameManifest.GameManifestContext.Default.GameManifest, ct);
    }

    public async Task<FileEntry> GetServerJarAsync(GameManifest manifest, CancellationToken ct = default)
    {
        var serverJarFile = new FileEntry(RepoType.ServerJar, $"{_pack.Runtime.GameVersion}.jar")
            .SetSha1FileRequired();
        if (serverJarFile.Validate(false))
            return serverJarFile;

        serverJarFile.SetDownloadable($"mc-server-{_pack.Runtime.GameVersion}.jar", manifest.downloads.server.url)
            .WithSha1(manifest.downloads.server.sha1)
            .WithSize(manifest.downloads.server.size);
        await FileDownloadService.DownloadAsync("下载服务端", [serverJarFile], true, ct);
        return serverJarFile;
    }

    public async Task<JavaRuntime> GetJavaRuntimeAsync(CancellationToken ct = default)
    {
        var javaArchiveFile = await Focused.StatusAsync("获取 Java 运行环境信息", async ctx =>
        {
            var os = Environment.OSVersion.Platform switch
            {
                PlatformID.Win32NT => "windows",
                PlatformID.Unix => "linux",
                _ => throw new Exception("服务端预安装失败：不支持当前操作系统")
            };
            var arch = RuntimeInformation.ProcessArchitecture.ToString().ToLower();
            var archiveType = Environment.OSVersion.Platform == PlatformID.Win32NT ? "zip" : "tar.gz";
            var javaVersion = SemVersion.Parse(_pack.Runtime.JavaVersion);

            var displayFileName = $"zulu-{_pack.Runtime.JavaVersion}-{os}.{archiveType}";
            // 兼容旧版索引
            var javaArchiveFile = new FileEntry(RepoType.JreArchive, displayFileName);
            if (javaArchiveFile.Validate())
            {
                // 检查之前误下载的musl版JRE
                if (isMuslJre(javaArchiveFile.LocalPath))
                    javaArchiveFile.Delete();
                else
                    return javaArchiveFile;
            }

            var baseVersionPair = (Version: $"{javaVersion.Major}.{javaVersion.Minor}.{javaVersion.Patch}", PkgType: "jre");
            var versionPairs = new[] { baseVersionPair, baseVersionPair with { PkgType = "jdk" } }.AsEnumerable();
            if (javaVersion.Major == 8)
            {
                versionPairs = versionPairs.Append((DefaultJava8Version, "jre"))
                    .Append((DefaultJava8Version, "jdk"));
            }
            versionPairs = versionPairs.Append((javaVersion.Major.ToString(), "jre"))
                    .Append((javaVersion.Major.ToString(), "jdk"));

            using var api = new AzulApiClient();
            ZuluPackage? pkg = null;
            foreach (var pair in versionPairs)
            {
                pkg = (await api.GetZuluPackageAsync(pair.Version, os, arch, archiveType, pair.PkgType, ct))
                    .Where(p => !p.name.ToLower().Contains("musl") && p.java_version[0] == javaVersion.Major)
                    .FirstOrDefault();
                if (pkg != null)
                    break;
            }
            if(pkg == null)
                throw new Exception($"服务端预安装失败：无法获取 Java{_pack.Runtime.JavaVersion} 运行环境信息");

            // 新版索引
            javaArchiveFile = new FileEntry(RepoType.JreArchive, pkg.name)
                .SetDownloadable(displayFileName, pkg.download_url);
            return javaArchiveFile;
        });
        await FileDownloadService.DownloadAsync("下载 Java 运行环境", new[] { javaArchiveFile }, true, ct);

        return Focused.Status("准备 Java 运行环境", ctx =>
        {
            return JavaRuntime.FromArchive(javaArchiveFile.LocalPath);
        });
    }

    static bool isMuslJre(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        return archive.Entries.First().FullName.Contains("musl");
    }

    public void Dispose()
    {
        _installer?.Dispose();
        _java?.Dispose();
    }
}