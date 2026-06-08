using CurseTheBeast.Core.Storage;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace CurseTheBeast.Core.ServerInstaller;


public class JavaRuntime : IDisposable
{
    public string DistName { get; }
    public string JavaHome { get; }
    public string JavaPath { get; }
    public string JavaExe { get; }

    readonly LocalStorage _storage;

    public JavaRuntime(LocalStorage storage)
    {
        _storage = storage;
        var dirs = Directory.GetDirectories(storage.WorkSpace);
        if (dirs.Length == 0)
            throw new Exception("未知的JavaHome目录结构");

        if (dirs.Length == 1)
        {
            DistName = Path.GetFileName(dirs[0]);
            JavaHome = dirs[0];
        }
        else
        {
            DistName = $"zulu-jre-{RuntimeInformation.ProcessArchitecture.ToString().ToLower()}";
            JavaHome = Path.GetDirectoryName(storage.WorkSpace)!;
        }

        JavaPath = Path.Combine(JavaHome, "bin");
        if(Environment.OSVersion.Platform == PlatformID.Win32NT)
            JavaExe = Path.Combine(JavaPath, "java.exe");
        else
            JavaExe = Path.Combine(JavaPath, "java");
    }

    public static JavaRuntime FromArchive(string archivePath)
    {
        var storage = LocalStorage.GetTempStorage("java");
        try
        {
            if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archivePath, storage.WorkSpace, true);
            }
            else if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                using var fs = File.OpenRead(archivePath);
                using var gzip = new GZipStream(fs, CompressionMode.Decompress, true);
                TarFile.ExtractToDirectory(gzip, storage.WorkSpace, true);
            }
            else
            {
                throw new Exception("不支持的Java压缩包类型：" + Path.GetFileName(archivePath));
            }
            return new JavaRuntime(storage);
        }
        catch(Exception)
        {
            storage.Dispose();
            throw;
        }
    }

    public Task<int> ExecuteJarAsync(string jarPath, IEnumerable<string>? args, string workDir, CancellationToken ct = default)
    {
        return executeAsync(JavaExe, new[] { "-jar", jarPath }.Concat(args ?? Enumerable.Empty<string>()), workDir, null, ct);
    }

    public async Task<IEnumerable<FileEntry>> GetJreFilesAsync(CancellationToken ct)
    {
        // jdk8-
        var jreDir = Path.Combine(JavaHome, "jre");
        if (Directory.Exists(jreDir))
            return getFiles(jreDir);

        // jdk9+
        if (Directory.Exists(Path.Combine(JavaHome, "jmods")))
        {
            jreDir = await jlinkAsync(ct);
            if(jreDir != null)
                return getFiles(jreDir);
        }

        // jre or fallback
        return getFiles(JavaHome);
    }

    IEnumerable<FileEntry> getFiles(string dir)
    {
        return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Select(f => new FileEntry(f)
                .WithArchiveEntryName(DistName, Path.GetRelativePath(dir, f)));
    }

    async Task<string?> jlinkAsync(CancellationToken ct)
    {
        var jrePath = Path.Combine(JavaHome, "jre-jlink");
        if (Directory.Exists(jrePath))
            return jrePath;
        var jmodsPath = Path.Combine(JavaHome, "jmods");
        var jmods = string.Join(',', Directory.GetFiles(jmodsPath, "*.jmod").Select(path => Path.GetFileNameWithoutExtension(path)));

        var ret = await executeAsync(Path.Combine(JavaPath, Environment.OSVersion.Platform == PlatformID.Win32NT ? "jlink.exe" : "jlink"), 
            new[] { "--output", jrePath, "--module-path", jmodsPath, "--add-modules", jmods, "--strip-debug", "--no-man-pages", "--no-header-files" }, 
            JavaHome, null, ct);
        if (ret != 0)
        {
            if (Directory.Exists(jrePath))
                Directory.Delete(jrePath, true);
            return null;
        }
        return jrePath;
    }

    async Task<int> executeAsync(string exePath, IEnumerable<string>? args, string workDir, IReadOnlyDictionary<string, string>? env, CancellationToken ct = default)
    {
        using var p = new Process();

        p.StartInfo.FileName = exePath;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.UseShellExecute = false;
        if (args != null)
            foreach (var arg in args)
                p.StartInfo.ArgumentList.Add(arg);
        p.StartInfo.WorkingDirectory = workDir;
        p.StartInfo.EnvironmentVariables["JAVA_HOME"] = JavaHome;
        p.StartInfo.EnvironmentVariables["PATH"] = $"{JavaPath}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";
        if (env != null)
            foreach (var (k, v) in env)
                p.StartInfo.EnvironmentVariables[k] = v;

        p.Start();
        p.BeginErrorReadLine();
        p.BeginOutputReadLine();
        using var reg = ct.Register(() => p.Kill(true));
        await p.WaitForExitAsync(ct);

        return p.ExitCode;
    }

    public void Dispose()
    {
        _storage.Dispose();
    }
}
