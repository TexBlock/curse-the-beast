using Spectre.Console.Cli;
using System.ComponentModel;

namespace CurseTheBeast.CLI.Commands.Options;


public abstract class DownloadOptions : HttpOptions
{
    [Description("下载服务端")]
    [CommandOption("-s|--server")]
    public bool Server { get; init; }

    [Description("预安装服务端，并且同意MC用户协议：https://aka.ms/MinecraftEULA")]
    [CommandOption("--agree-minecraft-eula")]
    public bool PreInstall { get; init; }

    [Description("并行下载数")]
    [CommandOption("-t|--thread")]
    public int? Thread { get; init; }

    [Description("输出目录或文件路径")]
    [CommandOption("-o|--output")]
    public string Output { get; init; } = Environment.CurrentDirectory;

    [Description("Curseforge API Key")]
    [CommandOption("-k|--curse-key")]
    public string? CurseforgeKey { get; init; }
}
