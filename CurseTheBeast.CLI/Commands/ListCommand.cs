using CurseTheBeast.CLI.Commands.Options;
using CurseTheBeast.Core.Services;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace CurseTheBeast.CLI.Commands;

[Description("列出所有整合包")]
public class ListCommand : AsyncCommand<HttpOptions>
{
    public override async Task<int> ExecuteAsync(CommandContext context, HttpOptions options)
    {
        HttpConfigService.SetupHttp(null, null, options.NoProxy, options.Proxy, options.UserAgent);
        using var ftb = new FTBService();

        await ftb.ListAsync(false, default);

        return 0;
    }
}
