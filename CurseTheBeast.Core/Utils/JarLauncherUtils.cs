using CurseTheBeast.Core.Storage;

namespace CurseTheBeast.Core.Utils;


public static class JarLauncherUtils
{
    const string TZ = "Asia/Shanghai";
    const int DefaultRam = 6144;

    public static FileEntry GenerateScript(string scriptDir, string jreDirName, string launcherJarName, string title, int? ram)
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            return GenerateWindowsScript(scriptDir, jreDirName, launcherJarName, title, ram);
        else
            return GenerateUnixScript(scriptDir, jreDirName, launcherJarName, title, ram);
    }

    public static FileEntry GenerateWindowsScript(string scriptDir, string jreDirName, string launcherJarName, string title, int? ram)
    {
        // 应该没人用windows文件链接吧
        var script = $$"""
            @echo off
            CHCP 936 >nul
            title {{title}}

            :: 分配内存大小
            set RAM={{ram ?? DefaultRam}}M
            
            set "JAVA_HOME=%~dp0%{{jreDirName}}"
            set "PATH=%JAVA_HOME%\bin;%PATH%"
            set TZ={{TZ}}
            
            java.exe ^
              -Xms%RAM% -Xmx%RAM% ^
              -Duser.timezone=%TZ% ^
              -Dlog4j2.formatMsgNoLookups=true ^
              -jar "{{launcherJarName}}"

            echo.
            echo 按任意退出...
            pause >nul
            """;
        var file = new FileEntry(Path.Combine(scriptDir, "run.bat"))
            .WithArchiveEntryName("run.bat");

        File.WriteAllText(file.LocalPath, script, MyEncoding.GBK);
        return file;
    }

    public static FileEntry GenerateUnixScript(string scriptDir, string jreDirName, string launcherJarName, string title, int? ram)
    {
        var script = $$"""
            #!/usr/bin/env sh
            echo "{{title}}"

            # 分配内存大小
            RAM={{ram ?? DefaultRam}}M

            scriptPath=$(which $0)
            if [ -L $scriptPath ]; then
                sourceDir=$(dirname $(readlink -f $scriptPath))
            else
                sourceDir=$(dirname $scriptPath)
            fi
            JAVA_HOME="${sourceDir}/{{jreDirName}}"
            chmod +x "${JAVA_HOME}/bin/java"
            PATH="${JAVA_HOME}/bin:${PATH}"
            TZ={{TZ}}

            java \
              -Xms${RAM} -Xmx${RAM} \
              -Djava.awt.headless=true \
              -Duser.timezone=${TZ} \
              -Dlog4j2.formatMsgNoLookups=true \
              -jar "{{launcherJarName}}" \
              nogui
            """.Replace("\r\n", "\n");
        var file = new FileEntry(Path.Combine(scriptDir, "run.sh"))
            .WithArchiveEntryName("run.sh")
            .SetUnixExecutable();

        File.WriteAllText(file.LocalPath, script, MyEncoding.UTF8);
        return file;
    }

    public static string? InjectForgeScript(string scriptDir, string jreDirName, string title, int? ram)
    {
        var winPath = Directory.EnumerateFiles(scriptDir, "*.bat", SearchOption.TopDirectoryOnly).FirstOrDefault();
        var unixPath = Directory.EnumerateFiles(scriptDir, "*.sh", SearchOption.TopDirectoryOnly).FirstOrDefault();
        var jvmArgsFilePath = Directory.EnumerateFiles(scriptDir, "*jvm*.txt", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(scriptDir, "*args*.txt", SearchOption.TopDirectoryOnly))
            .FirstOrDefault();
        if (winPath == null || unixPath == null || jvmArgsFilePath == null)
            return null;

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            File.Delete(unixPath);
            InjectForgeWindowsScript(winPath, jvmArgsFilePath, jreDirName, title, ram);
            return Path.GetFileName(winPath);
        }
        else
        {
            File.Delete(winPath);
            InjectForgeUnixScript(unixPath, jvmArgsFilePath, jreDirName, title, ram);
            return Path.GetFileName(unixPath);
        }
    }

    public static void InjectForgeWindowsScript(string scriptPath, string jvmArgsPath, string jreDirName, string title, int? ram)
    {
        var originalContent = File.ReadAllText(scriptPath).Replace("\npause", "").Trim();
        var script = $$"""
            @echo off
            CHCP 936 >nul
            title {{title}}

            :: 如要修改分配内存大小或其它JVM参数，编辑文件"{{Path.GetFileName(jvmArgsPath)}}"
            
            set "JAVA_HOME=%~dp0%{{jreDirName}}"
            set "PATH=%JAVA_HOME%\bin;%PATH%"

            :: --------------- Official Script ---------------

            {{originalContent}}

            :: -----------------------------------------------

            echo.
            echo 按任意退出...
            pause>nul
            """;
        File.WriteAllText(scriptPath, script, MyEncoding.GBK);
        injectJvmArgsFile(jvmArgsPath, ram, false);
    }

    public static void InjectForgeUnixScript(string scriptPath, string jvmArgsPath, string jreDirName, string title, int? ram)
    {
        var originalContent = File.ReadAllText(scriptPath).Trim();
        var script = $$"""
            #!/usr/bin/env sh
            echo "{{title}}"
            
            # 如要修改分配内存大小或其它JVM参数，编辑文件"{{Path.GetFileName(jvmArgsPath)}}"

            scriptPath=$(which $0)
            if [ -L $scriptPath ]; then
                sourceDir=$(dirname $(readlink -f $scriptPath))
            else
                sourceDir=$(dirname $scriptPath)
            fi
            JAVA_HOME="${sourceDir}/{{jreDirName}}"
            chmod +x "${JAVA_HOME}/bin/java"
            PATH="${JAVA_HOME}/bin:${PATH}"
            TZ={{TZ}}

            # --------------- Official Script ---------------

            {{originalContent}} nogui

            # -----------------------------------------------
            """.Replace("\r\n", "\n");
        File.WriteAllText(scriptPath, script, MyEncoding.UTF8);
        injectJvmArgsFile(jvmArgsPath, ram, true);
    }

    static void injectJvmArgsFile(string jvmArgsPath, int? ram, bool awtHeadless)
    {
        var hasMemArgs = File.ReadAllLines(jvmArgsPath).Select(line => line.Trim())
            .Any(line => line.StartsWith("-Xmx") || line.StartsWith("-Xms"));

        using var sw = new StreamWriter(jvmArgsPath, true, MyEncoding.UTF8);
        sw.WriteLine();
        sw.WriteLine();
        if (awtHeadless)
        {
            sw.WriteLine($"-Djava.awt.headless=true");
        }
        sw.WriteLine($"-Duser.timezone={TZ}");
        sw.WriteLine($"-Dlog4j2.formatMsgNoLookups=true");
        if (!hasMemArgs)
        {
            sw.WriteLine();
            sw.WriteLine("# 分配内存大小");
            sw.WriteLine($"-Xmx{ram ?? DefaultRam}M");
            sw.WriteLine($"-Xms{ram ?? DefaultRam}M");
        }
    }
}
