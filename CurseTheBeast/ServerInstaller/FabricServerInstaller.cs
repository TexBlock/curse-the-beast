using CurseTheBeast.Api.Fabric;
using CurseTheBeast.Api.Fabric.Model;
using CurseTheBeast.Api.Mojang.Model;
using CurseTheBeast.Storage;
using CurseTheBeast.Utils;
using Spectre.Console;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace CurseTheBeast.ServerInstaller;


public partial class FabricServerInstaller : AbstractModServerInstaller
{
    const string LegacyInstallerVersion = "0.11.2";
    const string DefaultLauncherManifestMainClass = "net.fabricmc.loader.launch.server.FabricServerLauncher";
    const string ServicesDir = "META-INF/services/";
    const string ManifestFile = "META-INF/MANIFEST.MF";

    readonly FabricApiClient _api = new();
    ServerManifest _manifest = null!;
    IReadOnlyList<MavenFileEntry> _libraries = null!;

    readonly LocalStorage _tempStorage = LocalStorage.GetTempStorage("fabric-install");

    public override async Task<IReadOnlyCollection<FileEntry>> ResolveStandaloneLoaderJarAsync(CancellationToken ct = default)
    {
        var meta = await _api.GetInstallerMetaAsync(ct);
        var installerVersion = meta.Where(v => v.stable).FirstOrDefault()?.version ?? LegacyInstallerVersion;

        var file = await resolveStandaloneServerJarAsync(installerVersion, ct);
        if(file == null && installerVersion != LegacyInstallerVersion)
        {
            file = await resolveStandaloneServerJarAsync(LegacyInstallerVersion, ct);
        }
        if(file == null)
        {
            return Array.Empty<FileEntry>();
        }

        return new[] { file };
    }

    public override bool IsPreinstallationSupported() => true;

    async Task<FileEntry?> resolveStandaloneServerJarAsync(string installerVersion, CancellationToken ct = default)
    {
        var fileName = $"fabric-server-{GameVersion}-{LoaderVersion}-{installerVersion}.jar";
        var file = new FileEntry(RepoType.ModLoaderJar, fileName)
            .WithArchiveEntryName(fileName);
        if (file.Validate())
            return file;

        var url = await _api.GetServerLoaderUrlAsync(GameVersion, LoaderVersion, installerVersion, ct);
        if (url == null)
            return null;

        file.SetDownloadable(fileName, url);
        return file;
    }

    public override Task<IReadOnlyCollection<FileEntry>> ResolveInstallerAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Array.Empty<FileEntry>() as IReadOnlyCollection<FileEntry>);
    }

    public override async Task<IReadOnlyCollection<FileEntry>> ResolveInstallerDependenciesAsync(GameManifest manifest, CancellationToken ct)
    {
        using var api = new FabricApiClient();
        _manifest = await api.GetServerManifestAsync(GameVersion, LoaderVersion, ct);
        _libraries = _manifest.libraries.Select(l => new MavenFileEntry(l.name)
            .WithMavenRepo(l.url)
            .WithMavenBaseArchiveEntryName()).ToArray();
        return _libraries;
    }

    public override async Task<IReadOnlyCollection<FileEntry>> PreInstallAsync(JavaRuntime java, FileEntry serverJar, CancellationToken ct = default)
    {
        var embededLib = new Version(LoaderVersion) <= "0.12.5";

        var launcherFile = await generateLauncherAsync(embededLib, ct);
        var jreFiles = await java.GetJreFilesAsync(ct);
        var scriptFile = JarLauncherUtils.GenerateScript(_tempStorage.WorkSpace, java.DistName, launcherFile.ArchiveEntryName!, ServerName ?? $"Fabric Server {GameVersion} {LoaderVersion}", Ram);
        serverJar.WithArchiveEntryName("server.jar");
        var eulaAgreementFile = await GenerateEulaAgreementFileAsync(_tempStorage.WorkSpace, ct);

        var files = new List<FileEntry>
        {
            launcherFile,
            serverJar,
            scriptFile,
            eulaAgreementFile
        };
        if (!embededLib)
            files.AddRange(_libraries);
        files.AddRange(jreFiles);
        return files;
    }

    // https://github.com/FabricMC/fabric-installer/blob/e73f4466e157f586472ec6c6cec5f8d1cc9dddaa/src/main/java/net/fabricmc/installer/server/ServerInstaller.java
    async Task<FileEntry> generateLauncherAsync(bool embededLib, CancellationToken ct)
    {
        var fileName = $"fabric-server-{GameVersion}-{LoaderVersion}.jar";
        var file = new FileEntry(_tempStorage, RepoType.ModLoaderJar, fileName)
            .WithArchiveEntryName(fileName);
        using var launcherJar = ZipFile.Open(file.LocalPath, ZipArchiveMode.Update, UTF8);

        var manifest = new Dictionary<string, string>()
        {
            ["Manifest-Version"] = "1.0",
            ["Main-Class"] = await getLauncherMainClass(),
        };
        if (!embededLib)
            manifest["Class-Path"] = string.Join(' ', _libraries!.Select(l => l.ArchiveEntryName));
        await launcherJar.WriteJarManifestAsync(manifest);

        // linux下也是CRLF
        await launcherJar.WriteTextFileAsync("fabric-server-launch.properties", $"launch.mainClass={_manifest.mainClass}\r\n", ct);

        if (!embededLib)
            return file;

        var serviceFiles = new Dictionary<string, HashSet<string>>();
        foreach (var lib in _libraries)
        {
            using var libJar = ZipFile.OpenRead(lib.LocalPath);
            foreach (var entry in libJar.Entries)
            {
                if (entry.Name == string.Empty)
                    continue;

                // 服务列表
                if (entry.FullName.StartsWith(ServicesDir) && entry.FullName.IndexOf('/', ServicesDir.Length) < 0)
                {
                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream, UTF8);

                    if(!serviceFiles.TryGetValue(entry.FullName, out var services))
                        services = serviceFiles[entry.FullName] = new();
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        int pos = line.IndexOf('#');
                        if (pos >= 0)
                            line = line[..pos];
                        line = line.Trim();

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        services.Add(line);
                    }
                }
                // 签名文件
                else if (JarSignatureFileNameReg().IsMatch(entry.FullName))
                {

                }
                // 重复文件
                else if (launcherJar.GetEntry(entry.FullName) != null)
                {

                }
                else
                {
                    var newEntry = launcherJar.CreateEntry(entry.FullName);
                    using var newStream = newEntry.Open();
                    using var stream = entry.Open();
                    await stream.CopyToAsync(newStream, ct);
                }
            }
        }

        // write service definitions
        foreach (var (entryName, services) in serviceFiles)
        {
            if (services.Count == 0)
                continue;

            var entry = launcherJar.CreateEntry(entryName);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, UTF8);

            foreach (var service in services)
            {
                writer.Write(service);
                writer.Write('\n');
            }
        }

        return file;
    }

    async Task<string> getLauncherMainClass()
    {
        var loader = _libraries!.FirstOrDefault(l => FabricLoaderJarNameReg().IsMatch(l.Artifact.Id));
        if (loader == null)
            return DefaultLauncherManifestMainClass;

        using var jar = ZipFile.OpenRead(loader.LocalPath);
        var dict = await jar.ReadJarManifestAsync();
        return dict.TryGetValue("Main-Class", out var mainClass) ? mainClass : DefaultLauncherManifestMainClass;
    }

    public override void Dispose()
    {
        _tempStorage.Dispose();
        _api.Dispose();
    }

    [GeneratedRegex("META-INF/[^/]+\\.(SF|DSA|RSA|EC)", RegexOptions.IgnoreCase)]
    private static partial Regex JarSignatureFileNameReg();

    [GeneratedRegex(@"net\.fabricmc:fabric-loader:.*", RegexOptions.IgnoreCase)]
    private static partial Regex FabricLoaderJarNameReg();
}
